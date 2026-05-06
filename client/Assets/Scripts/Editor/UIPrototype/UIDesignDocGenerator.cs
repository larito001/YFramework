using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

public sealed class UIDesignDocGenerator
{
    private const string LegacyContentMarker = "<!--content-->";
    private const string ContentBeginMarker = "<!--content-begin-->";
    private const string ContentEndMarker = "<!--content-end-->";
    private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.+)$");

    public string Generate(string prefabAssetPath, UIViewPlanInfo planInfo, UIViewScanResult scanResult)
    {
        string docAssetPath = UIPrototypePathUtility.GetDesignDocPath(prefabAssetPath, planInfo);
        string fullPath = UIPrototypePathUtility.AssetPathToFullPath(docAssetPath);
        Dictionary<string, string> oldBlocks = File.Exists(fullPath)
            ? ExtractContentBlocks(File.ReadAllLines(fullPath, Encoding.UTF8))
            : new Dictionary<string, string>();

        MarkdownWriter writer = new MarkdownWriter(oldBlocks);
        writer.AddHeading(1, scanResult.ViewName + "策划案");
        writer.AddHeading(2, "1. 基础信息");
        writer.AddLine("- 界面 ID：" + scanResult.ViewId);
        writer.AddLine("- 界面名称：" + scanResult.ViewName);
        writer.AddLine("- UI 版本：" + planInfo.version);
        writer.AddLine("- 生成时间：" + System.DateTime.Now.ToString("yyyy-MM-dd"));
        writer.AddLine("- 负责人：待填写");
        writer.AddBlankLine();

        writer.AddHeading(2, "2. UI 截图");
        if (!string.IsNullOrEmpty(planInfo.lastScreenshot))
        {
            writer.AddLine("![" + scanResult.ViewId + "](./" + Path.GetFileName(planInfo.lastScreenshot) + ")");
        }
        else
        {
            writer.AddLine("待生成截图。");
        }

        writer.AddBlankLine();
        writer.AddHeading(2, "本次 UI 变更");
        AddChangeSummary(writer, scanResult, planInfo, oldBlocks);

        writer.AddHeading(2, "3. 界面整体说明");
        writer.AddHeading(3, "3.1 打开入口");
        writer.AddContentBlock("待填写。");
        writer.AddHeading(3, "3.2 关闭规则");
        writer.AddContentBlock("待填写。");
        writer.AddHeading(3, "3.3 权限/等级/条件");
        writer.AddContentBlock("待填写。");

        writer.AddHeading(2, "4. UI 元素说明");
        for (int i = 0; i < scanResult.Elements.Count; i++)
        {
            UIElementScanInfo element = scanResult.Elements[i];
            if (!element.ExportToDesignDoc)
            {
                continue;
            }

            string title = element.ElementId + "：" + GetDisplayName(element);
            writer.AddHeading(3, title);
            writer.AddLine("- 类型：" + element.ElementType);
            writer.AddLine("- 路径：" + element.Path);
            writer.AddLine("- 策划注释：" + (string.IsNullOrEmpty(element.PlannerComment) ? "待填写" : element.PlannerComment));
            writer.AddBlankLine();

            for (int questionIndex = 0; questionIndex < element.DesignQuestions.Count; questionIndex++)
            {
                writer.AddHeading(4, element.DesignQuestions[questionIndex]);
                writer.AddContentBlock("待填写。");
            }
        }

        AddDeletedElements(writer, planInfo, oldBlocks);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, writer.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(docAssetPath);

        planInfo.lastGeneratedDoc = docAssetPath;
        return docAssetPath;
    }

    private static void AddChangeSummary(
        MarkdownWriter writer,
        UIViewScanResult scanResult,
        UIViewPlanInfo planInfo,
        Dictionary<string, string> oldBlocks)
    {
        bool wroteAny = false;
        for (int i = 0; i < scanResult.Elements.Count; i++)
        {
            UIElementScanInfo element = scanResult.Elements[i];
            if (!element.ExportToDesignDoc || element.DesignQuestions.Count == 0)
            {
                continue;
            }

            string key = BuildElementQuestionKey(scanResult.ViewName, element, element.DesignQuestions[0]);
            if (!oldBlocks.ContainsKey(key))
            {
                writer.AddLine("- 新增：" + element.ElementId + "，" + GetSummaryName(element));
                wroteAny = true;
            }
        }

        if (planInfo.deletedElements != null)
        {
            for (int i = 0; i < planInfo.deletedElements.Count; i++)
            {
                UIDeletedElementInfo deleted = planInfo.deletedElements[i];
                writer.AddLine("- 删除：" + deleted.elementId + "，原路径 " + deleted.path);
                wroteAny = true;
            }
        }

        if (!wroteAny)
        {
            writer.AddLine("- 无结构变化。");
        }

        writer.AddBlankLine();
    }

    private static void AddDeletedElements(
        MarkdownWriter writer,
        UIViewPlanInfo planInfo,
        Dictionary<string, string> oldBlocks)
    {
        if (planInfo.deletedElements == null || planInfo.deletedElements.Count == 0)
        {
            return;
        }

        writer.AddHeading(2, "5. 已删除 UI 元素");
        for (int i = 0; i < planInfo.deletedElements.Count; i++)
        {
            UIDeletedElementInfo deleted = planInfo.deletedElements[i];
            writer.AddHeading(3, "[已删除] " + deleted.elementId);
            writer.AddLine("- 类型：" + deleted.elementType);
            writer.AddLine("- 原路径：" + deleted.path);
            writer.AddLine("- 历史策划注释：" + (string.IsNullOrEmpty(deleted.plannerComment) ? "无" : deleted.plannerComment));
            writer.AddLine("- 删除版本：" + deleted.deletedAtVersion);
            writer.AddBlankLine();
            writer.AddHeading(4, "历史说明");
            writer.AddContentBlock("该元素已从当前 Prefab 中删除，历史策划内容保留待确认。");
        }
    }

    private static string GetDisplayName(UIElementScanInfo element)
    {
        return string.IsNullOrEmpty(element.DisplayName) ? element.ElementId : element.DisplayName;
    }

    private static string GetSummaryName(UIElementScanInfo element)
    {
        if (!string.IsNullOrEmpty(element.PlannerComment))
        {
            return element.PlannerComment;
        }

        return GetDisplayName(element);
    }

    private static string BuildElementQuestionKey(string viewName, UIElementScanInfo element, string question)
    {
        return viewName + "策划案>4. UI 元素说明>" + element.ElementId + "：" + GetDisplayName(element) + ">" + question;
    }

    private static Dictionary<string, string> ExtractContentBlocks(string[] lines)
    {
        Dictionary<string, string> blocks = new Dictionary<string, string>();
        List<string> headingStack = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = HeadingRegex.Match(lines[i]);
            if (match.Success)
            {
                int level = match.Groups[1].Value.Length;
                while (headingStack.Count >= level)
                {
                    headingStack.RemoveAt(headingStack.Count - 1);
                }

                headingStack.Add(match.Groups[2].Value.Trim());
                continue;
            }

            string trimmedLine = lines[i].Trim();
            bool isPairedBlock = trimmedLine == ContentBeginMarker;
            bool isLegacyBlock = trimmedLine == LegacyContentMarker;
            if (!isPairedBlock && !isLegacyBlock)
            {
                continue;
            }

            string key = string.Join(">", headingStack.ToArray());
            StringBuilder content = new StringBuilder();
            i++;
            while (i < lines.Length)
            {
                if (isPairedBlock && lines[i].Trim() == ContentEndMarker)
                {
                    break;
                }

                if (isLegacyBlock && HeadingRegex.IsMatch(lines[i]))
                {
                    i--;
                    break;
                }

                content.AppendLine(lines[i]);
                i++;
            }

            blocks[key] = TrimTrailingBlankLines(content.ToString());
        }

        return blocks;
    }

    private static string TrimTrailingBlankLines(string text)
    {
        return text.TrimEnd('\r', '\n');
    }

    private sealed class MarkdownWriter
    {
        private readonly Dictionary<string, string> oldBlocks;
        private readonly List<string> headingStack = new List<string>();
        private readonly StringBuilder builder = new StringBuilder();

        public MarkdownWriter(Dictionary<string, string> oldBlocks)
        {
            this.oldBlocks = oldBlocks;
        }

        public void AddHeading(int level, string title)
        {
            while (headingStack.Count >= level)
            {
                headingStack.RemoveAt(headingStack.Count - 1);
            }

            headingStack.Add(title);
            AddBlankLine();
            builder.AppendLine(new string('#', level) + " " + title);
            builder.AppendLine();
        }

        public void AddLine(string line)
        {
            builder.AppendLine(line);
        }

        public void AddBlankLine()
        {
            if (builder.Length > 0 && !builder.ToString().EndsWith("\n\n"))
            {
                builder.AppendLine();
            }
        }

        public void AddContentBlock(string defaultContent)
        {
            string key = string.Join(">", headingStack.ToArray());
            string content;
            if (!oldBlocks.TryGetValue(key, out content) || string.IsNullOrEmpty(content))
            {
                content = defaultContent;
            }

            builder.AppendLine(ContentBeginMarker);
            builder.AppendLine(content);
            builder.AppendLine(ContentEndMarker);
            builder.AppendLine();
        }

        public override string ToString()
        {
            return builder.ToString();
        }
    }
}
