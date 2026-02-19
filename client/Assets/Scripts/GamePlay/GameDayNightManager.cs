using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YOTO;

public class GameDayNightManager : IGameService
{
    public static GameDayNightManager Instance;

    public GameDayNightManager()
    {
        Instance = this;
        ResetDayNight();
    }
    RunPhaseMachine phaseMachine;
    private Light _mainLight;
    private Coroutine _lightCoroutine;


    private float _transitionDuration = 1f; //切换 时间
    private float _dayTime; // 白天持续时间（秒）
    private float _nightTime; // 夜晚持续时间（秒）
    private float _allTimer; // 一个完整昼夜周期
    private float _currentTimer; // 当前周期计时
    private bool _isDay; // 当前是否为白天
    

    /// <summary>
    /// 初始化昼夜参数
    /// </summary>
    public void ResetDayNight()
    {
        if ( GameLoop.Instance.isTest)
        {
            _dayTime = 9f; // 15 分钟白天
        }
        else
        {
            _dayTime = 420f; // 15 分钟白天
        }


        _nightTime = 180f; // 5 分钟夜晚

        _allTimer = _dayTime + _nightTime;
        _currentTimer = 0f;
        _isDay = true;
        CacheLight();
        OnEnterDay();
    }

    private float lastGenerationThreshold = 0f; // 记录上一次生成的阈值
    private float lastDayGenerationThreshold = 0f; // 记录上一次生成的阈值
    private float NightGeneratePoint = 0.34f;
    private float DayGeneratePoint = 0.34f;

    /// <summary>
    /// 外部驱动更新（例如由 GameLogic.Update(dt) 调用）
    /// </summary>
    public void Update(float dt)
    {
        float previousTimer = _currentTimer;
        bool previousIsDay = _isDay;

        _currentTimer += dt;

        if (_currentTimer >= _allTimer)
        {
            _currentTimer -= _allTimer;
        }
        GameLoop.Instance.Ctx.Get<EventMgr>().TriggerEvent(YOTOEventType.RefreshTime);
        
        // 判定当前是否为白天
        _isDay = _currentTimer < _dayTime;

        // 状态切换检测
        if (previousIsDay != _isDay)
        {
            if (_isDay)
            {
                OnEnterDay();
            }
            else
            {
                OnEnterNight();
            }
        }

        //黑夜刷怪
        if (!_isDay)
        {
            var rate = GetPhaseRate();
            // 每增加0.2生成一次
            // 检查rate是否达到了下一个0.2的阈值
            float nextThreshold = lastGenerationThreshold + NightGeneratePoint;

            if (rate >= nextThreshold)
            {
                // EnemiesManager.instance.OnNightGenerate(TrainManager.Instance.GetTrainPos(), 20 * 2);
                lastGenerationThreshold = nextThreshold;

                // 如果rate一次性跨越了多个0.2区间，处理这种情况
                while (rate >= lastGenerationThreshold + NightGeneratePoint)
                {
                    lastGenerationThreshold += NightGeneratePoint;
                    // EnemiesManager.instance.OnNightGenerate(TrainManager.Instance.GetTrainPos(), 20 * 2);
                }
            }
        }
        else
        {
            lastGenerationThreshold = -NightGeneratePoint;
        }

        //白天刷怪
        if (_isDay)
        {
            var rate = GetPhaseRate();
            // 每增加0.2生成一次
            // 检查rate是否达到了下一个0.2的阈值
            float nextThreshold = lastDayGenerationThreshold + DayGeneratePoint;

            if (rate >= nextThreshold)
            {
                // if (PlayerManager.Instance.playerEntity != null)
                // {
                //     EnemiesManager.instance.OnNightGenerate(PlayerManager.Instance.playerEntity.ObjTrans.position, 10);
                // }
                // else
                // {
                //     EnemiesManager.instance.OnNightGenerate(TrainManager.Instance.GetTrainPos(), 10);
                // }
                //
                // lastDayGenerationThreshold = nextThreshold;
                //
                // // 如果rate一次性跨越了多个0.2区间，处理这种情况
                // while (rate >= lastDayGenerationThreshold + DayGeneratePoint)
                // {
                //     lastDayGenerationThreshold += DayGeneratePoint;
                //     if (PlayerManager.Instance.playerEntity != null)
                //     {
                //         EnemiesManager.instance.OnNightGenerate(PlayerManager.Instance.playerEntity.ObjTrans.position,
                //             10);
                //     }
                //     else
                //     {
                //         EnemiesManager.instance.OnNightGenerate(TrainManager.Instance.GetTrainPos(), 10);
                //     }
                // }
            }
        }
        else
        {
            lastDayGenerationThreshold = -DayGeneratePoint;
        }
    }

