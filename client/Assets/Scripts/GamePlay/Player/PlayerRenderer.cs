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
    public GameObject ak;
    public GameObject nife;

    private void Awake()
    {
        camera = YFramework.cameraMgr.getMainCamera();
        rigidbody = thirdPlayerMoveCtrl.GetComponent<Rigidbody>();
    }

    public void UseNife()
    {
        ak.SetActive(false);
        nife.SetActive(true);
        animator.SetLayerWeight(0, 0);
        animator.SetLayerWeight(1, 0);
        animator.SetLayerWeight(2, 1);
        animator.SetLayerWeight(3, 1);
        animator.SetLayerWeight(4, 0);
    }

    public void UseAK()
    {
        ak.SetActive(true);
        nife.SetActive(false);
        animator.SetLayerWeight(0, 0);
        animator.SetLayerWeight(1, 1);
        animator.SetLayerWeight(2, 0);
        animator.SetLayerWeight(3, 0);
        animator.SetLayerWeight(4, 1);
        
    }

    public void SetAtacking(bool atk)
    {
        animator.SetBool("atking", atk);
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
            forward.y = 0;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return; // 或保持当前朝向
            }

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

        // var v = rigidbody.velocity;
        // // 去掉垂直分量，只取水平速度
        // v -= up * Vector3.Dot(v, up);
        //
        // // 如果速度足够大，则更新朝向
        // if (v.sqrMagnitude > 0.001f)
        // {
        //     transform.rotation = Quaternion.LookRotation(v.normalized, up);
        // }
    }
}