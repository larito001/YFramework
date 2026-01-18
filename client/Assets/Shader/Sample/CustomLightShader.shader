Shader "Unlit/CustomLightShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Ramp ("Ramp", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
           
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 lightDir : TEXCOORD1;
                float3 normal: TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _Ramp;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                o.lightDir = lightDir;
                o.normal = v.normal;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float3 worldNormal = normalize(i.normal);
                float lightIntensity = max(0, dot(i.lightDir, worldNormal));
                lightIntensity=lightIntensity*0.5f+0.5f;
                lightIntensity = tex2D(_Ramp, lightIntensity).rgb;
                fixed4 col = tex2D(_MainTex, i.uv);
                return col*lightIntensity;
            }
            ENDCG
        }
    }
}
