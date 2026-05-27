Shader "Custom/CharacterHitFlash"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Albedo",        2D)            = "white" {}
        [MainColor]   _BaseColor ("Tint",          Color)         = (1, 1, 1, 1)
        _FlashColor              ("Flash Color",   Color)         = (1, 1, 1, 1)
        _FlashAmount             ("Flash Amount",  Range(0, 1))   = 0
        _AmbientStrength         ("Ambient",       Range(0, 1))   = 1

        // ── Dissolve（死亡溶解）──
        // 0=完整可见，1=完全消失。中间值：基于 worldspace hash 噪声 clip 像素，边缘附近用 _DissolveEdgeColor 高亮。
        _DissolveAmount          ("Dissolve Amount",    Range(0, 1))  = 0
        _DissolveEdgeColor       ("Dissolve Edge Color", Color)       = (1, 0.4, 0, 1)
        _DissolveEdgeWidth       ("Dissolve Edge Width", Range(0, 0.5)) = 0.05
        _DissolveScale           ("Dissolve Scale",     Float)        = 8.0
        _DissolveEdgeEmission    ("Dissolve Edge Emission", Range(0, 10)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        HLSLINCLUDE
        // 共用：worldspace hash 噪声 + 溶解 clip。用 hash3 是为了避免依赖外部 noise texture。
        // 缺点：完全随机分布，没有大尺度结构；如果想要"火焰边缘前沿"风格，后续可换 Voronoi/Worley
        float Hash3(float3 p)
        {
            p = frac(p * float3(0.1031, 0.1030, 0.0973));
            p += dot(p, p.yxz + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        // 返回 noise 值；调用方自己做 clip 和 edge 判定（避免 ForwardLit 之外的 pass 在编辑器报变量未引用）
        float DissolveNoise(float3 positionWS, float scale)
        {
            return Hash3(positionWS * scale);
        }
        ENDHLSL

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
                float  _DissolveAmount;
                float4 _DissolveEdgeColor;
                float  _DissolveEdgeWidth;
                float  _DissolveScale;
                float  _DissolveEdgeEmission;
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
                // 溶解 clip：noise<threshold 的像素直接丢弃，边缘窄带切到 _DissolveEdgeColor 当余辉
                float noise = DissolveNoise(IN.positionWS, _DissolveScale);
                float thr = _DissolveAmount;
                clip(noise - thr);

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

                // 溶解边缘高亮：clip 之后仍存活的像素，越靠近 threshold 越像火焰边
                // edge=1 在边缘最近处，0 在远离阈值的"完整身体"区域
                float edge = saturate(1.0 - (noise - thr) / max(_DissolveEdgeWidth, 1e-4));
                // _DissolveAmount=0 时强制 edge=0，避免静态下也看到一圈橙边
                edge *= step(0.0001, _DissolveAmount);
                outCol = lerp(outCol, _DissolveEdgeColor.rgb * _DissolveEdgeEmission, edge);

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
                float  _DissolveAmount;
                float4 _DissolveEdgeColor;
                float  _DissolveEdgeWidth;
                float  _DissolveScale;
                float  _DissolveEdgeEmission;
            CBUFFER_END

            struct ShadowAttrs
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            float4 GetShadowPositionHClip(ShadowAttrs IN, out float3 positionWS)
            {
                positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

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
                float3 positionWS;
                OUT.positionCS = GetShadowPositionHClip(IN, positionWS);
                OUT.positionWS = positionWS;
                return OUT;
            }

            // 阴影 pass 也要 clip，否则身体溶解了影子还完整
            half4 ShadowPassFragment(ShadowVaryings IN) : SV_Target
            {
                float noise = DissolveNoise(IN.positionWS, _DissolveScale);
                clip(noise - _DissolveAmount);
                return 0;
            }
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
                float  _DissolveAmount;
                float4 _DissolveEdgeColor;
                float  _DissolveEdgeWidth;
                float  _DissolveScale;
                float  _DissolveEdgeEmission;
            CBUFFER_END

            struct DepthAttrs   { float4 positionOS : POSITION; };
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            DepthVaryings DepthVert(DepthAttrs IN)
            {
                DepthVaryings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                float noise = DissolveNoise(IN.positionWS, _DissolveScale);
                clip(noise - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }
    }
}
