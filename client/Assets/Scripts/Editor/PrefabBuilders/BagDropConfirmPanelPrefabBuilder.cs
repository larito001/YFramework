#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class BagDropConfirmPanelPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/BagDropConfirmPanel.prefab";

    [MenuItem("YFramework/Build Prefabs/BagDropConfirmPanel")]
    public static void Build()
    {
        var root = PrefabBuilderHelpers.CreateRoot("BagDropConfirmPanel");
        root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();

        // Top 层模态：根节点加全屏透明 Image (raycastTarget=true) 阻断下层输入（规划 §9）
        var blocker = root.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.5f);
        blocker.raycastTarget = true;

        var panel = root.AddComponent<BagDropConfirmPanel>();

        // txt_message : TextMeshProUGUI
        var txtGO = PrefabBuilderHelpers.CreateTMP(root.transform, "txt_message", "确认丢弃 ?");
        panel.txt_message = txtGO.GetComponent<TextMeshProUGUI>();

        // btn_confirm : Button
        var btnConfirmGO = PrefabBuilderHelpers.CreateButton(root.transform, "btn_confirm", "确认");
        panel.btn_confirm = btnConfirmGO.GetComponent<Button>();

        // btn_cancel : Button
        var btnCancelGO = PrefabBuilderHelpers.CreateButton(root.transform, "btn_cancel", "取消");
        panel.btn_cancel = btnCancelGO.GetComponent<Button>();

        var prefab = PrefabBuilderHelpers.SavePrefab(root, PrefabPath, overwrite: true);
        Object.DestroyImmediate(root);
        if (prefab != null)
        {
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }
    }
}
#endif
