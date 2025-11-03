using UnityEngine;

public class GravitySphere : GravitySource {

    [SerializeField]
    float gravity = 9.81f;

    [SerializeField, Min(0f)]
    float outerRadius = 10f, //最大强度距离
        outerFalloffRadius = 15f;//衰减半径
    
    //反重力球体
    [SerializeField, Min(0f)]
    float innerFalloffRadius = 1f, //重力衰减半径
        innerRadius = 5f;//内层半径

    
    float outerFalloffFactor,//靠近球心时的重力变化率
        innerFalloffFactor;//离开球心时的重力变化率

    void Awake () {
        OnValidate();
    }

    void OnValidate () {
        innerFalloffRadius = Mathf.Max(innerFalloffRadius, 0f);
        innerRadius = Mathf.Max(innerRadius, innerFalloffRadius);
        outerRadius = Mathf.Max(outerRadius, innerRadius);
        outerFalloffRadius = Mathf.Max(outerFalloffRadius, outerRadius);
        innerFalloffFactor = 1f / (innerRadius - innerFalloffRadius);
        outerFalloffFactor = 1f / (outerFalloffRadius - outerRadius);
    }
    public override Vector3 GetGravity (Vector3 position) {
        Vector3 vector = transform.position - position;
        float distance = vector.magnitude;
        if (distance > outerFalloffRadius|| distance < innerFalloffRadius) {
            return Vector3.zero;
        }
        float g = gravity/ distance;
        if (distance > outerRadius) {
            g *= 1f - (distance - outerRadius) * outerFalloffFactor;
        }else if (distance < innerRadius) {
            g *= 1f - (innerRadius - distance) * innerFalloffFactor;
        }
        return g * vector;
    }
    void OnDrawGizmos () {
        Vector3 p = transform.position;
        
        //反重力球体
        if (innerFalloffRadius > 0f && innerFalloffRadius < innerRadius) {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(p, innerFalloffRadius);
        }

        Gizmos.color = Color.red;
        if (innerRadius > 0f && innerRadius < outerRadius) {
            Gizmos.DrawWireSphere(p, innerRadius);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p, outerRadius);
        if (outerFalloffRadius > outerRadius) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p, outerFalloffRadius);
        }
    }
}