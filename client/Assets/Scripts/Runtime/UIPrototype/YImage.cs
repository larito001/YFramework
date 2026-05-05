using System.Collections.Generic;

public class YImage : YUIElement
{
    public override string ElementType
    {
        get { return "Image"; }
    }

    public override IEnumerable<string> GetDesignQuestions()
    {
        yield return "图片用途";
        yield return "是否需要美术出图";
        yield return "资源命名";
        yield return "是否有状态变化";
    }
}
