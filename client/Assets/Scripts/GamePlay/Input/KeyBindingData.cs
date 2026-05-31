using System;
using System.Collections.Generic;

/// <summary>按键绑定存档结构(JsonUtility 友好:用 List+字符串,不用 Dictionary/枚举键)。</summary>
[Serializable]
public class KeyBindingData
{
    public List<KeyBindingEntry> bindings = new List<KeyBindingEntry>();
}

[Serializable]
public class KeyBindingEntry
{
    public string action; // InputAction 名(ToString)
    public int key;       // (int)KeyCode
}
