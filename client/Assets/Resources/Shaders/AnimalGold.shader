Shader "Custom/AnimalGold"
{
    // 把动物整体渲染成「黄金雕像 + 边缘泛光」:金色金属基底 + 主光高光 + 菲涅尔边缘 HDR 自发光(配合 URP Bloom 出泛光),
    // 自发光带轻微呼吸脉动。无需贴图——直接覆盖动物原材质即可。由 AnimalSystem 在刷怪时按概率换上。
    // 想要明显「泛光」请确保场景 Volume 里启用了 URP Bloom;即使没有 Bloom,边缘高亮自发光本身也已经很「金光闪闪」。
    Properties
    {
        [MainColor] _BaseColor   ("Gold Color",        Color)        = (1.0, 0.766, 0.336, 1)
        _SpecColor               ("Specular Color",     Color)        = (1.0, 0.9, 0.6, 1)
        _SpecPower               ("Specular Power",     Range(1, 256))= 48
        _SpecIntensity           ("Specular Intensity", Range(0, 8))  = 2.0

        [HDR] _RimColor          ("Rim/Glow Color",     Color)        = (1.0, 0.8, 0.3, 1)
        _RimPower                ("Rim Power",          Range(0.5, 8))= 3.0
        _EmissionIntensity       ("Emission Intensity", Range(0, 16)) = 4.0
        _GlowBase                ("Base Glow",          Range(0, 4))  = 0.6
        _PulseSpeed              ("Pulse Speed",        Range(0, 12)) = 3.0
        _PulseAmount             ("Pulse Amount",       Range(0, 1))  = 0.25
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

            // skinned mesh 顶点蒙皮由 URP 头文件支持;阴影接收 keyword
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float  _SpecPower;
                float  _SpecIntensity;
                float4 _RimColor;
                float  _RimPower;
                float  _EmissionIntensity;
                float  _GlowBase;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;
                OUT.positionWS  = v.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogCoord    = ComputeFogFactor(v.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // 主光 + 阴影
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(IN.positionHCS);
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);

                half  NdotL    = saturate(dot(n, mainLight.direction));
                half3 diffuse  = _BaseColor.rgb * mainLight.color * NdotL * mainLight.shadowAttenuation;
                half3 ambient  = _BaseColor.rgb * SampleSH(n);

                // 金属高光(Blinn-Phong),给金子那种锐利反光
                float3 halfDir = normalize(mainLight.direction + viewDir);
                half   specTerm = pow(saturate(dot(n, halfDir)), _SpecPower) * _SpecIntensity;
                half3  specular = mainLight.color * specTerm * _SpecColor.rgb * mainLight.shadowAttenuation;

                half3 lit = diffuse + ambient + specular;

                // 呼吸脉动(_PulseAmount=0 则恒定)
                half pulse = 1.0 - _PulseAmount + _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));

                // 菲涅尔边缘 HDR 自发光 → Bloom 抓取出泛光;再加一点整体底光让暗部也是金的
                half  fresnel  = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                half3 emission = _RimColor.rgb * fresnel * _EmissionIntensity * pulse
                               + _BaseColor.rgb * _GlowBase * pulse;

                half3 outCol = lit + emission;
                outCol = MixFog(outCol, IN.fogCoord);
                return half4(outCol, 1);
            }
            ENDHLSL
        }

        // 阴影投射:动物变金后仍要正常投影
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // 与 ForwardLit 保持一致的 UnityPerMaterial 布局(SRP Batcher 要求各 pass 一致)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float  _SpecPower;
                float  _SpecIntensity;
                float4 _RimColor;
                float  _RimPower;
                float  _EmissionIntensity;
                float  _GlowBase;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            struct ShadowAttrs { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            float3 _LightDirection;
            float3 _LightPosition;

            ShadowVaryings ShadowVert(ShadowAttrs IN)
            {
                ShadowVaryings OUT;
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
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // 深度:适配依赖深度的后处理 / SSAO
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

            // 与 ForwardLit 保持一致的 UnityPerMaterial 布局(SRP Batcher 要求各 pass 一致)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float  _SpecPower;
                float  _SpecIntensity;
                float4 _RimColor;
                float  _RimPower;
                float  _EmissionIntensity;
                float  _GlowBase;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            struct DepthAttrs   { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

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
