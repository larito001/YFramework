using System;
using UnityEngine;

public class BoxBase : MonoBehaviour
{
    public bool IsCatching;
    public Transform pivot;     // 绳子挂点（支点）
    public float ropeLength = 2f;
    public float gravity = 9.8f;
    public float roundRatio;//旋转角度
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {

        float angleRad = roundRatio * Mathf.Deg2Rad; // 转弧度
        var x = Mathf.Sin(angleRad)*ropeLength;
        var y = Mathf.Cos(angleRad)*ropeLength;
        
        
        transform.position = pivot.position + new Vector3(x, -y, 0);
    }

    private void FixedUpdate()
    {
        if (IsCatching)
        {
            SimulatePendulum();
        }
        else
        {
            // 让 Unity 的物理系统自然处理自由落体
            rb.gravityScale = 1f;
        }
    }

    private void SimulatePendulum()
    {
        if (pivot == null) return;
        rb.gravityScale = 0f;
    
        // 从支点指向当前物体的向量
        Vector2 dir = (Vector2)transform.position - (Vector2)pivot.position;
    
        float dist = dir.magnitude;
        Vector2 dirNormalized = dir.normalized;
    
        // -------- 保持绳长恒定（约束） --------
        if (Mathf.Abs(dist - ropeLength) > 0.001f)
        {
            transform.position = (Vector2)pivot.position + dirNormalized * ropeLength;
        }
    
        // -------- 计算沿切线方向的加速度 --------
        Vector2 tangent = new Vector2(-dirNormalized.y, dirNormalized.x);
    
        float theta = Vector2.SignedAngle(Vector2.down, dir) * Mathf.Deg2Rad;
        float tangentialAccel = -gravity * Mathf.Sin(theta);
    
        float vTangent = Vector2.Dot(rb.velocity, tangent);
        vTangent += tangentialAccel * Time.fixedDeltaTime;
    
        rb.velocity = tangent * vTangent;
    
        // -------- 应用旋转（让钟摆朝向支点） --------
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

}