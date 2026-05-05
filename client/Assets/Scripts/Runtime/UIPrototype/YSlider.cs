using System.Collections.Generic;

public class YSlider : YUIElement
{
    public override string ElementType
    {
        get { return "Slider"; }
    }

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "数值来源";
        yield return "最小值与最大值";
        yield return "刷新时机";
        yield return "拖动后行为";
    }
}
