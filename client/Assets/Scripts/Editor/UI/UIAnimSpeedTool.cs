#if UNITY_EDITOR
using DG.Tweening;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把工程内所有带 <see cref="YOTOUIShow"/> 的预制体的出现/消失动画时长统一刷成更干脆的值。
/// 因为时长是每个预制体序列化保存的,改类默认值只影响"以后新建"的;已有预制体要靠这个工具一次性批量改。
///
/// 菜单:Tools/UI/Apply UI Anim Speed (all prefabs)
/// </summary>
public static class UIAnimSpeedTool
{
    // 与 YOTOUIShow 的新默认值保持一致。
    private const float EnterDuration = 0.18f;
    private const float ExitDuration = 0.1f;
    private const Ease EnterEase = Ease.OutCubic;
    private const Ease ExitEase = Ease.OutQuad;

    [MenuItem("Tools/UI/Apply UI Anim Speed (all prefabs)")]
    public static void Apply()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int changed = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            var shows = root.GetComponentsInChildren<YOTOUIShow>(true);
            if (shows.Length == 0) continue;

            foreach (var s in shows)
            {
                s.useEnterAnim = true;
                s.enterDuration = EnterDuration;
                s.enterEase = EnterEase;
                s.useExitAnim = true;
                s.exitDuration = ExitDuration;
                s.exitEase = ExitEase;
            }
            EditorUtility.SetDirty(root);
            PrefabUtility.SavePrefabAsset(root);
            changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[UIAnimSpeedTool] 已更新 {changed} 个含 YOTOUIShow 的预制体:进入 {EnterDuration}s / 退出 {ExitDuration}s。");
    }
}
#endif
