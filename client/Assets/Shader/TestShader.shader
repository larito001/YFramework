Shader "Custom/TestShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("MainTextrue", 2D) = "white" {}

    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" //分类标签
            "Queue"="Transparent"//渲染顺序
        }
        Blend SrcAlpha OneMinusSrcAlpha//开启混合，混合模式 FinalColor=SrcColor * SrcAlpha +DstColor * (1 - SrcAlpha)。一般是两个
        ZWrite off//关闭zwrite，防止覆盖其他物体
        Pass
        {
            CGPROGRAM
            sampler2D _MainTex;
            float4 _Color;

            struct modelData
            {
                //模型的顶点信息,模型空间
                float4 pos:POSITION;
                //顶点在纹理中的坐标
                float2 uv:TEXCOORD0;
            };

            struct vertexToFrag
            {
                float4 pos:SV_POSITION; //SystemValue
                float2 uv:TEXCOORD0;
            };
            #pragma vertex vert
            #pragma fragment frag
            vertexToFrag vert(modelData v)
            {
                vertexToFrag o;
                //物体空间直接转换为裁剪空间
                o.pos = UnityObjectToClipPos(v.pos);
                o.uv = v.uv;
                return o;
            }

            float4 frag(vertexToFrag i):SV_Target
            {
                //读取纹理
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }



    }
    FallBack "Diffuse"
}