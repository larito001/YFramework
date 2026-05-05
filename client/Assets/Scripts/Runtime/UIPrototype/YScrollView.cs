using System.Collections.Generic;

public class YScrollView : YUIElement
{
    public override string ElementType
    {
        get { return "ScrollView"; }
    }

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "数据来源";
        yield return "排序规则";
        yield return "刷新时机";
        yield return "为空时表现";
        yield return "列表项点击行为";
    }
}
