Shader "Custom/TestShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("MainTextrue", 2D) = "white" {}
        _SecondaryTex("Secondary Texture", 2D)="white"{}
		_SecondaryColor ("Secondary Color", Color) = (1,1,1,1) //the color to blend to
//		_Blend ("Blend Value", Range(0,1)) = 0 //0 is the first color, 1 the second
        _BlendTextrue ("Blend Texture", 2D) = "white"{}
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
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _SecondaryTex;
            sampler2D _BlendTextrue;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _SecondaryColor;
            // float _Blend;

            struct modelData
            {
                //模型的顶点信息,模型空间
                float4 pos:POSITION;
                // //顶点在纹理中的坐标
                // float2 uv:TEXCOORD0;
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
                float4 worldPos = mul(unity_ObjectToWorld, v.pos);
                o.uv =worldPos.xz;//TRANSFORM_TEX(v.pos.xz, _MainTex);//把“原始 UV 坐标”按照材质面板中的 Tiling（平铺）和 Offset（偏移）进行线性变换。
                return o;
            }

            float4 frag(vertexToFrag i):SV_Target
            {
                //读取纹理
                return lerp(tex2D(_MainTex, i.uv), tex2D(_SecondaryTex, i.uv),tex2D(_BlendTextrue, i.uv)) ;//lerp(_Color, _SecondaryColor, _Blend)
            }
            ENDCG
        }



    }
    FallBack "Diffuse"
}