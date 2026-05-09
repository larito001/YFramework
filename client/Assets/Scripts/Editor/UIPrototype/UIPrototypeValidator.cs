using System.Collections.Generic;
using UnityEngine;

public sealed class UIPrototypeValidator
{
    public List<UIValidationIssue> Validate(UIViewScanResult scanResult)
    {
        List<UIValidationIssue> issues = new List<UIValidationIssue>();
        if (scanResult == null)
        {
            issues.Add(new UIValidationIssue(UIValidationSeverity.Error, "没有可校验的扫描结果。"));
            return issues;
        }

        issues.AddRange(scanResult.Issues);

        for (int i = 0; i < scanResult.Elements.Count; i++)
        {
            UIElementScanInfo element = scanResult.Elements[i];
            if (string.IsNullOrEmpty(element.ElementId) ||
                element.ElementId == "GameObject" ||
                element.ElementId.StartsWith("New "))
            {
                issues.Add(new UIValidationIssue(
                    UIValidationSeverity.Warning,
                    "GameObject 名称为空或仍是默认名，建议重命名为稳定 ElementId。",
                    element.Path,
                    element.Element));
            }

            if (element.ExportToDesignDoc && string.IsNullOrEmpty(element.PlannerComment))
            {
                issues.Add(new UIValidationIssue(
                    UIValidationSeverity.Warning,
                    "导出元素 Planner Comment 为空，建议补充策划含义。",
                    element.Path,
                    element.Element));
            }

            if (element.ElementType == "Image" &&
                string.IsNullOrEmpty(element.PlannerComment))
            {
                issues.Add(new UIValidationIssue(
                    UIValidationSeverity.Warning,
                    "图片类元素建议说明用途和是否需要美术出图。",
                    element.Path,
                    element.Element));
            }

            if (element.ElementType == "ScrollView" &&
                element.Element != null &&
                !HasLikelyItemTemplate(element.Element.transform))
            {
                issues.Add(new UIValidationIssue(
                    UIValidationSeverity.Warning,
                    "ScrollView 未发现明显 Item 模板，建议补充列表项结构。",
                    element.Path,
                    element.Element));
            }
        }

        return issues;
    }

    private static bool HasLikelyItemTemplate(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string lowerName = children[i].name.ToLowerInvariant();
            if (lowerName.Contains("item") || lowerName.Contains("cell"))
            {
                return true;
            }
        }

        return false;
    }
}
