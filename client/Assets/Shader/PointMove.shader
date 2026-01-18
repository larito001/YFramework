Shader "Unlit/PointMove"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Amplitude ("Wave Size", Range(0,1)) = 0.4
        _Frequency ("Wave Freqency", Range(1, 8)) = 2
        _Ramp ("Ramp Texture", 2D) = "white" {}
        _AnimationSpeed ("Animation Speed", Range(0,5)) = 1
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

            struct appdata
            {
                float4 vertex : POSITION;
                //切线
                float3 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 lightDir: TEXCOORD4;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Amplitude;
            float _Frequency;
            sampler2D _Ramp;
            float _AnimationSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                float4 modifiedPos = v.vertex;
                //对顶点位移
                modifiedPos.y += sin(v.vertex.x * _Frequency+_Time.y*_AnimationSpeed) * _Amplitude;

                //顶点位置+切线
                float3 posPlusTangent = v.vertex + v.tangent * 0.01;
                //对切线位移
                posPlusTangent.y += sin(posPlusTangent.x * _Frequency+_Time.y*_AnimationSpeed) * _Amplitude;

                //侧切线
                float3 bitangent = cross(v.normal, v.tangent);
                //对侧切线位移
                float3 posPlusBitangent = v.vertex + bitangent * 0.01;
                posPlusBitangent.y += sin(posPlusBitangent.x * _Frequency+_Time.y*_AnimationSpeed) * _Amplitude;

                //获取位移后的切线
                float3 modifiedTangent = posPlusTangent - modifiedPos;
                //获取位移后的侧切线
                float3 modifiedBitangent = posPlusBitangent - modifiedPos;
                //获取位移后的法线
                float3 modifiedNormal = cross(modifiedTangent, modifiedBitangent);

                //传递到片元
                o.normal = normalize(modifiedNormal);
                o.vertex = UnityObjectToClipPos(modifiedPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                o.lightDir = lightDir;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float towardsLight = dot(i.normal, i.lightDir);
                towardsLight = towardsLight * 0.5 + 0.5;
                float3 lightIntensity = tex2D(_Ramp, towardsLight).rgb;
                fixed4 col = tex2D(_MainTex, i.uv);
                col.xyz = lightIntensity * col.xyz;
                return col; 
            }
            ENDCG
        }
    }
}