using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class PlayerRenderer : MonoBehaviour
{
    public ThirdPlayerMoveCtrl thirdPlayerMoveCtrl;
    private Rigidbody rigidbody;
    Camera camera;
    public Animator animator;

    private void Awake()
    {
        camera = YFramework.cameraMgr.getMainCamera();
        rigidbody = thirdPlayerMoveCtrl.GetComponent<Rigidbody>();
    }

    private Vector3 touchPosition;

    private void Update()
    {
        var up = CustomGravity.GetUpAxis(transform.position);
        touchPosition = Input.mousePosition;
        Vector3 screenPos = new Vector3(touchPosition.x, touchPosition.y, 0);
        Ray ray = camera.ScreenPointToRay(screenPos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            var forward = hit.point - transform.position;
            forward.y = transform.position.y;
            //当前角色转向hit的方向
            var target = Quaternion.LookRotation(forward, up);


            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 5);
        }
        
        var worldVelocity = rigidbody.velocity;

// 转换到角色本地空间
        var localVelocity = animator.transform.InverseTransformDirection(worldVelocity);

// 可选：忽略 Y
        localVelocity.y = 0f;

// 直接喂给 Animator
        animator.SetFloat("verticalSpeed", localVelocity.z);
        animator.SetFloat("horizontalSpeed", localVelocity.x);
        //

        var v = rigidbody.velocity;
        // 去掉垂直分量，只取水平速度
        v -= up * Vector3.Dot(v, up);
        //
        // // 如果速度足够大，则更新朝向
        // if (v.sqrMagnitude > 0.001f)
        // {
        //     transform.rotation = Quaternion.LookRotation(v.normalized, up);
        // }
    }
}