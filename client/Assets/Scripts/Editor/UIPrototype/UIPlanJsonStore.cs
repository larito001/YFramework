using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;

public sealed class UIPlanJsonStore
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    public UIViewPlanInfo LoadOrCreate(string prefabAssetPath)
    {
        string planPath = UIPrototypePathUtility.GetPlanPathForPrefab(prefabAssetPath);
        UIViewPlanInfo planInfo = null;

        if (File.Exists(UIPrototypePathUtility.AssetPathToFullPath(planPath)))
        {
            string json = File.ReadAllText(UIPrototypePathUtility.AssetPathToFullPath(planPath), Encoding.UTF8);
            planInfo = JsonConvert.DeserializeObject<UIViewPlanInfo>(json);
        }

        if (planInfo == null)
        {
            planInfo = new UIViewPlanInfo();
        }

        EnsureDefaults(planInfo, prefabAssetPath);
        return planInfo;
    }

    public void Save(string prefabAssetPath, UIViewPlanInfo planInfo)
    {
        EnsureDefaults(planInfo, prefabAssetPath);
        string planPath = UIPrototypePathUtility.GetPlanPathForPrefab(prefabAssetPath);
        string fullPath = UIPrototypePathUtility.AssetPathToFullPath(planPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        string json = JsonConvert.SerializeObject(planInfo, Formatting.Indented);
        File.WriteAllText(fullPath, json, Encoding.UTF8);
        AssetDatabase.ImportAsset(planPath);
    }

    public UIElementPlanInfo GetOrCreateElement(UIViewPlanInfo planInfo, string path)
    {
        if (planInfo.elements == null)
        {
            planInfo.elements = new Dictionary<string, UIElementPlanInfo>();
        }

        if (!planInfo.elements.TryGetValue(path, out var elementPlan) || elementPlan == null)
        {
            elementPlan = new UIElementPlanInfo();
            planInfo.elements[path] = elementPlan;
        }

        return elementPlan;
    }

    public void SyncWithScan(UIViewPlanInfo planInfo, UIViewScanResult scanResult)
    {
        if (planInfo.elements == null)
        {
            planInfo.elements = new Dictionary<string, UIElementPlanInfo>();
        }

        if (planInfo.deletedElements == null)
        {
            planInfo.deletedElements = new List<UIDeletedElementInfo>();
        }

        HashSet<string> currentPaths = new HashSet<string>();
        for (int i = 0; i < scanResult.Elements.Count; i++)
        {
            UIElementScanInfo element = scanResult.Elements[i];
            currentPaths.Add(element.Path);
            GetOrCreateElement(planInfo, element.Path);
            RemoveDeletedRecord(planInfo, element.Path);
        }

        List<string> knownPaths = new List<string>(planInfo.elements.Keys);
        for (int i = 0; i < knownPaths.Count; i++)
        {
            string path = knownPaths[i];
            if (currentPaths.Contains(path))
            {
                continue;
            }

            UIElementPlanInfo elementPlan = planInfo.elements[path];
            if (!HasDeletedRecord(planInfo, path))
            {
                planInfo.deletedElements.Add(new UIDeletedElementInfo
                {
                    elementId = GetElementIdFromPath(path),
                    path = path,
                    elementType = "Unknown",
                    plannerComment = elementPlan == null ? string.Empty : elementPlan.plannerComment,
                    exportToDesignDoc = elementPlan == null || elementPlan.exportToDesignDoc,
                    deletedAtVersion = Math.Max(1, planInfo.version + 1)
                });
            }

            planInfo.elements.Remove(path);
        }
    }

    public void ClearDeletedElements(UIViewPlanInfo planInfo)
    {
        if (planInfo.deletedElements == null)
        {
            planInfo.deletedElements = new List<UIDeletedElementInfo>();
            return;
        }

        planInfo.deletedElements.Clear();
    }

    public void Touch(UIViewPlanInfo planInfo)
    {
        planInfo.version = Math.Max(1, planInfo.version + 1);
        planInfo.lastModifiedTime = DateTime.Now.ToString(TimeFormat);
    }

    private static void EnsureDefaults(UIViewPlanInfo planInfo, string prefabAssetPath)
    {
        string viewId = Path.GetFileNameWithoutExtension(prefabAssetPath);
        if (string.IsNullOrEmpty(planInfo.viewId))
        {
            planInfo.viewId = viewId;
        }

        if (string.IsNullOrEmpty(planInfo.viewName))
        {
            planInfo.viewName = viewId;
        }

        if (planInfo.version <= 0)
        {
            planInfo.version = 1;
        }

        if (string.IsNullOrEmpty(planInfo.lastModifiedTime))
        {
            planInfo.lastModifiedTime = DateTime.Now.ToString(TimeFormat);
        }

        if (planInfo.elements == null)
        {
            planInfo.elements = new Dictionary<string, UIElementPlanInfo>();
        }

        if (planInfo.deletedElements == null)
        {
            planInfo.deletedElements = new List<UIDeletedElementInfo>();
        }
    }

    private static bool HasDeletedRecord(UIViewPlanInfo planInfo, string path)
    {
        for (int i = 0; i < planInfo.deletedElements.Count; i++)
        {
            if (planInfo.deletedElements[i].path == path)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveDeletedRecord(UIViewPlanInfo planInfo, string path)
    {
        for (int i = planInfo.deletedElements.Count - 1; i >= 0; i--)
        {
            if (planInfo.deletedElements[i].path == path)
            {
                planInfo.deletedElements.RemoveAt(i);
            }
        }
    }

    private static string GetElementIdFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        int slashIndex = path.LastIndexOf('/');
        return slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;
    }
}