    /// <summary>
    /// 当前整个昼夜周期进度（0~1）
    /// </summary>
    public float GetCurrentRate()
    {
        return _currentTimer / _allTimer;
    }

    /// <summary>
    /// 当前昼夜阶段内的进度（0~1）
    /// 白天：0~1
    /// 夜晚：0~1
    /// </summary>
    public float GetPhaseRate()
    {
        if (_isDay)
        {
            return _currentTimer / _dayTime;
        }
        else
        {
            return (_currentTimer - _dayTime) / _nightTime;
        }
    }

    /// <summary>
    /// 是否为白天
    /// </summary>
    public bool IsDay()
    {
        return _isDay;
    }

    /// <summary>
    /// 是否为夜晚
    /// </summary>
    public bool IsNight()
    {
        return !_isDay;
    }


    private void CacheLight()
    {
        if (_mainLight == null)
        {
            var lightObj = GameObject.Find("MainLight");
            if (lightObj)
            {
                _mainLight = lightObj.GetComponent<Light>();
            }
        }
    }

    /// <summary>
    /// 进入白天时调用（只触发一次）
    /// </summary>
    public void OnEnterDay()
    {
        Debug.Log("Enter Day");
        CacheLight();
        if (_mainLight == null) return;

        StartLightTransition(
            targetColor: new Color(1f, 0.95f, 0.85f), // 日光
            targetIntensity: 1.2f
        );
    }


    /// <summary>
    /// 进入夜晚时调用（只触发一次）
    /// </summary>
    public void OnEnterNight()
    {
        Debug.Log("Enter Night");
        CacheLight();
        if (_mainLight == null) return;

        StartLightTransition(
            targetColor: new Color(0.4f, 0.5f, 0.8f), // 月光
            targetIntensity: 0.2f
        );
    }

    private void StartLightTransition(Color targetColor, float targetIntensity)
    {
        if (_lightCoroutine != null)
        {
            GameLoop.Instance.StopCoroutine(_lightCoroutine);
        }

        _lightCoroutine = GameLoop.Instance.StartCoroutine(
            LightLerpCoroutine(targetColor, targetIntensity)
        );
    }


    private IEnumerator LightLerpCoroutine(Color targetColor, float targetIntensity)
    {
        Color startColor = _mainLight.color;
        float startIntensity = _mainLight.intensity;

        float timer = 0f;

        while (timer < _transitionDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / _transitionDuration);

            _mainLight.color = Color.Lerp(startColor, targetColor, t);
            _mainLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);

            yield return null;
        }

        // 确保最终值精确
        _mainLight.color = targetColor;
        _mainLight.intensity = targetIntensity;

        _lightCoroutine = null;
    }

    public string GetTime()
    {
        if (_isDay)
        {
            return "白天倒计时：" + ((int)(_dayTime - _currentTimer)).ToString()+"s";
        }
        else
        {
            return "夜晚倒计时：" + ((int)(_nightTime - (_currentTimer - _dayTime))).ToString()+"s";
        }
    }

    public void Init(GameContext ctx)
    {
        // ResetDayNight();
    }

    public void Shutdown()
    {
        
    }
}