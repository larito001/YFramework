using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    public ThirdPlayerMoveCtrl thirdPlayerMoveCtrl;
    private Rigidbody  rigidbody;
    private void Awake()
    {
        rigidbody = thirdPlayerMoveCtrl.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        var up = CustomGravity.GetUpAxis(transform.position);
        var v =rigidbody.velocity;
        // 去掉垂直分量，只取水平速度
        v -= up * Vector3.Dot(v, up);

        // 如果速度足够大，则更新朝向
        if (v.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(v.normalized, up);
        }
    }
}
