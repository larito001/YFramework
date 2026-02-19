using NoSLoofah.BuffSystem;
using NoSLoofah.BuffSystem.Manager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// BuffTag�������ĳ������
/// Ŀǰֻ��λTag��һ��ʵ��
/// </summary>
public abstract class BuffTagManager :IBuffTagManager
{
    public abstract void Init(BuffTagData data);

    public abstract bool IsTagRemoveOther(BuffTag tag, BuffTag other);
    public abstract bool IsTagCanAddWhenHaveOther(BuffTag tag, BuffTag other);
    
}
