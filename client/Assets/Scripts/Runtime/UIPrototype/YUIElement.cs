using System.Collections.Generic;
using UnityEngine;

public abstract class YUIElement : MonoBehaviour
{
    public abstract string ElementType { get; }

    public virtual string DisplayName
    {
        get { return gameObject.name; }
    }

    public virtual IEnumerable<string> GetDesignQuestions()
    {
        yield return "功能说明";
    }
}
