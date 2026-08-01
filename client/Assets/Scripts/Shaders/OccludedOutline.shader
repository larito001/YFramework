Shader "Custom/OccludedOutline"
{
    // 「被墙挡住时给角色描边」效果（俯视角常见的 X-ray 轮廓）。
    // 原理：单独一个**透明队列**材质，只在角色被前方不透明物体（墙体）遮挡的像素上绘制
    //   —— 用 ZTest Greater 判定"片元深度比深度缓冲里的更远 = 被挡住"。
    //   再用 Fresnel（边缘亮、正面透明）把实心剪影变成**轮廓描边**：interior 透明，silhouette 发光。
    //
    // 为什么放 Transparent 队列：透明物在所有不透明物之后绘制，此时深度缓冲已写满全部墙体，
    //   ZTest Greater 才能可靠判断"是否被墙挡"；放 Opaque 队列会受不透明排序影响（大墙体中心比角色远时漏判）。
    // 为什么单 Pass：URP 渲染循环每个物体每个 ShaderTag 只跑一个 Pass，经典「遮罩+反向壳描边环」两 Pass
    //   方案在 URP 需要自定义 RendererFeature 跑两次 DrawRenderers；这里走单 Pass 以便直接当额外材质挂上即用。
    //
    // 用法见文件末尾注释。支持 skinned mesh（顶点蒙皮由 URP 头文件处理，和 CharacterHitFlash 一致）。
    Properties
    {
        [HDR] _OutlineColor ("描边颜色 (HDR)",        Color)        = (0.2, 0.8, 1.0, 1.0)
        _RimPower           ("边缘锐度 (越大越细)",    Range(0.2, 12)) = 3.0
        _Intensity          ("强度",                   Range(0, 8))   = 1.5
        // 0 = 纯边缘描边（中间透明）；1 = 实心剪影。中间值在两者间过渡。
        _FillStrength       ("填充 (0=描边 1=实心)",   Range(0, 1))   = 0.0
        // 可选：把轮廓沿法线略微外扩，描边更"包住"角色（屏幕空间近似，0 关闭）。
        _OutlineWidth       ("外扩宽度 (米)",          Range(0, 0.1)) = 0.0
    }

    SubShader
    {
        // Transparent 队列：在所有不透明物之后绘制，深度缓冲已完整 → 遮挡判定可靠。
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" }

        Pass
        {
            Name "OccludedOutline"
            // UniversalForward 标签 → 被 URP 透明物绘制阶段拾取
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZTest Greater   // ★ 只在"被前方物体挡住"的片元绘制 —— 没墙挡时整体不出现
            ZWrite Off
            Blend SrcAlpha One   // 加色混合：不依赖后处理 Bloom 也能看出发光描边

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            // skinned mesh 顶点蒙皮（URP 标准顶点输入即可，无需额外宏）

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _RimPower;
                float  _Intensity;
                float  _FillStrength;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = normalize(TransformObjectToWorldNormal(IN.normalOS));

                // 沿世界法线略微外扩（让描边稍微包住身体；_OutlineWidth=0 时无影响）
                positionWS += normalWS * _OutlineWidth;

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS    = normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(positionWS); // 片元 → 相机
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);

                // Fresnel：正面 dot≈1 → rim≈0（透明）；侧面/轮廓 dot≈0 → rim≈1（发光）
                float fresnel = pow(saturate(1.0 - saturate(dot(n, v))), _RimPower);

                // _FillStrength 在"纯边缘描边"和"实心剪影"之间插值
                float mask = lerp(fresnel, 1.0, saturate(_FillStrength));

                float alpha = saturate(mask) * _Intensity;
                // Blend SrcAlpha One：最终 = rgb*alpha + dst，故 rgb 直接给颜色、alpha 控制权重
                return half4(_OutlineColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

// ─────────────────────────────────────────────────────────────────────────────
// 接入方式（任选其一）
//
// 【方式 A·最快】额外材质槽
//   1. 新建材质 → shader 选 Custom/OccludedOutline。
//   2. 把该材质**追加**到角色 SkinnedMeshRenderer 的 Materials 列表末尾（原材质保留）。
//   说明：单 submesh 的角色网格整体生效；多 submesh 时额外材质只覆盖最后一个 submesh
//        （Unity 规则：materials 多于 subMesh 数时，多出的材质重绘最后一个 submesh）。
//        本项目角色由同一 prefab 生成（玩家/僵尸共用），在 prefab 上加一次即可全体生效。
//
// 【方式 B·健壮·覆盖全身多 submesh】URP RenderObjects RendererFeature（无需写 C#）
//   1. 把角色放到一个独立 Layer（如新建 "Character"）。
//   2. URP Renderer 资产 → Add Renderer Feature → Render Objects：
//        Event = AfterRenderingOpaques，LayerMask = Character，
//        Overrides → Material = 上面的 OccludedOutline 材质，Depth → Test = Greater。
//   这样会用描边材质重绘整层角色的所有 submesh，深度测试针对完整不透明深度，最稳。
//
// 调参：_FillStrength=0 细描边 / 调高变实心剪影；_RimPower 越大描边越细；
//      想叠 Bloom 把 _OutlineColor 开成 HDR（>1）。
// ─────────────────────────────────────────────────────────────────────────────
