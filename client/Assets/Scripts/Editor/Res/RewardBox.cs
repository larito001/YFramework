using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class RewardBox : EditorWindow
{
    private bool isEnabled;
    private GameObject resRoot;

    private const string ResRootName = "ResRoot";
    private const float CircleRadius = 0.5f;

    // ===== Item =====
    private RewardBoxDataSO itemDataSO;
    private int selectedIndex;
    private string[] itemOptions;

    private bool showLabel = true;

    [MenuItem("Tools/保险摆放")]
    public static void Open()
    {
        GetWindow<RewardBox>("RewardBoxTool");
    }

    private void OnEnable()
    {
        // 自动尝试加载（可删）
        itemDataSO = Resources.Load<RewardBoxDataSO>("Config/RewardBoxDataSO");
        // 关键 1：自动启用 Scene 绘制
        Enable();

        RefreshItemOptions();

        // 关键 2：强制 Scene 重绘
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        Disable();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Item 配置", EditorStyles.boldLabel);


        RewardBoxDataSO newSO = (RewardBoxDataSO)EditorGUILayout.ObjectField(
            "RewardBoxDataSO",
            itemDataSO,
            typeof(RewardBoxDataSO),
            false);

        if (newSO != itemDataSO)
        {
            itemDataSO = newSO;
            selectedIndex = 0;
            RefreshItemOptions();
            SceneView.RepaintAll();
        }

        if (itemDataSO == null || itemOptions == null || itemOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("请配置 ItemDataSO", MessageType.Warning);
            return;
        }

        selectedIndex = EditorGUILayout.Popup("当前 Item", selectedIndex, itemOptions);
        showLabel = EditorGUILayout.Toggle("显示 ItemId", showLabel);
    }

    private void Enable()
    {
        if (isEnabled)
            return;

        isEnabled = true;
        SceneView.duringSceneGui += OnSceneGUI;

        resRoot = GameObject.Find(ResRootName);
        if (resRoot == null)
            resRoot = new GameObject(ResRootName);
    }

    private void Disable()
    {
        if (!isEnabled)
            return;

        isEnabled = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (resRoot == null || itemDataSO == null)
            return;

        Event e = Event.current;

        List<Transform> toRemove = null;

        // ===== 先画 Handles =====
        foreach (Transform child in resRoot.transform)
        {
            CircleItemMarker marker = child.GetComponent<CircleItemMarker>();
            if (marker == null)
                continue;

            RewardBoxData data = itemDataSO.rewardDatas.Find(i => i.RewardId == marker.itemId);
            if (data == null)
                continue;

            Vector3 pos = child.position;

            Handles.color = GetColorByItemType(data.RewardId);
            Handles.DrawSolidDisc(pos, Vector3.up, CircleRadius);

            if (showLabel)
            {
                Handles.Label(pos + Vector3.up * 0.3f, $"Id:{data.RewardId}");
            }

            // 右键删除
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                if (HandleUtility.DistanceToCircle(pos, CircleRadius) == 0f)
                {
                    toRemove ??= new List<Transform>();
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
        GameObject go = new GameObject($"CircleMark_Item_{itemDataSO.rewardDatas[selectedIndex].RewardId}");
        go.transform.position = position;
        go.transform.SetParent(resRoot.transform);

        var marker = go.AddComponent<CircleItemMarker>();
        marker.itemId = itemDataSO.rewardDatas[selectedIndex].RewardId;
        Undo.RegisterCreatedObjectUndo(go, "Create Circle Marker");
        SceneView.RepaintAll();
    }

    private void RefreshItemOptions()
    {
        if (itemDataSO == null)
        {
            itemOptions = null;
            return;
        }

        var items = itemDataSO.rewardDatas;
        itemOptions = new string[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            itemOptions[i] = $"Id:{items[i].RewardId}";
        }
    }

    private Color GetColorByItemType(int type)
    {
        return  Color.red;
    }
}
