using System.Collections.Generic;

public class YInput : YUIElement
{
    public override string ElementType
    {
        get { return "InputField"; }
    }

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "输入内容含义";
        yield return "默认占位文案";
        yield return "合法性校验";
        yield return "提交或失焦行为";
    }
}
