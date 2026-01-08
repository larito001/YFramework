Shader "Custom/TestShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("MainTextrue", 2D) = "white" {}
        _Sharpness ("Blend sharpness", Range(1, 64)) = 1

    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" //分类标签
            "Queue"="Geometry"//渲染顺序
        }
        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Sharpness;

            struct modelData
            {
                //模型的顶点信息,模型空间
                float4 pos:POSITION;
                float3 normal:NORMAL;
            };

            struct vertexToFrag
            {
                float4 pos:SV_POSITION; //SystemValue
                float3 normal:NORMAL;
                float3 worldPos:TEXCOORD0;
            };
            #pragma vertex vert
            #pragma fragment frag
            vertexToFrag vert(modelData v)
            {
                vertexToFrag o;
                //物体空间直接转换为裁剪空间
                o.pos = UnityObjectToClipPos(v.pos);
                float4 worldPos = mul(unity_ObjectToWorld, v.pos);
                o.worldPos = worldPos.xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); //法线从物体空间转换为世界空间

                return o;
            }

            float4 frag(vertexToFrag i):SV_Target
            {

                float2 uv_front = TRANSFORM_TEX(i.worldPos.xy, _MainTex);
                float2 uv_side = TRANSFORM_TEX(i.worldPos.zy, _MainTex);
                float2 uv_top = TRANSFORM_TEX(i.worldPos.xz, _MainTex);
                float4 fron = tex2D(_MainTex, uv_front);
                float4 side = tex2D(_MainTex, uv_side);
                float4 top = tex2D(_MainTex, uv_top);

                float3 weights = i.normal;
                weights = abs(weights);
                		weights = pow(weights, _Sharpness);
                weights = weights / (weights.x + weights.y + weights.z);
                fron *= weights.z;
                side *= weights.x;
                top *= weights.y;
                return (fron + side + top) * _Color;
            }
            ENDCG
        }



    }
    FallBack "Diffuse"
}