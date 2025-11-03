using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂上此脚本，就是第三人称控制器
/// </summary>
public class ThirdPlayerMoveCtrl : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] float maxSpeed = 10f,//最大移动速度
        maxClimbSpeed = 2f; //最大攀爬速度

    [SerializeField, Range(0f, 100f)] float maxAcceleration = 10f, //最大加速度 
        maxAirAcceleration = 1f,//在空中的最大加速度
    maxClimbAcceleration = 20f;//攀爬时的最大加速度 

    [SerializeField, Range(0f, 10f)] float jumpHeight = 2f; //踢啊欧俄高度
    [SerializeField, Range(0, 5)] int maxAirJumps = 0; //跳跃段数

    [SerializeField, Range(0f, 90f)] float maxGroundAngle = 25f, //允许最大爬坡角度
        maxStairsAngle = 50f; //允许最大楼梯角度

    [SerializeField, Range(0f, 100f)] float maxSnapSpeed = 100f; //起飞速度，不大于这个速度会被牢牢吸附在平面

    [SerializeField, Min(0f)] float probeDistance = 1f; //当球体下方有地面时，无论距离有多远，我们都会进行捕捉

    [SerializeField] LayerMask probeMask = -1, //检测的层
        stairsMask = -1, //楼梯的曾
        climbMask = -1; //攀爬的曾

    [SerializeField] Transform playerInputSpace = default; //输入空间，基于什么移动

    [SerializeField, Range(90, 180)] float maxClimbAngle = 140f; //最大爬升角（攀爬）

    [SerializeField] Material normalMaterial = default, climbingMaterial = default;

    MeshRenderer meshRenderer;

    Vector3 contactNormal, //当前接触面的法线
        steepNormal, //墙面法线
        climbNormal, //攀爬法线
        lastClimbNormal;//最后一次攀爬的法线，为了爬出裂缝

    float minGroundDotProduct, //允许的最大角度的余弦（比这个大就是在地上）
        minStairsDotProduct, //允许的最大角度的余弦（楼梯）（比这个大就是在地上）
        minClimbDotProduct; //允许最大角度攀爬的点积（）

    bool desiredJump, //是否即将跳跃
        desiresClimbing; //是否即将吸附
    bool OnGround => groundContactCount > 0; //是否在地面
    bool OnSteep => steepContactCount > 0; //是否与墙面接触
    bool Climbing => climbContactCount > 0&& stepsSinceLastJump > 2; //是否正在攀爬，防止刚跳就被吸住

    int groundContactCount, //与地面的接触的点
        steepContactCount, //与墙面的接触点
        climbContactCount; //攀爬接触点数

    Vector2 playerInput;
    Vector3 velocity, connectionVelocity;
    int jumpPhase; //当前跳跃了几段

    Rigidbody body,
        connectedBody, //当前接触的物体
        previousConnectedBody; //上一个接触的物体

    int stepsSinceLastGrounded, //在空中的帧数
        stepsSinceLastJump; //跳跃时的帧数

    Vector3 upAxis, rightAxis, forwardAxis; //上轴 ，右轴，前轴

    Vector3 connectionWorldPosition, connectionLocalPosition; //接触物体的世界坐标和空间坐标

    #region 初始化
    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        meshRenderer = GetComponent<MeshRenderer>();
        OnValidate();
    }
    

    #endregion
    
    #region 状态更新
    void ClearState()
    {
        groundContactCount = steepContactCount = climbContactCount = 0;
        contactNormal = steepNormal = climbNormal = connectionVelocity = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;
    }
    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        //地面，爬坡黏贴，墙面视为地面（裂缝）
        if (CheckClimbing() || OnGround || SnapToGround() || CheckSteepContacts())
        {
            stepsSinceLastGrounded = 0;
            //如果跳跃帧数>1，防止错误着陆
            if (stepsSinceLastJump > 1)
            {
                jumpPhase = 0;
            }

            if (groundContactCount > 1)
            {
                contactNormal.Normalize();
            }
        }
        else
        {
            contactNormal = upAxis;
        }

        if (connectedBody)
        {
            if (connectedBody.isKinematic || connectedBody.mass >= body.mass)
            {
                UpdateConnectionState();
            }
        }
    }
    //解决下坡时，角色弹跳的问题，把速度由水平方向，变为斜坡方向
    void AdjustVelocity()
    {
        float acceleration, speed;
        
        //获取x轴和z轴投影
        Vector3 xAxis, zAxis;
        if (Climbing) {
            acceleration = maxClimbAcceleration;
            speed = maxClimbSpeed;
            xAxis = Vector3.Cross(contactNormal, upAxis);
            zAxis = upAxis;
        }
        else {
            acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
            speed = OnGround && desiresClimbing ? maxClimbSpeed : maxSpeed;//在攀爬前降低速度
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }
        xAxis = ProjectDirectionOnPlane(xAxis, contactNormal);
        zAxis = ProjectDirectionOnPlane(zAxis, contactNormal);

        //链接接触物体和自身的移动
        Vector3 relativeVelocity = velocity - connectionVelocity;
        //获取水平面和当前速度的cos，用于计算
        float currentX = Vector3.Dot(relativeVelocity, xAxis);
        float currentZ = Vector3.Dot(relativeVelocity, zAxis);
        
        float maxSpeedChange = acceleration * Time.deltaTime;

        //重新计算x轴和z轴在水平面的速度
        float newX =
            Mathf.MoveTowards(currentX, playerInput.x*speed, maxSpeedChange);
        float newZ =
            Mathf.MoveTowards(currentZ, playerInput.y*speed, maxSpeedChange);
        //分别给x轴和z轴赋值
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }
    
    #endregion
    
    #region 碰撞检测

    //刷新当前接触的物体的速度和位置
    void UpdateConnectionState()
    {
        if (connectedBody == previousConnectedBody)
        {
            Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) -
                                         connectionWorldPosition;
            connectionVelocity = connectionMovement / Time.deltaTime;
        }

        connectionWorldPosition = body.position;
        connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);
    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        int layer = collision.gameObject.layer;
        float minDot = GetMinDot(layer);
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis, normal);
            if (upDot >= minDot)
            {
                groundContactCount += 1;
                contactNormal += normal;
                connectedBody = collision.rigidbody;
            }
            else
            {
                //如果非平面，检测墙和爬坡
                if (upDot > -0.01f)
                {
                    //如果接触到了墙则，记录法线
                    steepContactCount += 1;
                    steepNormal += normal;
                    if (groundContactCount == 0)
                    {
                        connectedBody = collision.rigidbody;
                    }
                }

                //检测爬坡
                if (desiresClimbing &&upDot >= minClimbDotProduct && (climbMask & (1 << layer)) != 0)
                {
                    climbContactCount += 1;
                    climbNormal += normal;
                    lastClimbNormal = normal;
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }

    #endregion

    #region 刷新

    void Update()
    {
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        desiredJump |= Input.GetButtonDown("Jump"); //desiredJump一直为true，直到desiredJump被手动改为false
        desiresClimbing = Input.GetButton("Climb");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);
        //目标速度
        if (playerInputSpace)
        {
            //防止太远导致输入太小
            rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
        }
        else
        {
            rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
        }
        
        meshRenderer.material = Climbing ? climbingMaterial : normalMaterial;
    }

    void FixedUpdate()
    {
        Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
        UpdateState();
        //空气阻力，如果在跳跃时，加速度变小，增大操控难度
        AdjustVelocity();
        if (desiredJump)
        {
            desiredJump = false;
            Jump(gravity);
        }

        //拐角攀爬
        if (Climbing) {
            velocity -= contactNormal * (maxClimbAcceleration* 0.9f * Time.deltaTime);
        }
        //如果速度极小，且在地面上，则消除缓慢滑落的重力
        else if (OnGround && velocity.sqrMagnitude < 0.01f) {
            velocity += contactNormal * (Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
        }
        //攀爬前降低速度
        else if (desiresClimbing && OnGround) {
            velocity += (gravity - contactNormal * (maxClimbAcceleration * 0.9f)) * Time.deltaTime;
        }
        else {
            velocity += gravity * Time.deltaTime;
        }

        body.velocity = velocity;
        ClearState();
    }

    #endregion
    
    #region 跳跃

    void Jump(Vector3 gravity)
    {
        Vector3 jumpDirection;
        if (OnGround)
        {
            jumpDirection = contactNormal;
        }
        else if (OnSteep) //如果与墙面接触，重置跳跃次数
        {
            jumpDirection = steepNormal;
            jumpPhase = 0;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) //仅允许开启空中跳跃时启用，且能跳
        {
            //防止表面掉下来算一次跳跃
            if (jumpPhase == 0)
            {
                jumpPhase = 1;
            }

            jumpDirection = contactNormal;
        }
        else
        {
            return;
        }

        //多段跳  
        stepsSinceLastJump = 0;
        jumpPhase += 1;
        //限制跳跃速度，防止连续按跳跃后跳跃高度过大
        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
        //跳跃刨墙
        jumpDirection = (jumpDirection + upAxis).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }

        //根据当前法线跳跃
        velocity += jumpDirection * jumpSpeed;
    }

    #endregion
    
    #region 地面检测

    //低速吸附在地面
    bool SnapToGround()
    {
        //如果空中帧数大于1且启动跳跃的前2帧被跳过，则不进行吸附
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }

        //高于这个速度就起飞，不会被吸附在表面
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
        {
            return false;
        }

        //向下方发射射线，如果没有返回false
        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask))
        {
            return false;
        }

        float upDot = Vector3.Dot(upAxis, hit.normal);
        //击中的y轴必须能够爬坡
        if (upDot < GetMinDot(hit.collider.gameObject.layer))
        {
            return false;
        }

        //如果都符合，则认为还在地上，只是与地面稍微有点起飞
        groundContactCount = 1;
        contactNormal = hit.normal;
        //调整速度
        float dot = Vector3.Dot(velocity, hit.normal);
        //如果速度已经向下，则不需要矫正
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }

        connectedBody = hit.rigidbody;
        return true;
    }

    //返回是否成功将陡峭的接触面转换为虚拟地面
    bool CheckSteepContacts()
    {
        if (steepContactCount > 1)
        {
            steepNormal.Normalize();
            float upDot = Vector3.Dot(upAxis, steepNormal);
            //如果墙的角度和地面一致
            if (upDot >= minGroundDotProduct)
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }

        return false;
    }

    //检测是否能攀爬
    bool CheckClimbing()
    {
        if (Climbing)
        {
            //确定是否有多个爬升触点（检测是否在裂缝中）
            if (climbContactCount > 1) {
                climbNormal.Normalize();
                float upDot = Vector3.Dot(upAxis, climbNormal);
                if (upDot >= minGroundDotProduct) {
                    climbNormal = lastClimbNormal;
                }
            }
            groundContactCount = 1;
            contactNormal = climbNormal;
            return true;
        }

        return false;
    }

    #endregion

    #region 变换工具

    //解决下坡时的跳跃问题，获取平面上，速度在水平面的投影
    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }

    //如果这个 layer 不是楼梯层，返回地面的最小点积 minGroundDotProduct；
    //如果它 是楼梯层，返回楼梯的最小点积 minStairsDotProduct。
    float GetMinDot(int layer)
    {
        return (stairsMask & (1 << layer)) == 0 ? minGroundDotProduct : minStairsDotProduct;
    }

    #endregion
}