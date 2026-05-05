using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIPrefabScanner
{
    public UIViewScanResult Scan(GameObject prefabRoot, UIViewPlanInfo planInfo = null)
    {
        UIViewScanResult result = new UIViewScanResult();
        if (prefabRoot == null)
        {
            result.Issues.Add(new UIValidationIssue(UIValidationSeverity.Error, "未选择 UI Prefab。"));
            return result;
        }

        result.ViewId = planInfo != null && !string.IsNullOrEmpty(planInfo.viewId) ? planInfo.viewId : prefabRoot.name;
        result.ViewName = planInfo != null && !string.IsNullOrEmpty(planInfo.viewName) ? planInfo.viewName : prefabRoot.name;

        Traverse(prefabRoot.transform, prefabRoot.transform, planInfo, result);
        AddDuplicateIdIssues(result);
        return result;
    }

    public static Type GetRecommendedSemanticType(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return null;
        }

        if (gameObject.GetComponent<Button>() != null || gameObject.GetComponent<YOTOButton>() != null)
        {
            return typeof(YButton);
        }

        if (gameObject.GetComponent<TMP_InputField>() != null || gameObject.GetComponent<InputField>() != null)
        {
            return typeof(YInput);
        }

        if (gameObject.GetComponent<Slider>() != null)
        {
            return typeof(YSlider);
        }

        if (gameObject.GetComponent<ScrollRect>() != null || gameObject.GetComponent<YOTOScrollView>() != null)
        {
            return typeof(YScrollView);
        }

        if (gameObject.GetComponent<TMP_Text>() != null || gameObject.GetComponent<Text>() != null)
        {
            return typeof(YText);
        }

        if (gameObject.GetComponent<Image>() != null || gameObject.GetComponent<RawImage>() != null)
        {
            return typeof(YImage);
        }

        return null;
    }

    public static string GetDefaultNamePrefix(string elementType)
    {
        switch (elementType)
        {
            case "Button":
                return "Btn_";
            case "Text":
                return "Txt_";
            case "Image":
                return "Img_";
            case "Slider":
                return "Sld_";
            case "ScrollView":
                return "List_";
            case "InputField":
                return "Input_";
            default:
                return "UI_";
        }
    }

    private static void Traverse(Transform current, Transform root, UIViewPlanInfo planInfo, UIViewScanResult result)
    {
        YUIElement element = current.GetComponent<YUIElement>();
        string path = GetPath(root, current);

        if (element != null)
        {
            UIElementPlanInfo elementPlan = null;
            if (planInfo != null && planInfo.elements != null)
            {
                planInfo.elements.TryGetValue(path, out elementPlan);
            }

            UIElementScanInfo info = new UIElementScanInfo
            {
                ElementId = current.gameObject.name,
                DisplayName = element.DisplayName,
                ElementType = element.ElementType,
                Path = path,
                ExportToDesignDoc = elementPlan == null || elementPlan.exportToDesignDoc,
                PlannerComment = elementPlan == null ? string.Empty : elementPlan.plannerComment,
                Element = element
            };

            foreach (string question in element.GetDesignQuestions())
            {
                if (!string.IsNullOrEmpty(question))
                {
                    info.DesignQuestions.Add(question);
                }
            }

            result.Elements.Add(info);
        }
        else
        {
            Type recommendedType = GetRecommendedSemanticType(current.gameObject);
            if (recommendedType != null)
            {
                result.Issues.Add(new UIValidationIssue(
                    UIValidationSeverity.Warning,
                    "节点包含可识别 UGUI 控件，但未挂载 YUIElement 语义脚本，可使用一键补挂。",
                    path,
                    current.gameObject));
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Traverse(current.GetChild(i), root, planInfo, result);
        }
    }

    private static void AddDuplicateIdIssues(UIViewScanResult result)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < result.Elements.Count; i++)
        {
            string id = result.Elements[i].ElementId;
            if (!counts.ContainsKey(id))
            {
                counts[id] = 0;
            }

            counts[id]++;
        }

        for (int i = 0; i < result.Elements.Count; i++)
        {
            UIElementScanInfo element = result.Elements[i];
            if (counts[element.ElementId] > 1)
            {
                result.Issues.Add(new UIValidationIssue(
                    UIValidationSeverity.Error,
                    "ElementId 重复：" + element.ElementId,
                    element.Path,
                    element.Element));
            }
        }
    }

    private static string GetPath(Transform root, Transform current)
    {
        if (current == root)
        {
            return current.name;
        }

        Stack<string> names = new Stack<string>();
        Transform cursor = current;
        while (cursor != null && cursor != root)
        {
            names.Push(cursor.name);
            cursor = cursor.parent;
        }

        return string.Join("/", names.ToArray());
    }
}
