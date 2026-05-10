#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class PrefabBuilderHelpers
{
    public static GameObject CreateRoot(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    public static GameObject CreateChildGO(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    public static GameObject CreateChildCanvasGroup(Transform parent, string name)
    {
        var go = CreateChildGO(parent, name);
        go.AddComponent<CanvasGroup>();
        return go;
    }

    public static GameObject CreateImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    public static GameObject CreateRawImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    public static GameObject CreateTMP(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24;
        return go;
    }

    public static GameObject CreateText(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    public static GameObject CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 40);
        var labelGO = CreateTMP(go.transform, "Label", label);
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        return go;
    }

    public static GameObject CreateChildWithComponent<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<T>();
        return go;
    }

    public static void EnsureAssetDir(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir)) return;
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
    }

    public static GameObject SavePrefab(GameObject root, string assetPath, bool overwrite = true)
    {
        EnsureAssetDir(assetPath);
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
        {
            Debug.LogWarning($"[PrefabBuilder] {assetPath} 已存在且 overwrite=false，跳过保存。");
            return null;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        Debug.Log($"[PrefabBuilder] Saved {assetPath}");
        return prefab;
    }
}
#endif
