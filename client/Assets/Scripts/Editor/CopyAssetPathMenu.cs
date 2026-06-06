#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project 窗口右键菜单「复制路径」:把选中资源的路径复制到系统剪贴板,支持多选(每行一个)。
///   · 相对路径(工程内)：形如 <c>Assets/Art/UI/xxx.png</c>,可直接粘进代码用于 AssetDatabase.LoadAssetAtPath。
///   · 绝对路径(磁盘)  ：形如 <c>C:/UnityProject/.../xxx.png</c>(已把反斜杠统一成正斜杠)。
/// 复制结果同时打到 Console 方便回看。菜单也带快捷键:Alt+Shift+C 复制相对路径。
/// </summary>
public static class CopyAssetPathMenu
{
    private const string MenuRelative = "Assets/复制路径/相对路径(工程内) &#c";
    private const string MenuAbsolute = "Assets/复制路径/绝对路径(磁盘)";

    [MenuItem(MenuRelative, false, 19)]
    private static void CopyRelative() => CopyPaths(absolute: false);

    [MenuItem(MenuAbsolute, false, 20)]
    private static void CopyAbsolute() => CopyPaths(absolute: true);

    // 仅当在 Project 窗口选中了资源时才可点。
    [MenuItem(MenuRelative, true)]
    [MenuItem(MenuAbsolute, true)]
    private static bool Validate() => Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;

    private static void CopyPaths(bool absolute)
    {
        var sb = new StringBuilder();
        foreach (var guid in Selection.assetGUIDs)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) continue;
            // assetPath 相对工程根目录;Unity 运行时工作目录即工程根,GetFullPath 可解析出绝对路径。
            var path = absolute ? Path.GetFullPath(assetPath).Replace('\\', '/') : assetPath;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(path);
        }

        if (sb.Length == 0) return;
        var result = sb.ToString();
        EditorGUIUtility.systemCopyBuffer = result;
        Debug.Log($"[CopyAssetPath] 已复制{(absolute ? "绝对" : "相对")}路径到剪贴板:\n{result}");
    }
}
#endif
