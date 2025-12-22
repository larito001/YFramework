using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class EnemyPlacerEditor : EditorWindow
{
    private bool isEnabled;
    private GameObject resRoot;

    private const string ResRootName = "EnemyRoot";
    private const float CircleRadius = 0.5f;

    // ===== Item =====
    private EnemyGroupSO enemyGroupSo;
    private int selectedIndex;
    private string[] itemOptions;

    private bool showLabel = true;

    [MenuItem("Tools/EnemyPlacer Placer")]
    public static void Open()
    {
        GetWindow<EnemyPlacerEditor>("Enmey Placer");
    }

    private void OnEnable()
    {
        // 自动尝试加载（可删）
        enemyGroupSo = Resources.Load<EnemyGroupSO>("Config/EnemyGroupSO");
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
        EditorGUILayout.LabelField("enemyGroup 配置", EditorStyles.boldLabel);

        EnemyGroupSO newSO = (EnemyGroupSO)EditorGUILayout.ObjectField(
            "enemyGroupSo",
            enemyGroupSo,
            typeof(EnemyGroupSO),
            false);

        if (newSO != enemyGroupSo)
        {
            enemyGroupSo = newSO;
            selectedIndex = 0;
            RefreshItemOptions();
            SceneView.RepaintAll();
        }

        if (enemyGroupSo == null || itemOptions == null || itemOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("请配置 enemyGroupSo", MessageType.Warning);
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
        if (resRoot == null || enemyGroupSo == null)
            return;

        Event e = Event.current;

        List<Transform> toRemove = null;

        // ===== 先画 Handles =====
        foreach (Transform child in resRoot.transform)
        {
            var data = enemyGroupSo.EnemyGroupDatas.Find(i => i.id ==int.Parse(child.name));
            if (data == null)
                continue;

            Vector3 pos = child.position;

            Handles.color = Color.red;
            Handles.DrawSolidDisc(pos, Vector3.up, CircleRadius);

            if (showLabel)
            {
                Handles.Label(pos + Vector3.up * 0.3f, $"Id:{child.name}");
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
        GameObject go = new GameObject();
        go.transform.position = position;
        go.transform.SetParent(resRoot.transform);
        go.name = enemyGroupSo.EnemyGroupDatas[selectedIndex].id.ToString();
        Undo.RegisterCreatedObjectUndo(go, "Create Enemy Marker");
        SceneView.RepaintAll();
    }

    private void RefreshItemOptions()
    {
        if (enemyGroupSo == null)
        {
            itemOptions = null;
            return;
        }

        var items = enemyGroupSo.EnemyGroupDatas;
        itemOptions = new string[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            itemOptions[i] = $"Id:{items[i].id}名称:{items[i].name}";
        }
    }


}
