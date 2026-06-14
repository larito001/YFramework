// 战争迷雾——黑雾面。
// 一张世界固定、盖住迷雾区域的水平 quad，UV 0..1 对应整张迷雾贴图（_MainTex）。
// 贴图 alpha = 雾浓度（0 可见 / 中间 已探索灰 / 1 未探索黑），由 FogOfWarManager 在 CPU 端逐帧算好并上传。
// 贴图 Bilinear 采样让格子之间自然过渡（软边）。配套脚本：FogOfWarManager。
Shader "Hidden/FogOfWar/FogOverlay"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0,0,0,1)
        _MainTex ("Fog", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half fa = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                return half4(_Color.rgb, fa * _Color.a);
            }
            ENDHLSL
        }
    }
}
