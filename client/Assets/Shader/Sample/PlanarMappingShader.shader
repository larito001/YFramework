Shader "Unlit/PlanarMappingShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

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
            };

            struct v2f
            {
                float3 worldPos : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                fixed2 top = TRANSFORM_TEX(i.worldPos.xz, _MainTex);
                fixed2 front = TRANSFORM_TEX(i.worldPos.xy, _MainTex);
                fixed2 right = TRANSFORM_TEX(i.worldPos.zy, _MainTex);
                fixed4 coltop = tex2D(_MainTex, top);
                fixed4 colfront = tex2D(_MainTex, front);
                fixed4 colright = tex2D(_MainTex, right);
                fixed4 col = coltop + colfront + colright;
                return col / 3;
            }
            ENDCG
        }
    }
}