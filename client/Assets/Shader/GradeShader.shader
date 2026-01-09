Shader "Custom/GradeShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "Queue" = "Geometry"
        }
        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            #pragma vertex  vert ;
            #pragma fragment frag;
            struct appdata
            {
                float3 pos:POSITION;
                float2 uv:TEXCOORD0;
            };

            struct v2f
            {
                float4 pos:SV_POSITION;
                float3 worldPos:TEXCOORD0;
            };


            v2f vert(appdata IN)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(IN.pos);
                o.worldPos = mul(unity_ObjectToWorld, IN.pos).xyz;
                return o;
            }

            float4 frag(v2f IN) : SV_Target
            {
                float chessboard = floor(IN.worldPos.x);
                chessboard = frac(chessboard * 0.5);//只保留小数部分
                // chessboard *= 2;
                return chessboard;
            }
            ENDCG
        }

    }
    FallBack "Diffuse"
}