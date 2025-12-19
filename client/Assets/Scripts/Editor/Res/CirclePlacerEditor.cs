using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 圆形标记放置编辑器
/// - Handles 绘制
/// - 右键删除
/// - 支持 ItemData 选择并写入 ItemId
/// </summary>
public class CirclePlacerEditor : EditorWindow
{
    private bool isEnabled;
    private GameObject resRoot;

    private const string ResRootName = "ResRoot";
    private const string RedTag = "Red";

    private const float CircleRadius = 0.5f;

    // ===== Item Data =====
    private ItemDataSO itemDataSO;
    private int selectedIndex;
    private string[] itemOptions;

    [MenuItem("Tools/Circle Placer (Editor Mark)")]
    public static void Open()
    {
        GetWindow<CirclePlacerEditor>("Circle Placer");
    }

    private void OnEnable()
    {
        // 自动尝试加载（可删）
        itemDataSO = Resources.Load<ItemDataSO>("Config/ItemsData");

        RefreshItemOptions();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField("Item 配置", EditorStyles.boldLabel);

        ItemDataSO newSO = (ItemDataSO)EditorGUILayout.ObjectField(
            "Item Data SO",
            itemDataSO,
            typeof(ItemDataSO),
            false);

        if (newSO != itemDataSO)
        {
            itemDataSO = newSO;
            selectedIndex = 0;
            RefreshItemOptions();
        }

        if (itemDataSO == null || itemOptions == null || itemOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("请配置 ItemDataSO，且 Item 列表不能为空", MessageType.Warning);
            return;
        }

        selectedIndex = EditorGUILayout.Popup("当前 Item", selectedIndex, itemOptions);

        GUILayout.Space(10);

        if (!isEnabled)
        {
            if (GUILayout.Button("开启", GUILayout.Height(30)))
                Enable();
        }
        else
        {
            if (GUILayout.Button("关闭", GUILayout.Height(30)))
                Disable();
        }
    }

    private void Enable()
    {
        isEnabled = true;
        SceneView.duringSceneGui += OnSceneGUI;

        resRoot = GameObject.Find(ResRootName);
        if (resRoot == null)
            resRoot = new GameObject(ResRootName);
    }

    private void Disable()
    {
        isEnabled = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        if (isEnabled)
            Disable();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        DrawAndHandleMarkers(e);

        // 左键创建
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                CreateMarker(hit.point);
                e.Use();
            }
        }
    }

    private void CreateMarker(Vector3 position)
    {
        GameObject marker = new GameObject("CircleMark");
        marker.transform.position = position;
        marker.transform.SetParent(resRoot.transform);
        marker.tag = RedTag;

        CircleItemMarker itemMarker = marker.AddComponent<CircleItemMarker>();
        itemMarker.itemId = itemDataSO.ItemDatas[selectedIndex].Id;

        Undo.RegisterCreatedObjectUndo(marker, "Create Circle Marker");
    }

    private void DrawAndHandleMarkers(Event e)
    {
        if (resRoot == null)
            return;

        Handles.color = Color.red;

        List<Transform> toRemove = null;

        foreach (Transform child in resRoot.transform)
        {
            if (!child.CompareTag(RedTag))
                continue;

            Vector3 pos = child.position;

            Handles.DrawSolidDisc(pos, Vector3.up, CircleRadius);

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                float dist = HandleUtility.DistanceToCircle(pos, CircleRadius);
                if (dist == 0f)
                {
                    if (toRemove == null)
                        toRemove = new List<Transform>();

                    toRemove.Add(child);
                    e.Use();
                }
            }
        }

        if (toRemove != null)
        {
            foreach (var t in toRemove)
                Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    private void RefreshItemOptions()
    {
        if (itemDataSO == null || itemDataSO.ItemDatas == null)
        {
            itemOptions = null;
            return;
        }

        var items = itemDataSO.ItemDatas;
        itemOptions = new string[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            itemOptions[i] = $"Id: {items[i].Id}";
        }
    }
}
