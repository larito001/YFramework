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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
