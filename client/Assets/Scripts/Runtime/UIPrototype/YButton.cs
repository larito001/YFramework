using System.Collections.Generic;

public class YButton : YUIElement
{
    public override string ElementType
    {
        get { return "Button"; }
    }

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "点击后行为";
        yield return "按钮文本";
    }
}
