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
                o.pos = UnityObjectToClipPos(v.pos);
                o.worldPos = mul(unity_ObjectToWorld, v.pos).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(vertexToFrag i):SV_Target
            {
                //缩放和旋转
                float2 front = TRANSFORM_TEX(i.worldPos.xz, _MainTex);
                float2 side = TRANSFORM_TEX(i.worldPos.yx, _MainTex);
                float2 top = TRANSFORM_TEX(i.worldPos.zy, _MainTex);

                float4 frontColor = tex2D(_MainTex, front);
                float4 sideColor = tex2D(_MainTex, side);
                float4 topColor = tex2D(_MainTex, top);


                float3 weight =i.normal;
                weight = abs(weight);//取绝对值只关心强度
                weight = pow(weight, _Sharpness);//a的b次方
                //
                weight=weight / (weight.x + weight.y + weight.z);
                frontColor *= weight.z;
                sideColor *= weight.x;
                topColor *= weight.y;
                return frontColor + sideColor + topColor*_Color;
                
            }
            ENDCG
        }



    }
    FallBack "Diffuse"
}