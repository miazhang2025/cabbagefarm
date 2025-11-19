Shader "URP/PainterlyLighting_Refined"
{
    Properties
    {
        [Header(Base Material)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        
        [Header(Normal Mapping)]
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(-2, 2)) = 1.0
        
        [Header(Surface Properties)]
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        
        [Header(Painterly Shading)]
        _ShadingGradient("Shading Gradient", 2D) = "white" {}
        _PainterlyGuide("Painterly Guide", 2D) = "white" {}
        _PainterlySmoothness("Shading Transition Smoothness", Range(0, 1)) = 0.1
        
        [Header(Specular)]
        [HDR]_SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        
        [Header(Rim Lighting)]
        [HDR]_RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 8.0)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 1)) = 0.5
        
        [Header(Erosion Effect)]
        _ErosionScale("Erosion Scale", Float) = 5.0
        _ErosionThreshold("Erosion Threshold", Range(0, 1)) = 0.5
        _ErosionSoftness("Erosion Softness", Range(0, 0.5)) = 0.1
        [Toggle(_USE_VERTEX_COLOR_NORMALS)] _UseVertexColorNormals("Use Vertex Color for Smooth Normals", Float) = 0
        
        [Header(Brush Stroke Edges)]
        _BrushScale("Brush Stroke Scale", Float) = 10.0
        _BrushStrength("Brush Strength", Range(0, 1)) = 0.3
        _BrushContrast("Brush Contrast", Range(1, 5)) = 2.0
        
        [Header(Ambient)]
        _AmbientStrength("Ambient/GI Strength", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3 glcore
            
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // Shader features
            #pragma shader_feature_local _USE_VERTEX_COLOR_NORMALS

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ============================================
            // STRUCTURES
            // ============================================
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
                float3 positionWS               : TEXCOORD1;
                float3 normalWS                 : TEXCOORD2;
                half4 tangentWS                 : TEXCOORD3;
                float3 viewDirWS                : TEXCOORD4;
                half4 fogFactorAndVertexLight   : TEXCOORD5;
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord          : TEXCOORD6;
                #endif

                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                
                #ifdef _USE_VERTEX_COLOR_NORMALS
                    float4 vertexColor          : TEXCOORD8;
                #endif
                
                float4 positionCS               : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ============================================
            // TEXTURES AND SAMPLERS
            // ============================================
            
            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ShadingGradient);    SAMPLER(sampler_ShadingGradient);
            TEXTURE2D(_PainterlyGuide);     SAMPLER(sampler_PainterlyGuide);

            // ============================================
            // UNIFORMS
            // ============================================
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SpecularColor;
                half4 _RimColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                half _PainterlySmoothness;
                half _RimPower;
                half _RimIntensity;
                float _ErosionScale;
                half _ErosionThreshold;
                half _ErosionSoftness;
                float _BrushScale;
                half _BrushStrength;
                half _BrushContrast;
                half _AmbientStrength;
            CBUFFER_END

            // ============================================
            // HELPER FUNCTIONS
            // ============================================
            
            // Sample texture using triplanar mapping for UV-free consistent appearance
            half SampleTriplanar(TEXTURE2D_PARAM(tex, samplerTex), float3 worldPos, float3 normal, float scale)
            {
                float2 uvX = worldPos.yz * scale;
                float2 uvY = worldPos.xz * scale;
                float2 uvZ = worldPos.xy * scale;
                
                half3 samples;
                samples.x = SAMPLE_TEXTURE2D(tex, samplerTex, uvX).r;
                samples.y = SAMPLE_TEXTURE2D(tex, samplerTex, uvY).r;
                samples.z = SAMPLE_TEXTURE2D(tex, samplerTex, uvZ).r;
                
                // Blend based on surface normal
                half3 blendWeights = abs(normal);
                blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);
                
                return dot(samples, blendWeights);
            }

            // Painterly lighting calculation with stylized shading
            half3 PainterlyLighting(
                half3 albedo, 
                half3 normalWS, 
                half3 lightDir, 
                half3 viewDir, 
                half3 lightColor, 
                half attenuation, 
                half painterlyGuide, 
                half smoothness)
            {
                // Normalize inputs
                lightDir = normalize(lightDir);
                viewDir = normalize(viewDir);
                
                // Diffuse with bias for better illumination
                half nDotL = saturate(dot(normalWS, lightDir) + 0.2);
                
                // Apply painterly guide and smooth transition
                half diffuseStep = smoothstep(
                    painterlyGuide - _PainterlySmoothness, 
                    painterlyGuide + _PainterlySmoothness, 
                    nDotL
                );
                
                // Sample custom shading gradient
                half3 gradientColor = SAMPLE_TEXTURE2D(_ShadingGradient, sampler_ShadingGradient, float2(diffuseStep, 0.5)).rgb;
                
                // Specular with reflection
                half3 reflectionDir = reflect(-lightDir, normalWS);
                half specularDot = saturate(dot(viewDir, reflectionDir));
                half specularThreshold = painterlyGuide + smoothness;
                
                half specularStep = smoothstep(
                    specularThreshold - _PainterlySmoothness, 
                    specularThreshold + _PainterlySmoothness, 
                    specularDot
                );
                
                half3 specular = _SpecularColor.rgb * lightColor * specularStep * smoothness;
                
                // Smooth attenuation transition
                attenuation = smoothstep(
                    painterlyGuide - _PainterlySmoothness, 
                    painterlyGuide + _PainterlySmoothness, 
                    attenuation
                );
                
                // Combine diffuse and specular
                return (albedo * gradientColor * lightColor + specular) * attenuation;
            }

            // Calculate rim erosion effect with brush strokes
            half CalculateRimErosion(float3 positionWS, half3 normalWS, half3 viewDirWS, half3 smoothNormal)
            {
                // Fresnel for edge detection
                half NdotV = saturate(dot(smoothNormal, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _RimPower);
                
                // Brush stroke texture using triplanar
                half brushMask = SampleTriplanar(
                    TEXTURE2D_ARGS(_PainterlyGuide, sampler_PainterlyGuide),
                    positionWS,
                    normalWS,
                    _BrushScale * 0.1
                );
                
                // Increase contrast of brush strokes
                brushMask = saturate(pow(brushMask, _BrushContrast));
                
                // Apply brush to edges only
                half brushModulation = lerp(1.0, brushMask, _BrushStrength * fresnel);
                
                // Erosion texture (using two projections for variation)
                float2 erosionUV1 = positionWS.xy * _ErosionScale * 0.1;
                float2 erosionUV2 = positionWS.yz * _ErosionScale * 0.1;
                
                half erosion1 = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, erosionUV1).r;
                half erosion2 = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, erosionUV2).r;
                half erosionMask = (erosion1 + erosion2) * 0.5;
                
                // Combine fresnel with erosion (remap to -1 to 1)
                half erosionValue = fresnel + (erosionMask - 0.5) * 2.0;
                
                // Modulate with brush strokes
                erosionValue *= brushModulation;
                
                // Create stepped edge
                return smoothstep(
                    _ErosionThreshold - _ErosionSoftness, 
                    _ErosionThreshold + _ErosionSoftness, 
                    erosionValue
                );
            }

            // ============================================
            // VERTEX SHADER
            // ============================================
            
            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Transform positions
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                // Basic outputs
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                // Normals and tangents
                output.normalWS = normalInput.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
                
                // Lighting data
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                // Lightmap and SH
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                // Shadow coordinates
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif
                
                // Vertex colors (for smooth normals if enabled)
                #ifdef _USE_VERTEX_COLOR_NORMALS
                    output.vertexColor = input.color;
                #endif

                return output;
            }

            // ============================================
            // FRAGMENT SHADER
            // ============================================
            
            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ========================================
                // SAMPLE TEXTURES
                // ========================================
                
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), 
                    _BumpScale
                );
                
                half painterlyGuide = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, input.uv).r;

                // ========================================
                // CALCULATE WORLD SPACE NORMAL
                // ========================================
                
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(
                    normalTS, 
                    half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz)
                );
                normalWS = NormalizeNormalPerPixel(normalWS);

                half3 viewDirWS = SafeNormalize(input.viewDirWS);

                // ========================================
                // SHADOW COORDINATES
                // ========================================
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                // ========================================
                // MAIN LIGHT
                // ========================================
                
                Light mainLight = GetMainLight(shadowCoord);
                half3 color = PainterlyLighting(
                    albedo, 
                    normalWS, 
                    mainLight.direction, 
                    viewDirWS, 
                    mainLight.color, 
                    mainLight.shadowAttenuation * mainLight.distanceAttenuation,
                    painterlyGuide, 
                    _Smoothness
                );

                // ========================================
                // ADDITIONAL LIGHTS
                // ========================================
                
                #ifdef _ADDITIONAL_LIGHTS
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        color += PainterlyLighting(
                            albedo, 
                            normalWS, 
                            light.direction, 
                            viewDirWS,
                            light.color, 
                            light.shadowAttenuation * light.distanceAttenuation,
                            painterlyGuide, 
                            _Smoothness
                        );
                    }
                #endif

                // ========================================
                // AMBIENT / GI
                // ========================================
                
                half3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                color += albedo * bakedGI * _AmbientStrength;

                // ========================================
                // VERTEX LIGHTING
                // ========================================
                
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    color += input.fogFactorAndVertexLight.yzw * albedo;
                #endif

                // ========================================
                // RIM LIGHTING WITH EROSION
                // ========================================
                
                // Determine smooth normals (for better edge detection)
                half3 smoothNormal = normalWS;
                
                #ifdef _USE_VERTEX_COLOR_NORMALS
                    // Vertex colors store normals in 0-1 range, convert to -1 to 1
                    smoothNormal = normalize(input.vertexColor.rgb * 2.0 - 1.0);
                    // Transform to world space
                    smoothNormal = normalize(mul((float3x3)unity_ObjectToWorld, smoothNormal));
                #endif
                
                // Calculate rim mask with erosion
                half rimMask = CalculateRimErosion(input.positionWS, normalWS, viewDirWS, smoothNormal);
                
                // Apply rim light
                half3 rimLight = _RimColor.rgb * rimMask * _RimIntensity;
                color += rimLight;

                // ========================================
                // FOG
                // ========================================
                
                color = MixFog(color, input.fogFactorAndVertexLight.x);

                // ========================================
                // ALPHA (TRANSPARENCY FROM EROSION)
                // ========================================
                
                // Eroded areas become transparent
                half alpha = albedoAlpha.a * (1.0 - rimMask);

                return half4(color, alpha);
            }
            ENDHLSL
        }

        // ============================================
        // SHADOW CASTER PASS
        // ============================================
        
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3 glcore
            
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // ============================================
        // DEPTH ONLY PASS
        // ============================================
        
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3 glcore
            
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 position     : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // ============================================
        // DEPTH NORMALS PASS
        // ============================================
        
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3 glcore
            
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS     : POSITION;
                float3 normalOS       : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 0.0, 0.0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
