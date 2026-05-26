using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有动态物体，管理所有组件
/// </summary>
public class Character : Actor
{
    List<ICharacterComponent>  components = new List<ICharacterComponent>();
}
