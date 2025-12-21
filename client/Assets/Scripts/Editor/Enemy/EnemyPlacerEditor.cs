using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class EnemyPlacerEditor : EditorWindow
{
    private bool isEnabled;
    private GameObject resRoot;

    private const string ResRootName = "EnemyRoot";
    private const float CircleRadius = 0.5f;
    private EnemyActionType selectedActionIndex;
    private bool showLabel = true;
    [MenuItem("Tools/Enemy Placer (Editor Mark)")]
    public static void Open()
    {
        GetWindow<EnemyPlacerEditor>("Enemy Placer");
    }

    private void OnEnable()
    {
        // 关键 1：自动启用 Scene 绘制
        Enable();

        // 关键 2：强制 Scene 重绘
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        Disable();
    }

    private int enemyNumber = 5;

    private void OnGUI()
    {
        showLabel = EditorGUILayout.Toggle("显示 ItemId", showLabel);
        
        // 选择 EnemyActionType 枚举
        selectedActionIndex = (EnemyActionType)EditorGUILayout.EnumPopup(
            "敌人动作类型", 
           selectedActionIndex);
        enemyNumber = EditorGUILayout.IntSlider("敌人数量", enemyNumber, 0, 999);
        //todo:文本提示
        EditorGUILayout.HelpBox("动作行为名称|id|敌人数量|敌人动作类型", MessageType.Info);
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
        if (resRoot == null )
            return;

        Event e = Event.current;

        List<Transform> toRemove = null;

        // ===== 先画 Handles =====
        foreach (Transform child in resRoot.transform)
        {

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

        // ===== 再画 GUI（关键修复点）=====

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
        go.name = $"Enemy{(EnemyActionType)selectedActionIndex}|{resRoot.transform.childCount}|{enemyNumber}|{(int)selectedActionIndex}";
        go.transform.position = position;
        go.transform.SetParent(resRoot.transform);
        Undo.RegisterCreatedObjectUndo(go, "Create Enemy Marker");
        SceneView.RepaintAll();
    }
    
}
