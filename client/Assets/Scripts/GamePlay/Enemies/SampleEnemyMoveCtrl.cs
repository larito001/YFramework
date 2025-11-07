using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleEnemyMoveCtrl : MonoBehaviour
{
    public Vector3 targetPos;

    Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        rigidbody.useGravity = false;
    }

    public void SetPlayerPosition( Vector3 targetPos)
    {
        this.targetPos = targetPos;
    }

    private void Update()
    {
    }

    private void FixedUpdate()
    {
        
        //如果已经到达目标位置
        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            transform.position = targetPos;
            rigidbody.velocity = Vector3.zero; // 关键：停止所有运动
            return;
        }

        
        //朝着角色方向给速度
        Vector3 dir = targetPos - transform.position;
        
        rigidbody.velocity = dir.normalized * 10;
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