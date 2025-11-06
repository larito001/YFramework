using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    public ThirdPlayerMoveCtrl thirdPlayerMoveCtrl;
    private void Awake()
    {
        
    }

    private void Update()
    {
        var up = CustomGravity.GetUpAxis(transform.position);
        var forward = thirdPlayerMoveCtrl.forwardAxis;
        transform.rotation = Quaternion.LookRotation(forward, up);
    }
}
