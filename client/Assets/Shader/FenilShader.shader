Shader "Unlit/FenilShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        //        _EmissionColor ("Emission", Color) = (1,1,1,1)
        _EmissionColorTex ("Texture", 2D) = "white" {}
        [PowerSlider(4)] _FresnelExponent ("Fresnel Exponent", Range(0.25, 4)) = 1
        _Ramp ("Toon Ramp", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

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
                float3 normal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float3 lightDir: TEXCOORD4;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            // float4 _EmissionColor;
            float _FresnelExponent;
            sampler2D _EmissionColorTex;
            sampler2D _Ramp;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = mul(unity_ObjectToWorld, v.normal).xyz;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                float3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                o.lightDir = lightDir;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float towardsLight = dot(i.normal, i.lightDir);
                towardsLight = towardsLight * 0.5 + 0.5;
                float3 lightIntensity = tex2D(_Ramp, towardsLight).rgb;
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 _EmissionColor = tex2D(_EmissionColorTex, i.uv);
                float fresnel = dot(i.normal, i.viewDir);
                fresnel = saturate(1 - fresnel); //限制在0-1之间
                fresnel = pow(fresnel, _FresnelExponent);
                float4 baseColor = col * 0.8f;
                col.xyz=lightIntensity*col.xyz;
                return baseColor+ col + fresnel * _EmissionColor ;
            }
            ENDCG
        }
    }
}