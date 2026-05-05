using System.Collections.Generic;

public class YText : YUIElement
{
    public override string ElementType
    {
        get { return "Text"; }
    }

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "默认文案";
    }
}
