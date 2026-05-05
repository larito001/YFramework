using System.IO;
using UnityEngine;

public static class UIPrototypePathUtility
{
    public const string ComponentLibraryPath = "Assets/Game/UIPrototypeComponents";
    public const string PrototypeRootPath = "Assets/Game/UIPrototypes";
    public const string DesignDocRootPath = "Docs/UIPlans";

    public static string GetPlanPathForPrefab(string prefabAssetPath)
    {
        string directory = Path.GetDirectoryName(prefabAssetPath);
        string fileName = Path.GetFileNameWithoutExtension(prefabAssetPath);
        return NormalizeAssetPath(Path.Combine(directory, fileName + ".plan.json"));
    }

    public static string GetDesignDocPath(string prefabAssetPath, UIViewPlanInfo planInfo)
    {
        string module = GetModuleName(prefabAssetPath);
        string viewId = string.IsNullOrEmpty(planInfo.viewId)
            ? Path.GetFileNameWithoutExtension(prefabAssetPath)
            : planInfo.viewId;
        return NormalizeAssetPath(Path.Combine(DesignDocRootPath, module, viewId + ".design.md"));
    }

    public static string GetScreenshotPath(string prefabAssetPath, UIViewPlanInfo planInfo)
    {
        string module = GetModuleName(prefabAssetPath);
        string viewId = string.IsNullOrEmpty(planInfo.viewId)
            ? Path.GetFileNameWithoutExtension(prefabAssetPath)
            : planInfo.viewId;
        return NormalizeAssetPath(Path.Combine(DesignDocRootPath, module, viewId + ".png"));
    }

    public static string AssetPathToFullPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    public static string NormalizeAssetPath(string path)
    {
        return path.Replace("\\", "/");
    }

    private static string GetModuleName(string prefabAssetPath)
    {
        string normalized = NormalizeAssetPath(prefabAssetPath);
        string prefix = PrototypeRootPath + "/";
        if (normalized.StartsWith(prefix))
        {
            string rest = normalized.Substring(prefix.Length);
            int slashIndex = rest.IndexOf('/');
            if (slashIndex > 0)
            {
                return rest.Substring(0, slashIndex);
            }
        }

        string directory = Path.GetDirectoryName(normalized);
        if (!string.IsNullOrEmpty(directory))
        {
            return new DirectoryInfo(directory).Name;
        }

        return "General";
    }
}
