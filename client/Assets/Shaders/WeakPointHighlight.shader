Shader "Hunting/WeakPointHighlight"
{
    // 弱点范围高亮:画在动物头/心脏部位碰撞盒上的半透明发光盒。
    // 盒面很淡(_FillAlpha),边缘按菲涅尔发亮(_RimAlpha)→ 看起来像全息标注框,不挡视线;
    // HDR 自发光 + 呼吸脉动,配合场景 URP Bloom 会泛光。双面渲染、不写深度。
    Properties
    {
        [HDR] _Color      ("Glow Color",        Color)        = (1.0, 0.2, 0.15, 1)
        _FillAlpha        ("Fill Alpha",        Range(0, 1))  = 0.12
        _RimAlpha         ("Rim Alpha",         Range(0, 2))  = 0.95
        _RimPower         ("Rim Power",         Range(0.5, 8))= 2.5
        _Intensity        ("Emission Intensity",Range(0, 8))  = 2.0
        _PulseSpeed       ("Pulse Speed",       Range(0, 12)) = 3.0
        _PulseAmount      ("Pulse Amount",      Range(0, 1))  = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "Highlight"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FillAlpha;
                float  _RimAlpha;
                float  _RimPower;
                float  _Intensity;
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

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n       = normalize(IN.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // 菲涅尔:正对相机的面淡,边缘亮 → 全息框感
                half fresnel = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);

                // 呼吸脉动(_PulseAmount=0 则恒定)
                half pulse = 1.0 - _PulseAmount + _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));

                half  alpha = saturate(_FillAlpha + fresnel * _RimAlpha) * pulse;
                half3 col   = _Color.rgb * _Intensity * (0.5 + 0.5 * fresnel) * pulse;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
