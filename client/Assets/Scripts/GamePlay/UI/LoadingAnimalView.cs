using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 加载页的「狼跑动」动画:用一台只拍 <see cref="PreviewLayer"/> 层的离屏相机,把 Resources 里的狼模型渲染到本物体的
/// <see cref="RawImage"/> 上,并让它一直播行走/奔跑动画(原地跑),当作加载指示。
/// 加载/切场景时常 timeScale=0,所以 Animator 用 <see cref="AnimatorUpdateMode.UnscaledTime"/>、自转用 unscaledDeltaTime,
/// 保证 timeScale=0 也照动。由 <see cref="LoadingPanel"/> 在 OnShow/OnHide 调 <see cref="Play"/>/<see cref="Stop"/>。
/// 狼预制体缺失(未生成)时静默退化:不显示,加载页仍有转圈/文字。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class LoadingAnimalView : MonoBehaviour
{
    [Tooltip("要展示的动物预制体(Resources 路径)")]
    public string animalPath = "Animals/Wolf";
    [Tooltip("缓慢自转速度(度/秒,unscaled);0=不转,固定角度原地跑(默认)")]
    public float yawSpeed = 0f;

    private const int PreviewLayer = 18;                                  // 复用 ProjectSettings 的 WeaponPreview 层
    private static readonly Vector3 RigOrigin = new Vector3(7000f, 7000f, 0f); // 远离场景,避免互相入镜
    // 直接播放的「奔跑/行走」状态名(按优先级;狼的控制器里是 Running / Walk)
    private static readonly string[] LocomotionStates = { "Running", "Run", "Walk", "Walking", "Gallop", "Trot", "Move" };

    private RawImage image;
    private RenderTexture rt;
    private Camera cam;
    private GameObject rig;
    private Transform pivot;
    private GameObject model;
    private Animator animator;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
    }

    /// <summary>开始展示并奔跑(懒建离屏 rig)。</summary>
    public void Play()
    {
        EnsureRig();
        if (rig != null) rig.SetActive(true);
        ApplyRun(); // 每次显示都重设奔跑——rig 被 SetActive(false) 关过后 Animator 会回到默认(站立)状态
        if (image != null) image.enabled = model != null;
    }

    /// <summary>停止渲染(收起加载页时调,省开销)。</summary>
    public void Stop()
    {
        if (rig != null) rig.SetActive(false);
        if (image != null) image.enabled = false;
    }

    private void Update()
    {
        if (pivot != null && yawSpeed != 0f)
            pivot.Rotate(0f, yawSpeed * Time.unscaledDeltaTime, 0f); // unscaled:timeScale=0 也转
    }

    private void EnsureRig()
    {
        if (rt == null)
        {
            rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "LoadingAnimalRT" };
            rt.Create();
        }
        if (image != null) image.texture = rt;
        if (rig != null) return;

        rig = new GameObject("LoadingAnimalRig");
        rig.transform.position = RigOrigin;

        var pv = new GameObject("Pivot");
        pv.transform.SetParent(rig.transform, false);
        pivot = pv.transform;

        var camGo = new GameObject("Cam");
        camGo.transform.SetParent(rig.transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透明,叠在加载页深色底上
        cam.cullingMask = 1 << PreviewLayer;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.targetTexture = rt;

        var prefab = Resources.Load<GameObject>(animalPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[LoadingAnimalView] 找不到动物预制体(确认已在 Resources/ 下并已生成): {animalPath}");
            return;
        }

        model = Object.Instantiate(prefab, pivot);
        WeaponModelUtil.SetLayer(model, PreviewLayer);
        WeaponModelUtil.DisableColliders(model);
        // 动物身上的弱点高亮/命中盒不该在加载页显示
        foreach (var hz in model.GetComponentsInChildren<AnimalHitZone>(true)) hz.gameObject.SetActive(false);
        WeaponModelUtil.MakeUnlit(model);     // 离屏层没灯,换 Unlit 才看得清
        WeaponModelUtil.Frame(cam, pivot, model); // 先用通用取景把模型居中到 pivot
        FrameSide(model);                          // 再把相机摆到正侧面,看清奔跑步态

        animator = model.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // 只被离屏相机渲染时,主相机看不到它 → 默认culling会判定"不可见"而停掉动画。强制一直播。
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime; // timeScale=0(加载中)也播
        }
    }

    /// <summary>把相机摆到动物正侧面(垂直于体长轴看其侧腹),略微抬高俯一点,看清奔跑步态。</summary>
    private void FrameSide(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        float radius = Mathf.Max(b.extents.magnitude, 0.05f);
        float dist = radius / Mathf.Sin(Mathf.Deg2Rad * cam.fieldOfView * 0.5f) * 1.15f;

        // 侧面 = 沿"较短的水平轴"看(体长轴是较长的那个;相机站在它侧边)。用负号站到另一侧,头朝向看着顺。
        Vector3 dir = (b.size.z >= b.size.x) ? new Vector3(-1f, 0.12f, 0f) : new Vector3(0f, 0.12f, -1f);
        dir = dir.normalized;

        cam.transform.position = b.center + dir * dist;
        cam.transform.LookAt(b.center);
    }

    /// <summary>设置奔跑:把跑/走 bool 置 true 并直接进奔跑状态。每次显示都要调——rig 关掉重开后 Animator 会重置回默认站立态。</summary>
    private void ApplyRun()
    {
        if (animator == null) return;
        SetBoolIfExists(animator, "isRunning", true);
        SetBoolIfExists(animator, "isWalking", true);
        SetBoolIfExists(animator, "isJumping", true);
        PlayLocomotion(animator);
    }

    /// <summary>若该 bool 参数存在则设置(避免给不存在的参数赋值报错刷屏)。</summary>
    private static void SetBoolIfExists(Animator a, string name, bool value)
    {
        if (a.runtimeAnimatorController == null) return;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
            {
                a.SetBool(name, value);
                return;
            }
    }

    /// <summary>直接 Play 第一个存在的「奔跑/行走」状态(绕过默认状态没有条件转移导致一直站着的情况)。</summary>
    private static void PlayLocomotion(Animator a)
    {
        foreach (var s in LocomotionStates)
        {
            int h = Animator.StringToHash(s);
            if (a.HasState(0, h)) { a.Play(h, 0, 0f); return; }
        }
    }

    private void OnDestroy()
    {
        if (rig != null) { Object.Destroy(rig); rig = null; cam = null; pivot = null; model = null; }
        if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
    }
}
