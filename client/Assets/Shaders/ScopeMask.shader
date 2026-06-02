Shader "Hunting/ScopeMask"
{
    // 瞄准镜黑边遮罩:贴在一张铺满屏幕的 UI RawImage 上。
    // 屏幕中心一个圆内完全透明(看见场景),圆外是不透明黑(镜筒外),圆边一圈软过渡。
    // 圆按屏幕宽高比校正,保证是正圆而不是椭圆。半径/软边/颜色都可在材质上调。
    Properties
    {
        // uGUI 渲染 UI 图形时会把图形纹理赋给 _MainTex;本 shader 不采样它,
        // 但必须声明该属性,否则每帧告警 "doesn't have a texture property '_MainTex'"。
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("镜外颜色", Color) = (0,0,0,1)
        _Radius  ("镜孔半径(按屏幕半高归一化)", Range(0,1)) = 0.42
        _Feather ("软边宽度", Range(0,0.5)) = 0.04
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float _Radius;
            float _Feather;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // RawImage 铺满屏幕时 uv 为 0..1
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 以屏幕中心为原点,按宽高比把横轴拉伸成与纵轴同尺度 → 正圆
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = i.uv - 0.5;
                d.x *= aspect;
                float dist = length(d);

                // 圆内 alpha=0(透明),圆外 alpha=1(不透明黑),边缘 _Feather 软过渡
                float a = smoothstep(_Radius, _Radius + _Feather, dist);
                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
