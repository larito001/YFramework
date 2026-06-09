Shader "Custom/TimeScaleShield"
{
    // 时间领域能量球（玻璃质感）：透过球看到的背景/物体被「玻璃式」折射扭曲（静态、无动态水波），
    // 边缘弯曲更强；轻微蓝色玻璃染色 + 锐利明亮的 Fresnel 边缘 + 与场景相交处同样的亮边高亮。
    // 依赖 URP 开启 Opaque Texture（折射）与 Depth Texture（相交高亮）。
    Properties
    {
        _MainColor       ("Glass Tint (blue)", Color) = (0.2, 0.55, 1.0, 1.0)
        _RimColor        ("Edge / Rim Color",  Color) = (0.6, 0.92, 1.0, 1.0)
        _Tint            ("Tint Amount",       Range(0, 1)) = 0.22
        _FresnelPower    ("Fresnel Power",     Range(1, 8)) = 4.0
        _FresnelIntensity("Edge Intensity",    Range(0, 8)) = 3.0
        _IntersectWidth  ("Intersect Width",   Range(0.01, 2)) = 0.3
        _Distortion      ("Glass Distortion",  Range(0, 0.2)) = 0.07
        _EdgeDistortion  ("Edge Bend Extra",   Range(0, 0.2)) = 0.05
        _NoiseScale      ("Warp Noise Scale",  Float) = 2.5
        _GlassAlpha      ("Glass Alpha",       Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _RimColor;
                float  _Tint;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _IntersectWidth;
                float  _Distortion;
                float  _EdgeDistortion;
                float  _NoiseScale;
                float  _GlassAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
            };

            float Hash(float3 p){ return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453); }
            float Noise(float3 p)
            {
                float3 i = floor(p); float3 f = frac(p); float3 u = f * f * (3.0 - 2.0 * f);
                float n000=Hash(i),               n100=Hash(i+float3(1,0,0));
                float n010=Hash(i+float3(0,1,0)), n110=Hash(i+float3(1,1,0));
                float n001=Hash(i+float3(0,0,1)), n101=Hash(i+float3(1,0,1));
                float n011=Hash(i+float3(0,1,1)), n111=Hash(i+float3(1,1,1));
                return lerp(lerp(lerp(n000,n100,u.x), lerp(n010,n110,u.x), u.y),
                            lerp(lerp(n001,n101,u.x), lerp(n011,n111,u.x), u.y), u.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.worldPos   = p.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.screenPos  = ComputeScreenPos(p.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE vf : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                N = IS_FRONT_VFACE(vf, N, -N);                       // 双面：背面翻法线
                float3 V = normalize(_WorldSpaceCameraPos - IN.worldPos);

                // 1) Fresnel 边缘
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // 2) 与场景相交边缘高亮
                float2 screenUV  = IN.screenPos.xy / IN.screenPos.w;
                float  sceneEye  = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  intersect = 1.0 - saturate((sceneEye - IN.screenPos.w) / max(_IntersectWidth, 1e-4));
                intersect *= intersect;

                // 3) 玻璃折射（静态，无动态水波）：
                //    a. 静态噪声场扭曲整个球面 → 透过球看到的物体/背景被扭曲（不只是边缘）
                //    b. 视空间法线在边缘额外增强弯曲（玻璃球边缘折射更强）
                float  n1 = Noise(IN.worldPos * _NoiseScale);
                float  n2 = Noise(IN.worldPos * _NoiseScale + 17.31);
                float2 warp = (float2(n1, n2) - 0.5) * 2.0;          // -1..1 静态扭曲方向
                float3 viewN = normalize(mul((float3x3)UNITY_MATRIX_V, N));
                float2 refrOffset = warp * _Distortion + viewN.xy * _EdgeDistortion;
                half3  scene = SampleSceneColor(screenUV + refrOffset);

                // 透出折射后的背景 + 轻微蓝色玻璃染色
                half3 col = scene * lerp(half3(1,1,1), _MainColor.rgb, _Tint);

                // 轮廓边缘 + 相交边缘 同一亮色发光
                float edge = saturate(max(fres, intersect));
                col += _RimColor.rgb * edge * _FresnelIntensity;

                float alpha = saturate(_GlassAlpha + edge);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
