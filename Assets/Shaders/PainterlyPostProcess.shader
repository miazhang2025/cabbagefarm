Shader "Hidden/Custom/PainterlyPostProcess"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PainterlyPostProcess"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_ShadingGradient);
            SAMPLER(sampler_ShadingGradient);

            TEXTURE2D(_PainterlyGuide);
            SAMPLER(sampler_PainterlyGuide);

            TEXTURE2D(_BrushTexture);
            SAMPLER(sampler_BrushTexture);

            float _PainterlySmoothness;
            float4 _SpecularColor;
            float _Smoothness;
            float _NormalBias;
            float _Intensity;
            float _BrushScale;
            float _BrushStrength;
            float _EdgeThreshold;
            float _EdgeWidth;
            float _ErosionStrength;

            float3 ReconstructWorldPos(float2 uv, float depth)
            {
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif
                
                float4 clipPos = float4(uv * 2.0 - 1.0, depth, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    clipPos.y = -clipPos.y;
                #endif
                
                float4 viewPos = mul(UNITY_MATRIX_I_P, clipPos);
                viewPos /= viewPos.w;
                float3 worldPos = mul(UNITY_MATRIX_I_V, viewPos).xyz;
                return worldPos;
            }

            // Edge detection using depth and normal discontinuities
            float DetectEdge(float2 uv, float depth, float3 normal)
            {
                float2 texelSize = _ScreenParams.zw - 1.0;
                float edge = 0.0;
                
                // Sample neighboring pixels
                float depthL = SampleSceneDepth(uv + float2(-texelSize.x, 0) * _EdgeWidth);
                float depthR = SampleSceneDepth(uv + float2(texelSize.x, 0) * _EdgeWidth);
                float depthU = SampleSceneDepth(uv + float2(0, texelSize.y) * _EdgeWidth);
                float depthD = SampleSceneDepth(uv + float2(0, -texelSize.y) * _EdgeWidth);
                
                // Depth discontinuity
                float depthDiff = abs(depthL - depth) + abs(depthR - depth) + 
                                 abs(depthU - depth) + abs(depthD - depth);
                edge = saturate(depthDiff * 100.0);
                
                // Normal discontinuity
                float3 normalL = SampleSceneNormals(uv + float2(-texelSize.x, 0) * _EdgeWidth);
                float3 normalR = SampleSceneNormals(uv + float2(texelSize.x, 0) * _EdgeWidth);
                float3 normalU = SampleSceneNormals(uv + float2(0, texelSize.y) * _EdgeWidth);
                float3 normalD = SampleSceneNormals(uv + float2(0, -texelSize.y) * _EdgeWidth);
                
                float normalDiff = (1.0 - dot(normal, normalL)) + (1.0 - dot(normal, normalR)) +
                                  (1.0 - dot(normal, normalU)) + (1.0 - dot(normal, normalD));
                edge += saturate(normalDiff * 2.0);
                
                return saturate(edge);
            }

            // Noise function for erosion variation
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.13);
                p3 += dot(p3, p3.yzx + 3.333);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Sample depth and normals
                float depth = SampleSceneDepth(uv);
                float3 worldNormal = SampleSceneNormals(uv);
                float3 worldPos = ReconstructWorldPos(uv, depth);

                // Skip skybox
                #if UNITY_REVERSED_Z
                    if (depth < 0.0001)
                        return color;
                #else
                    if (depth > 0.9999)
                        return color;
                #endif

                // Get main light
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                // Detect edges for erosion
                float edgeMask = DetectEdge(uv, depth, worldNormal);
                float erosion = step(_EdgeThreshold, edgeMask) * _ErosionStrength;

                // Apply brush texture
                float2 brushUV = uv * _BrushScale;
                float brushPattern = 1.0;
                #if defined(_BrushTexture)
                    brushPattern = SAMPLE_TEXTURE2D(_BrushTexture, sampler_BrushTexture, brushUV).r;
                    brushPattern = lerp(1.0, brushPattern, _BrushStrength);
                #endif

                // Combine erosion with brush for painterly edges
                float edgeNoise = Hash(uv * 100.0);
                float painterlyMask = 1.0 - (erosion * edgeNoise);
                
                // Early out if pixel is eroded away
                if (painterlyMask < 0.1 && erosion > 0.5)
                {
                    // Return darkened color at eroded edges
                    return half4(color.rgb * 0.3, color.a);
                }

                // Sample painterly guide (if available, otherwise use a default)
                float painterlyGuide = 0.5;
                #if defined(_PainterlyGuide)
                    painterlyGuide = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, uv).r;
                #endif

                // Diffuse calculation with bias
                float nDotL = saturate(dot(worldNormal, lightDir) + _NormalBias);
                float diff = smoothstep(
                    painterlyGuide - _PainterlySmoothness, 
                    painterlyGuide + _PainterlySmoothness, 
                    nDotL
                );

                // Sample shading gradient
                float3 shadingColor = SAMPLE_TEXTURE2D(_ShadingGradient, sampler_ShadingGradient, float2(diff, 0.5)).rgb;

                // Specular calculation
                float3 reflDir = reflect(-lightDir, worldNormal);
                float vDotR = saturate(dot(viewDir, reflDir));
                float specularThreshold = painterlyGuide + _Smoothness;
                float specularMask = smoothstep(
                    specularThreshold - _PainterlySmoothness,
                    specularThreshold + _PainterlySmoothness,
                    vDotR
                );
                float3 specular = _SpecularColor.rgb * specularMask * _Smoothness;

                // Combine lighting
                float3 litColor = color.rgb * shadingColor * mainLight.color + specular;

                // Apply brush texture variation to final color
                litColor *= brushPattern;
                
                // Apply painterly mask for edge effects
                litColor = lerp(litColor * 0.5, litColor, painterlyMask);

                // Blend with original color based on intensity
                float3 finalColor = lerp(color.rgb, litColor, _Intensity);

                return half4(finalColor, color.a);
            }
            ENDHLSL
        }
    }
}