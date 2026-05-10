#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class BagPanelPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/BagPanel.prefab";

    [MenuItem("YFramework/Build Prefabs/BagPanel")]
    public static void Build()
    {
        var root = PrefabBuilderHelpers.CreateRoot("BagPanel");
        root.AddComponent<CanvasGroup>();
        root.AddComponent<YOTOUIShow>();
        var panel = root.AddComponent<BagPanel>();

        // panelRect : RectTransform → 直接回填 root 的 RectTransform
        panel.panelRect = root.GetComponent<RectTransform>();

        // dragLayer : RectTransform → 子节点 DragLayer，用于拖拽时 reparent
        var dragLayerGO = PrefabBuilderHelpers.CreateChildGO(root.transform, "DragLayer");
        var dragLayerRt = dragLayerGO.GetComponent<RectTransform>();
        dragLayerRt.anchorMin = Vector2.zero;
        dragLayerRt.anchorMax = Vector2.one;
        dragLayerRt.offsetMin = Vector2.zero;
        dragLayerRt.offsetMax = Vector2.zero;
        panel.dragLayer = dragLayerRt;

        // tooltip : BagTooltip → 子节点挂 BagTooltip 组件（其内部 iconImage / nameText / descText 需手工挂）
        var tooltipGO = PrefabBuilderHelpers.CreateChildWithComponent<BagTooltip>(root.transform, "Tooltip");
        panel.tooltip = tooltipGO.GetComponent<BagTooltip>();

        // btn_sort : Button
        var btnSortGO = PrefabBuilderHelpers.CreateButton(root.transform, "btn_sort", "整理");
        panel.btn_sort = btnSortGO.GetComponent<Button>();

        // btn_close : Button
        var btnCloseGO = PrefabBuilderHelpers.CreateButton(root.transform, "btn_close", "关闭");
        panel.btn_close = btnCloseGO.GetComponent<Button>();

        // slotItems : BagSlotItem[20] —— 规划 §1 固定 4×5 = 20 格，自动展开
        const int SlotCount = 20;
        var slotsRoot = PrefabBuilderHelpers.CreateChildGO(root.transform, "Slots");
        panel.slotItems = new BagSlotItem[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            var slotGO = PrefabBuilderHelpers.CreateChildGO(slotsRoot.transform, $"Slot_{i:D2}");
            var slotItem = slotGO.AddComponent<BagSlotItem>();
            slotItem.canvasGroup = slotGO.AddComponent<CanvasGroup>();

            var iconGO = PrefabBuilderHelpers.CreateImage(slotGO.transform, "Icon");
            slotItem.iconImage = iconGO.GetComponent<Image>();

            var countGO = PrefabBuilderHelpers.CreateTMP(slotGO.transform, "Count", "1");
            slotItem.countText = countGO.GetComponent<TextMeshProUGUI>();

            panel.slotItems[i] = slotItem;
        }

        // TODO: currencyRows : BagCurrencyRow[]
        //     规划 §6：货币种类由 item.xlsx (type==Currency) 决定，数量未定。请按需在 root 下建
        //     若干子节点挂 BagCurrencyRow，再拖入 currencyRows 数组。BagCurrencyRow 内部
        //     (iconImage / countText) 需手挂。

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
