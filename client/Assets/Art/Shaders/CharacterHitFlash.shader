Shader "Custom/CharacterHitFlash"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Albedo",        2D)            = "white" {}
        [MainColor]   _BaseColor ("Tint",          Color)         = (1, 1, 1, 1)
        _FlashColor              ("Flash Color",   Color)         = (1, 1, 1, 1)
        _FlashAmount             ("Flash Amount",  Range(0, 1))   = 0
        _AmbientStrength         ("Ambient",       Range(0, 1))   = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // skinned mesh 走顶点蒙皮，URP 头文件已包含相关支持
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _FlashColor;
                float  _FlashAmount;
                float  _AmbientStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;
                OUT.positionWS  = v.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord    = ComputeFogFactor(v.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 n = normalize(IN.normalWS);

                // 主光 + 阴影
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(IN.positionHCS);
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);

                half  NdotL   = saturate(dot(n, mainLight.direction));
                half3 diffuse = baseTex.rgb * mainLight.color * NdotL * mainLight.shadowAttenuation;
                half3 ambient = baseTex.rgb * SampleSH(n) * _AmbientStrength;
                half3 lit     = diffuse + ambient;

                // _FlashAmount=0 渲染正常，=1 全身被 _FlashColor 覆盖；中间值线性过渡
                // 注意是叠加在 lit 之后，避免阴影/光照"吞掉"闪光
                half3 outCol = lerp(lit, _FlashColor.rgb, saturate(_FlashAmount));

                outCol = MixFog(outCol, IN.fogCoord);
                return half4(outCol, baseTex.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _FlashColor;
                float  _FlashAmount;
                float  _AmbientStrength;
            CBUFFER_END

            struct ShadowAttrs
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            float4 GetShadowPositionHClip(ShadowAttrs IN)
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

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

            ShadowVaryings ShadowPassVertex(ShadowAttrs IN)
            {
                ShadowVaryings OUT;
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 ShadowPassFragment(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _FlashColor;
                float  _FlashAmount;
                float  _AmbientStrength;
            CBUFFER_END

            struct DepthAttrs   { float4 positionOS : POSITION; };
            struct DepthVaryings{ float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttrs IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}