using System.Collections;
using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

public class NewTrainEngine : MonoBehaviour
{
    [Header("Train Control Settings")] public float acceleration = 5f; // 加速曲线（越大加速越猛）
    public float deceleration = -3f; // 减速曲线
    public float maxSpeed = 20f; // 最大速度（正反通用）
    public SplineFollower follower;
    public List<SplinePositioner> positioners = new List<SplinePositioner>();
    private float currentSpeed = 0f; // 当前速度
    private SplineComputer spline;
    public bool canMove = false;

    public void Start()
    {
        for (var i = 0; i < positioners.Count; i++)
        {
            TowerManager.Instance.GenerateTowerBaseAtTransform(positioners[i].transform, new Vector3(0, 2, 3));
            TowerManager.Instance.GenerateTowerBaseAtTransform(positioners[i].transform, new Vector3(0, 2, -3));
            TowerManager.Instance.GenerateTowerBaseAtTransform(positioners[i].transform, new Vector3(0, 2, 1.5f));
            TowerManager.Instance.GenerateTowerBaseAtTransform(positioners[i].transform, new Vector3(0, 2, -1.5f));
        }
    }

    private void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // 1. 根据输入设定目标速度
        float targetSpeed = 0f;

        if (canMove && Input.GetKey(KeyCode.W))
        {
            targetSpeed = maxSpeed; // 前进
        }
        else if (canMove && Input.GetKey(KeyCode.S))
        {
            targetSpeed = -maxSpeed; // 后退
        }
        else
        {
            targetSpeed = 0f; // 刹车
        }

        // 2. 选择加速或减速速率（必须为正数）
        float rate = (Mathf.Abs(targetSpeed) < 0.01f)
            ? Mathf.Abs(deceleration)
            : Mathf.Abs(acceleration);

        // 3. 平滑改变当前速度
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            rate * Time.deltaTime
        );

        // 4. 更新 SplineFollower
        follower.followSpeed = Mathf.Abs(currentSpeed);

        if (currentSpeed > 0.01f)
        {
            follower.direction = Spline.Direction.Forward;
        }
        else if (currentSpeed < -0.01f)
        {
            follower.direction = Spline.Direction.Backward;
        }
    }


    public void SetTracer(SplineComputer spline)
    {
        this.spline = spline;
        follower.spline = spline;
        for (var i = 0; i < positioners.Count; i++)
        {
            positioners[i].spline = spline;
        }
    }
}