using System;
using System.Collections.Generic;

public enum UIValidationSeverity
{
    Info,
    Warning,
    Error
}

[Serializable]
public sealed class UIViewPlanInfo
{
    public string viewId;
    public string viewName;
    public int version = 1;
    public string lastModifiedTime;
    public string lastGeneratedDoc;
    public string lastScreenshot;
    public Dictionary<string, UIElementPlanInfo> elements = new Dictionary<string, UIElementPlanInfo>();
    public List<UIDeletedElementInfo> deletedElements = new List<UIDeletedElementInfo>();
}

[Serializable]
public sealed class UIElementPlanInfo
{
    public string plannerComment;
    public bool exportToDesignDoc = true;
}

[Serializable]
public sealed class UIDeletedElementInfo
{
    public string elementId;
    public string path;
    public string elementType;
    public string plannerComment;
    public bool exportToDesignDoc = true;
    public int deletedAtVersion;
}

public sealed class UIViewScanResult
{
    public string ViewId;
    public string ViewName;
    public readonly List<UIElementScanInfo> Elements = new List<UIElementScanInfo>();
    public readonly List<UIValidationIssue> Issues = new List<UIValidationIssue>();
}

public sealed class UIElementScanInfo
{
    public string ElementId;
    public string DisplayName;
    public string ElementType;
    public string Path;
    public bool ExportToDesignDoc;
    public string PlannerComment;
    public YUIElement Element;
    public readonly List<string> DesignQuestions = new List<string>();
}

public sealed class UIValidationIssue
{
    public UIValidationSeverity Severity;
    public string Message;
    public string Path;
    public UnityEngine.Object Context;

    public UIValidationIssue(UIValidationSeverity severity, string message, string path = null, UnityEngine.Object context = null)
    {
        Severity = severity;
        Message = message;
        Path = path;
        Context = context;
    }
}
