Shader "Custom/GradeShader"
{
    Properties
    {
        _Scale ("Pattern Size", Range(0,10)) = 1
        _EvenColor("Color 1", Color) = (0,0,0,1)
        _OddColor("Color 2", Color) = (1,1,1,1)
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
    
            float _Scale;
            float4 _EvenColor;
            float4 _OddColor;
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
                float3 ad = IN.worldPos * _Scale;
                float chessboard = floor(ad.x) + floor(ad.z) + floor(ad.y);
                chessboard = frac(chessboard * 0.5); //只保留小数部分
                chessboard *= 2;
                float4 color = lerp(_EvenColor, _OddColor, chessboard);
                return color;
            }
            ENDCG
        }

    }
    FallBack "Diffuse"
}