using System.Collections;
using UnityEngine;
using YOTO;

public class GameDayNightManager : IGameService, ITickable
{
    private Light mainLight;
    private Coroutine lightCoroutine;
    private GameContext ctx;
    private SceneReferenceService sceneReferenceService;
    private ICoroutineRunner coroutineRunner;
    private GameRuntimeConfig runtimeConfig;

    private float transitionDuration = 1f;
    private float dayTime;
    private float nightTime;
    private float allTimer;
    private float currentTimer;
    private bool isDay = true;
    private float lastGenerationThreshold;
    private float lastDayGenerationThreshold;
    private float nightGeneratePoint = 0.34f;
    private float dayGeneratePoint = 0.34f;

    public void ResetDayNight()
    {
        dayTime = runtimeConfig.IsTest ? 9f : 420f;
        nightTime = 180f;
        allTimer = dayTime + nightTime;
        currentTimer = 0f;
        isDay = true;
        CacheLight();
        OnEnterDay();
    }

    public void Update(float dt)
    {
        bool previousIsDay = isDay;
        currentTimer += dt;

        if (currentTimer >= allTimer)
        {
            currentTimer -= allTimer;
        }

        ctx.Get<EventMgr>().TriggerEvent(YOTOEventType.RefreshTime);
        isDay = currentTimer < dayTime;

        if (previousIsDay != isDay)
        {
            if (isDay) OnEnterDay();
            else OnEnterNight();
        }

        if (!isDay)
        {
            float rate = GetPhaseRate();
            float nextThreshold = lastGenerationThreshold + nightGeneratePoint;
            if (rate >= nextThreshold)
            {
                lastGenerationThreshold = nextThreshold;
                while (rate >= lastGenerationThreshold + nightGeneratePoint)
                {
                    lastGenerationThreshold += nightGeneratePoint;
                }
            }
        }
        else
        {
            lastGenerationThreshold = -nightGeneratePoint;
        }

        if (!isDay)
        {
            return;
        }

        float dayRate = GetPhaseRate();
        float nextDayThreshold = lastDayGenerationThreshold + dayGeneratePoint;
        if (dayRate >= nextDayThreshold)
        {
        }
        else
        {
            lastDayGenerationThreshold = -dayGeneratePoint;
        }
    }

    public float GetCurrentRate()
    {
        return currentTimer / allTimer;
    }

    public float GetPhaseRate()
    {
        return isDay ? currentTimer / dayTime : (currentTimer - dayTime) / nightTime;
    }

    public bool IsDay()
    {
        return isDay;
    }

    public bool IsNight()
    {
        return !isDay;
    }

    public void OnEnterDay()
    {
        CacheLight();
        if (mainLight == null) return;
        StartLightTransition(new Color(1f, 0.95f, 0.85f), 1.2f);
    }

    public void OnEnterNight()
    {
        CacheLight();
        if (mainLight == null) return;
        StartLightTransition(new Color(0.4f, 0.5f, 0.8f), 0.2f);
    }

    public string GetTime()
    {
        if (isDay)
        {
            return "Day: " + ((int)(dayTime - currentTimer)).ToString() + "s";
        }

        return "Night: " + ((int)(nightTime - (currentTimer - dayTime))).ToString() + "s";
    }

    public void Init(GameContext gameContext)
    {
        ctx = gameContext;
        sceneReferenceService = gameContext.Get<SceneReferenceService>();
        coroutineRunner = gameContext.Get<ICoroutineRunner>();
        runtimeConfig = gameContext.Get<GameRuntimeConfig>();
        ResetDayNight();
    }

    public void Shutdown()
    {
        if (lightCoroutine != null)
        {
            coroutineRunner.Stop(lightCoroutine);
            lightCoroutine = null;
        }
    }

    public void Tick(float dt)
    {
        Update(dt);
    }

    private void CacheLight()
    {
        if (mainLight == null && sceneReferenceService != null)
        {
            sceneReferenceService.TryGetLight(SceneReferenceKeys.MainLight, out mainLight);
        }
    }

    private void StartLightTransition(Color targetColor, float targetIntensity)
    {
        if (lightCoroutine != null)
        {
            coroutineRunner.Stop(lightCoroutine);
        }

        lightCoroutine = coroutineRunner.Run(LightLerpCoroutine(targetColor, targetIntensity));
    }

    private IEnumerator LightLerpCoroutine(Color targetColor, float targetIntensity)
    {
        Color startColor = mainLight.color;
        float startIntensity = mainLight.intensity;
        float timer = 0f;

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / transitionDuration);
            mainLight.color = Color.Lerp(startColor, targetColor, t);
            mainLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            yield return null;
        }

        mainLight.color = targetColor;
        mainLight.intensity = targetIntensity;
        lightCoroutine = null;
    }
}
