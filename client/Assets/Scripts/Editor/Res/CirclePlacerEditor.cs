using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 编辑器标记工具（最终修正版）
/// - 不生成任何 Mesh / Renderer / Material
/// - 使用 Handles 绘制圆
/// - 使用 Handles.Button 实现无 Mesh 的可点击删除
/// </summary>
public class CirclePlacerEditor : EditorWindow
{
    private bool isEnabled;
    private GameObject resRoot;

    private const string ResRootName = "ResRoot";
    private const string RedTag = "Red"; // 需在 Tag Manager 中创建

    private const float CircleRadius = 0.5f;
    private const float PickSize = 0.15f; // 点击判定半径

    [MenuItem("Tools/Circle Placer (Editor Mark)")]
    public static void Open()
    {
        GetWindow<CirclePlacerEditor>("Circle Placer");
    }

    private void OnGUI()
    {
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

        // 左键：创建标记
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
    }

    /// <summary>
    /// 绘制并处理点击逻辑（右键删除）
    /// </summary>
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

            // 1. 只负责画
            Handles.DrawSolidDisc(pos, Vector3.up, CircleRadius);

            // 2. 右键命中检测（关键）
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                float dist = HandleUtility.DistanceToCircle(pos, CircleRadius);

                if (dist == 0f) // 命中
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
                DestroyImmediate(t.gameObject);
        }
    }

}
