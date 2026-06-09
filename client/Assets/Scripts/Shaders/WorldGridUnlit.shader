Shader "Custom/WorldGridUnlit"
{
    Properties
    {
        _BgColor   ("Background Color",     Color) = (0.15, 0.15, 0.15, 1)
        _GridColor ("Grid Color (1m)",      Color) = (1, 1, 1, 1)
        _MainSize  ("Grid Size (m)",        Float) = 1.0
        _LineWidth ("Line Width (px)",      Float) = 1.5
        [Enum(Auto,0, XZ,1, XY,2, YZ,3)] _Plane ("Grid Plane", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _GridColor;
                float  _MainSize;
                float  _LineWidth;
                float  _Plane;
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
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;
                OUT.positionWS  = v.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // 按法线最大分量自动选平面：法线朝上用 XZ，朝前用 XY，朝右用 YZ
            float2 PickPlane(float3 ws, float3 n)
            {
                if (_Plane < 0.5)
                {
                    float3 an = abs(n);
                    if (an.y >= an.x && an.y >= an.z) return ws.xz;
                    if (an.x >= an.z)                 return ws.yz;
                    return ws.xy;
                }
                if (_Plane < 1.5) return ws.xz;
                if (_Plane < 2.5) return ws.xy;
                return ws.yz;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n     = normalize(IN.normalWS);
                float2 ws    = PickPlane(IN.positionWS, n);
                float2 coord = ws / _MainSize;

                // derivative-based AA：让线宽在屏幕空间近似为 _LineWidth 像素
                float2 d     = fwidth(coord);
                float2 grid  = abs(frac(coord - 0.5) - 0.5) / max(d, 1e-6);
                float  line1 = min(grid.x, grid.y);
                float  mask  = 1.0 - saturate(line1 / _LineWidth);

                // 掠射/远处一像素覆盖 >1 个格子时无法准确显示，淡出避免整片亮
                float  fade  = saturate(1.5 - max(d.x, d.y));
                mask *= fade;

                half3 col = lerp(_BgColor.rgb, _GridColor.rgb, mask);

                // 采样主光阴影
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(IN.positionHCS);
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                // 阴影只压暗，不染色；0.5 是最暗到原色 50%，按需调
                col *= lerp(0.5, 1.0, shadow);

                return half4(col, 1);
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _GridColor;
                float  _MainSize;
                float  _LineWidth;
                float  _Plane;
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

            half4 ShadowPassFragment(ShadowVaryings IN) : SV_Target
            {
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _GridColor;
                float  _MainSize;
                float  _LineWidth;
                float  _Plane;
            CBUFFER_END

            struct DepthAttrs { float4 positionOS : POSITION; };
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
