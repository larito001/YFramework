using UnityEditor;

[InitializeOnLoad]
public static class UIPrototypeCodeBinderQueue
{
    private const string KeyPrefab = "UIPrototype.PendingBind.Prefab";
    private const string KeyScript = "UIPrototype.PendingBind.Script";

    static UIPrototypeCodeBinderQueue()
    {
        EditorApplication.delayCall += TryBind;
    }

    public static void Enqueue(string prefabAssetPath, string scriptAssetPath)
    {
        SessionState.SetString(KeyPrefab, prefabAssetPath);
        SessionState.SetString(KeyScript, scriptAssetPath);
        EditorApplication.delayCall += TryBind;
    }

    private static void TryBind()
    {
        string prefabPath = SessionState.GetString(KeyPrefab, null);
        string scriptPath = SessionState.GetString(KeyScript, null);
        if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(scriptPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryBind;
            return;
        }

        MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
        if (ms == null || ms.GetClass() == null)
        {
            // 类还没编出来；静态构造函数会在下次 assembly reload 后再触发 TryBind
            return;
        }

        SessionState.EraseString(KeyPrefab);
        SessionState.EraseString(KeyScript);
        UICodeBinder.AttachAndBind(prefabPath, scriptPath);
    }
}
