using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂上此脚本，就是第三人称控制器
/// </summary>
public class ThirdPlayerMoveCtrl : MonoBehaviour
{
    /// <summary>
    /// 如何拓展Render：删除render和相关代码，然后外部update里读取状态和输入，然后播放动画等
    /// </summary>

    #region Render

    [SerializeField] Transform ball = default;

    [SerializeField, Min(0.1f)] float ballRadius = 0.5f; //球的半径

    [SerializeField, Min(0f)] float ballAlignSpeed = 180f; //对准速度

    [SerializeField, Min(0f)] float
        ballAirRotation = 0.5f, //空中旋转速度
        ballSwimRotation = 2f; //游泳旋转速度

    [SerializeField] Material normalMaterial = default, climbingMaterial = default, swimmingMaterial = default;

    MeshRenderer meshRenderer;

    Vector3 lastContactNormal, lastSteepNormal, lastConnectionVelocity; //上次接触的法线,上次陡坡的法线，上次接触物体的速度

    Quaternion AlignBallRotation(Vector3 rotationAxis, Quaternion rotation, float traveledDistance)
    {
        Vector3 ballAxis = ball.up;
        float dot = Mathf.Clamp(Vector3.Dot(ballAxis, rotationAxis), -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float maxAngle = ballAlignSpeed * traveledDistance;

        Quaternion newAlignment =
            Quaternion.FromToRotation(ballAxis, rotationAxis) * rotation;
        if (angle <= maxAngle)
        {
            return newAlignment;
        }
        else
        {
            return Quaternion.SlerpUnclamped(
                rotation, newAlignment, maxAngle / angle
            );
        }
    }

    void UpdateBall()
    {
        Material ballMaterial = normalMaterial;
        Vector3 rotationPlaneNormal = lastContactNormal;
        float rotationFactor = 1f;
        if (Climbing)
        {
            ballMaterial = climbingMaterial;
        }
        else if (Swimming)
        {
            ballMaterial = swimmingMaterial;
            rotationFactor = ballSwimRotation;
        }
        else if (!OnGround)
        {
            if (OnSteep)
            {
                rotationPlaneNormal = lastSteepNormal;
            }
            else
            {
                rotationFactor = ballAirRotation;
            }
        }

        meshRenderer.material = ballMaterial;
        Vector3 movement = (body.velocity - lastConnectionVelocity) * Time.deltaTime;
        //忽略向上运动
        movement -= rotationPlaneNormal * Vector3.Dot(movement, rotationPlaneNormal);
        float distance = movement.magnitude;

        //随平台旋转
        Quaternion rotation = ball.localRotation;
        if (connectedBody && connectedBody == previousConnectedBody)
        {
            rotation = Quaternion.Euler(connectedBody.angularVelocity * (Mathf.Rad2Deg * Time.deltaTime)) * rotation;
            if (distance < 0.001f)
            {
                ball.localRotation = rotation;
                return;
            }
        }
        else if (distance < 0.001f)
        {
            return;
        }

        float angle = distance * rotationFactor * (180f / Mathf.PI) / ballRadius;
        Vector3 rotationAxis = Vector3.Cross(rotationPlaneNormal, movement).normalized;
        rotation = Quaternion.Euler(rotationAxis * angle) * rotation;
        if (ballAlignSpeed > 0f)
        {
            rotation = AlignBallRotation(rotationAxis, rotation, distance);
        }

        ball.localRotation = rotation;
    }

    #endregion

    #region 配置参数

    [SerializeField, Range(0f, 100f), Tooltip("最大移动速度")]
    float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f), Tooltip("最大攀爬速度")]
    float maxClimbSpeed = 2f;

    [SerializeField, Range(0f, 100f), Tooltip("最大游泳速度")]
    float maxSwimSpeed = 5f;

    [SerializeField, Range(0f, 100f), Tooltip("最大加速度")]
    float maxAcceleration = 10f;

    [SerializeField, Range(0f, 100f), Tooltip("在空中的最大加速度")]
    float maxAirAcceleration = 1f;

    [SerializeField, Range(0f, 100f), Tooltip("攀爬时的最大加速度")]
    float maxClimbAcceleration = 20f;

    [SerializeField, Range(0f, 100f), Tooltip("游泳时的最大加速度")]
    float maxSwimAcceleration = 5f;

    [SerializeField, Range(0f, 10f), Tooltip("跳跃高度")]
    float jumpHeight = 2f;

    [SerializeField, Range(0, 5), Tooltip("最大空中跳跃次数（多段跳）")]
    int maxAirJumps = 0;

    [SerializeField, Range(0f, 90f), Tooltip("允许的最大地面爬坡角度")]
    float maxGroundAngle = 25f;

    [SerializeField, Range(0f, 90f), Tooltip("允许的最大楼梯角度")]
    float maxStairsAngle = 50f;

    [SerializeField, Range(0f, 100f), Tooltip("最大贴地速度，小于此速度时会保持贴地状态")]
    float maxSnapSpeed = 100f;

    [SerializeField, Min(0f), Tooltip("地面检测的射线长度")]
    float probeDistance = 1f;

    [SerializeField, Tooltip("地面检测层（Ground）")]
    LayerMask probeMask = -1;

    [SerializeField, Tooltip("楼梯检测层（Stairs）")]
    LayerMask stairsMask = -1;

    [SerializeField, Tooltip("攀爬检测层（Climb）")]
    LayerMask climbMask = -1;

    [SerializeField, Tooltip("水检测层（Water）")]
    LayerMask waterMask = 0;

    [SerializeField, Tooltip("用于确定移动方向的输入空间（一般是摄像机）")]
    Transform playerInputSpace = default;

    [SerializeField, Range(90, 180), Tooltip("最大攀爬角度")]
    float maxClimbAngle = 140f;

    [SerializeField, Tooltip("计算淹没程度的偏移位置")]
    float submergenceOffset = 0.5f;

    [SerializeField, Min(0.1f), Tooltip("物体用于计算浮力的高度范围")]
    float submergenceRange = 1f;

    [SerializeField, Range(0f, 10f), Tooltip("水中运动的阻力（拖拽系数）")]
    float waterDrag = 1f;

    [SerializeField, Min(0f), Tooltip("浮力大小")]
    float buoyancy = 1f;

    [SerializeField, Range(0.01f, 1f), Tooltip("进入游泳状态所需的淹没深度比例")]
    float swimThreshold = 0.5f;

    #endregion


    #region 外部读取状态

    public bool OnGround => groundContactCount > 0; //是否在地面
    public bool OnSteep => steepContactCount > 0; //是否与墙面接触
    public bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2; //是否正在攀爬，防止刚跳就被吸住
    public bool InWater => submergence > 0f; //是否在水里
    public bool Swimming => submergence >= swimThreshold; //是否在游泳

    #endregion


    #region 私有变量，判断状态使用

    Vector3 contactNormal, //当前接触面的法线
        steepNormal, //墙面法线
        climbNormal, //攀爬法线
        lastClimbNormal; //最后一次攀爬的法线，为了爬出裂缝

    float minGroundDotProduct, //允许的最大角度的余弦（比这个大就是在地上）
        minStairsDotProduct, //允许的最大角度的余弦（楼梯）（比这个大就是在地上）
        minClimbDotProduct; //允许最大角度攀爬的点积（）

    bool desiredJump, //是否即将跳跃
        desiresClimbing; //是否即将吸附

    int groundContactCount, //与地面的接触的点
        steepContactCount, //与墙面的接触点
        climbContactCount; //攀爬接触点数

    Vector3 playerInput;
    Vector3 velocity, connectionVelocity;
    int jumpPhase; //当前跳跃了几段

    Rigidbody body,
        connectedBody, //当前接触的物体
        previousConnectedBody; //上一个接触的物体

    int stepsSinceLastGrounded, //在空中的帧数
        stepsSinceLastJump; //跳跃时的帧数

    Vector3 upAxis, rightAxis, forwardAxis; //上轴 ，右轴，前轴

    Vector3 connectionWorldPosition, connectionLocalPosition; //接触物体的世界坐标和空间坐标

    float submergence; //被水淹没得比例0-1

    #endregion

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
        meshRenderer = ball.GetComponent<MeshRenderer>();
        OnValidate();
    }

    #endregion

    #region 状态更新

    void ClearState()
    {
        lastContactNormal = contactNormal;
        lastSteepNormal = steepNormal;
        lastConnectionVelocity = connectionVelocity;
        groundContactCount = steepContactCount = climbContactCount = 0;
        contactNormal = steepNormal = climbNormal = connectionVelocity = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;
        submergence = 0f;
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        //地面，爬坡黏贴，墙面视为地面（裂缝）
        if (CheckClimbing() || CheckSwimming() || OnGround || SnapToGround() || CheckSteepContacts())
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
        if (Climbing)
        {
            acceleration = maxClimbAcceleration;
            speed = maxClimbSpeed;
            xAxis = Vector3.Cross(contactNormal, upAxis);
            zAxis = upAxis;
        }
        else if (InWater)
        {
            float swimFactor = Mathf.Min(1f, submergence / swimThreshold);
            acceleration = Mathf.LerpUnclamped(
                OnGround ? maxAcceleration : maxAirAcceleration,
                maxSwimAcceleration, swimFactor
            );
            speed = Mathf.LerpUnclamped(maxSpeed, maxSwimSpeed, swimFactor);
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }
        else
        {
            acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
            speed = OnGround && desiresClimbing ? maxClimbSpeed : maxSpeed; //在攀爬前降低速度
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }

        xAxis = ProjectDirectionOnPlane(xAxis, contactNormal);
        zAxis = ProjectDirectionOnPlane(zAxis, contactNormal);

        //链接接触物体和自身的移动
        Vector3 relativeVelocity = velocity - connectionVelocity;
        //获取水平面和当前速度的cos，用于计算
        Vector3 adjustment = default;
        adjustment.x = playerInput.x * speed - Vector3.Dot(relativeVelocity, xAxis);
        adjustment.z = playerInput.z * speed - Vector3.Dot(relativeVelocity, zAxis);
        adjustment = Vector3.ClampMagnitude(adjustment, acceleration * Time.deltaTime);

        //分别给x轴和z轴赋值
        velocity += xAxis * adjustment.x + zAxis * adjustment.z;
        if (Swimming)
        {
            velocity += upAxis * adjustment.y;
        }
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
        if (Swimming)
        {
            return;
        }

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
                if (desiresClimbing && upDot >= minClimbDotProduct && (climbMask & (1 << layer)) != 0)
                {
                    climbContactCount += 1;
                    climbNormal += normal;
                    lastClimbNormal = normal;
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if ((waterMask & (1 << other.gameObject.layer)) != 0)
        {
            EvaluateSubmergence(other);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if ((waterMask & (1 << other.gameObject.layer)) != 0)
        {
            EvaluateSubmergence(other);
        }
    }

    //水面检测
    void EvaluateSubmergence(Collider collider)
    {
        if (Physics.Raycast(
                body.position + upAxis * submergenceOffset,
                -upAxis, out RaycastHit hit, submergenceRange + 1f,
                waterMask, QueryTriggerInteraction.Collide
            ))
        {
            submergence = 1f - hit.distance / submergenceRange;
        }
        else
        {
            submergence = 1f;
        }

        //在流动的水中游泳
        if (Swimming)
        {
            connectedBody = collider.attachedRigidbody;
        }
    }

    #endregion

    #region update和fixupdate

    void Update()
    {
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.z = Input.GetAxis("Vertical");
        playerInput.y = Swimming ? Input.GetAxis("UpDown") : 0f;
        if (Swimming)
        {
            desiresClimbing = false;
        }
        else
        {
            desiredJump |= Input.GetButtonDown("Jump"); //desiredJump一直为true，直到desiredJump被手动改为false
            desiresClimbing = Input.GetButton("Climb");
        }

        playerInput = Vector3.ClampMagnitude(playerInput, 1f);
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

        // meshRenderer.material = Climbing ? climbingMaterial : Swimming  ? swimmingMaterial : normalMaterial;
        UpdateBall();
    }

    void FixedUpdate()
    {
        Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
        UpdateState();

        if (InWater)
        {
            velocity *= 1f - waterDrag * submergence * Time.deltaTime;
        }


        //空气阻力，如果在跳跃时，加速度变小，增大操控难度
        AdjustVelocity();
        if (desiredJump)
        {
            desiredJump = false;
            Jump(gravity);
        }

        //拐角攀爬
        if (Climbing)
        {
            velocity -= contactNormal * (maxClimbAcceleration * 0.9f * Time.deltaTime);
        }
        else if (InWater)
        {
            velocity += gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
        }
        //如果速度极小，且在地面上，则消除缓慢滑落的重力
        else if (OnGround && velocity.sqrMagnitude < 0.01f)
        {
            velocity += contactNormal * (Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
        }
        //攀爬前降低速度
        else if (desiresClimbing && OnGround)
        {
            velocity += (gravity - contactNormal * (maxClimbAcceleration * 0.9f)) * Time.deltaTime;
        }
        else
        {
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

        if (InWater)
        {
            jumpSpeed *= Mathf.Max(0f, 1f - submergence / swimThreshold);
        }

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

    #region 地面、吸附、游泳、攀爬等检测

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
        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask,
                QueryTriggerInteraction.Ignore))
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
            if (climbContactCount > 1)
            {
                climbNormal.Normalize();
                float upDot = Vector3.Dot(upAxis, climbNormal);
                if (upDot >= minGroundDotProduct)
                {
                    climbNormal = lastClimbNormal;
                }
            }

            groundContactCount = 1;
            contactNormal = climbNormal;
            return true;
        }

        return false;
    }

    //检测是否正在游泳
    bool CheckSwimming()
    {
        if (Swimming)
        {
            groundContactCount = 0;
            contactNormal = upAxis;
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