Shader "URP/PainterlyLighting"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(-2, 2)) = 1.0
        
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        
        [HDR]_SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        
        _ShadingGradient("Shading Gradient", 2D) = "white" {}
        _PainterlyGuide("Painterly Guide", 2D) = "white" {}
        _PainterlySmoothness("Painterly Smoothness", Range(0, 1)) = 0.1
        
        [HDR]_RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 8.0)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 1)) = 0.5
        
        //_ErosionTexture("Erosion Texture", 2D) = "white" {}
        _ErosionScale("Erosion Scale", Float) = 5.0
        _ErosionThreshold("Erosion Threshold", Range(0, 1)) = 0.5
        _ErosionSoftness("Erosion Softness", Range(0, 0.5)) = 0.1
        [Toggle] _UseVertexColorNormals("Use Vertex Color for Smooth Normals", Float) = 0
        
        [Header(Brush Stroke Edges)]
        //_BrushTexture("Brush Stroke Texture", 2D) = "white" {}
        _BrushScale("Brush Stroke Scale", Float) = 10.0
        _BrushStrength("Brush Strength", Range(0, 1)) = 0.3
        _BrushContrast("Brush Contrast", Range(1, 5)) = 2.0
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
                float4 vertexColor              : TEXCOORD8;
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord          : TEXCOORD6;
                #endif

                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                
                float4 positionCS               : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ShadingGradient);    SAMPLER(sampler_ShadingGradient);
            TEXTURE2D(_PainterlyGuide);     SAMPLER(sampler_PainterlyGuide);


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
                half _UseVertexColorNormals;
                float _BrushScale;
                half _BrushStrength;
                half _BrushContrast;
            CBUFFER_END

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
                output.tangentWS = tangentWS;
                
                output.positionWS = vertexInput.positionWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.positionCS = vertexInput.positionCS;
                output.vertexColor = input.color;

                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                return output;
            }

            half3 PainterlyLighting(half3 albedo, half3 normalWS, half3 lightDir, half3 viewDir, 
                                   half3 lightColor, half atten, half painterlyGuide, half smoothness)
            {
                // Diffuse calculation with painterly guide
                half nDotL = saturate(dot(normalWS, normalize(lightDir)) + 0.2);
                half diff = smoothstep(painterlyGuide - _PainterlySmoothness, 
                                      painterlyGuide + _PainterlySmoothness, nDotL);
                
                // Sample shading gradient
                half3 gradientColor = SAMPLE_TEXTURE2D(_ShadingGradient, sampler_ShadingGradient, float2(diff, 0.5)).rgb;
                
                // Specular calculation
                float3 refl = reflect(normalize(lightDir), normalWS);
                float vDotRefl = dot(normalize(viewDir), -refl);
                float specularThreshold = painterlyGuide + smoothness;
                half3 specular = _SpecularColor.rgb * lightColor * 
                                smoothstep(specularThreshold - _PainterlySmoothness, 
                                          specularThreshold + _PainterlySmoothness, vDotRefl) * smoothness;
                
                // Smooth attenuation
                atten = smoothstep(painterlyGuide - _PainterlySmoothness, 
                                  painterlyGuide + _PainterlySmoothness, atten);
                
                // Final color
                half3 color = (albedo * gradientColor * lightColor + specular) * atten;
                return color;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample textures
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                
                half painterlyGuide = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, input.uv).r;

                // Calculate normal in world space
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
                normalWS = NormalizeNormalPerPixel(normalWS);

                half3 viewDirWS = SafeNormalize(input.viewDirWS);

                // Shadow coord
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                // Main light
                Light mainLight = GetMainLight(shadowCoord);
                half3 color = PainterlyLighting(albedo, normalWS, mainLight.direction, viewDirWS, 
                                               mainLight.color, mainLight.shadowAttenuation * mainLight.distanceAttenuation,
                                               painterlyGuide, _Smoothness);

                // Additional lights
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    color += PainterlyLighting(albedo, normalWS, light.direction, viewDirWS,
                                             light.color, light.shadowAttenuation * light.distanceAttenuation,
                                             painterlyGuide, _Smoothness);
                }
                #endif

                // Ambient/GI
                half3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                color += albedo * bakedGI * 0.3;

                // Vertex lighting
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    color += input.fogFactorAndVertexLight.yzw * albedo;
                #endif

                // Rim erosion effect (based on cyn-prod.com technique)
                // Use smooth normals from vertex colors if available, otherwise use mesh normals
                half3 smoothNormal = normalWS;
                if (_UseVertexColorNormals > 0.5)
                {
                    // Vertex colors store normals in 0-1 range, convert to -1 to 1
                    smoothNormal = normalize(input.vertexColor.rgb * 2.0 - 1.0);
                    // Transform to world space (assuming they're stored in object space)
                    smoothNormal = normalize(mul((float3x3)unity_ObjectToWorld, smoothNormal));
                }
                
                // Create fresnel using smooth normals for better edge detection
                half NdotV = saturate(dot(smoothNormal, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _RimPower);
                
                // === BRUSH STROKE EDGE EFFECT ===
                // Sample brush texture with world-space triplanar mapping for consistent look
                float2 brushUV_XY = input.positionWS.xy * _BrushScale * 0.1;
                float2 brushUV_YZ = input.positionWS.yz * _BrushScale * 0.1;
                float2 brushUV_XZ = input.positionWS.xz * _BrushScale * 0.1;
                
                half3 brushSample;
                brushSample.x = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, brushUV_YZ).r;
                brushSample.y = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, brushUV_XZ).r;
                brushSample.z = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, brushUV_XY).r;
                
                // Weight by normal direction for triplanar blending
                half3 blendWeights = abs(normalWS);
                blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);
                half brushMask = dot(brushSample, blendWeights);
                
                // Increase contrast of brush strokes
                brushMask = saturate(pow(brushMask, _BrushContrast));
                
                // Apply brush texture to edges only (using fresnel as mask)
                // The brush texture will modulate the edge visibility
                half brushModulation = lerp(1.0, brushMask, _BrushStrength * fresnel);
                
                // === EROSION EFFECT ===
                // Sample erosion texture using world position for stable texture projection
                float2 erosionUV = input.positionWS.xy * _ErosionScale * 0.1;
                half erosionMask = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, erosionUV).r;
                
                // Add more variation by blending multiple projections
                float2 erosionUV2 = input.positionWS.yz * _ErosionScale * 0.1;
                half erosionMask2 = SAMPLE_TEXTURE2D(_PainterlyGuide, sampler_PainterlyGuide, erosionUV2).r;
                erosionMask = (erosionMask + erosionMask2) * 0.5;
                
                // Combine fresnel with erosion texture - fresnel acts as a mask
                // Only apply erosion on the edges (where fresnel is high)
                half erosionValue = fresnel + (erosionMask - 0.5) * 2.0; // Remap erosion to -1 to 1 range
                
                // Modulate erosion with brush texture for more organic edges
                erosionValue *= brushModulation;
                
                // Create stepped edge with smoothstep
                half rimMask = smoothstep(_ErosionThreshold - _ErosionSoftness, 
                                         _ErosionThreshold + _ErosionSoftness, 
                                         erosionValue);
                
                // Apply rim light with erosion and brush strokes
                half3 rimLight = _RimColor.rgb * rimMask * _RimIntensity;
                color += rimLight;

                // Fog
                color = MixFog(color, input.fogFactorAndVertexLight.x);

                // Use erosion mask to control transparency (eroded areas become transparent)
                // Invert rimMask so high erosion = low alpha (transparent)
                // Also preserve base texture alpha
                half alpha = albedoAlpha.a * (1.0 - rimMask);

                return half4(color, alpha);
            }
            ENDHLSL
        }

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