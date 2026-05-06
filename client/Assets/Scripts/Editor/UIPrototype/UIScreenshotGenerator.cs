using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIScreenshotGenerator
{
    private static readonly Color BackgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Vector3 StagingOrigin = new Vector3(100000f, 100000f, 0f);

    public string Generate(GameObject prefabRoot, string prefabAssetPath, UIViewPlanInfo planInfo, int width, int height)
    {
        string screenshotAssetPath = UIPrototypePathUtility.GetScreenshotPath(prefabAssetPath, planInfo);
        string fullPath = UIPrototypePathUtility.AssetPathToFullPath(screenshotAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        GameObject instance = null;
        GameObject cameraObject = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previous = null;
        Camera camera = null;

        try
        {
            instance = Object.Instantiate(prefabRoot);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = StagingOrigin;

            cameraObject = new GameObject("UIPrototypeScreenshotCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.cullingMask = ~0;
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.aspect = (float)width / height;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = StagingOrigin + new Vector3(0f, 0f, -100f);
            camera.transform.rotation = Quaternion.identity;
            camera.allowMSAA = false;
            camera.allowHDR = false;

            PrepareCanvases(instance, camera, width, height);

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            camera.targetTexture = renderTexture;

            ForceRebuildAllUI(instance);

            previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, camera.backgroundColor);
            camera.Render();

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            if (!HasVisiblePixels(texture, camera.backgroundColor))
            {
                int graphicCount = CountRenderableGraphics(instance);
                throw new InvalidDataException("截图没有检测到 UI 像素。当前临时截图 Canvas 下可渲染 Graphic 数量：" + graphicCount + "。请确认 UI 节点处于激活状态，且 Image/Text 颜色不是全透明。");
            }

            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(screenshotAssetPath);
            planInfo.lastScreenshot = screenshotAssetPath;
            return screenshotAssetPath;
        }
        finally
        {
            RenderTexture.active = previous;

            if (camera != null && camera.targetTexture == renderTexture)
            {
                camera.targetTexture = null;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    private static void PrepareCanvases(GameObject instance, Camera camera, int width, int height)
    {
        SetLayerRecursively(instance, 0);
        Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
        if (canvases.Length == 0)
        {
            canvases = new[] { instance.AddComponent<Canvas>() };
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            canvas.enabled = true;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = i;

            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(width, height);
                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.position = StagingOrigin + new Vector3(0f, 0f, i * 0.01f);
            }
        }
    }

    private static void ForceRebuildAllUI(GameObject root)
    {
        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransforms[i]);
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;
            graphic.SetAllDirty();
        }

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;
            graphic.Rebuild(CanvasUpdate.PreRender);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static bool HasVisiblePixels(Texture2D texture, Color background)
    {
        int stepX = Mathf.Max(1, texture.width / 200);
        int stepY = Mathf.Max(1, texture.height / 200);
        for (int y = 0; y < texture.height; y += stepY)
        {
            for (int x = 0; x < texture.width; x += stepX)
            {
                Color pixel = texture.GetPixel(x, y);
                float delta = Mathf.Abs(pixel.r - background.r) +
                              Mathf.Abs(pixel.g - background.g) +
                              Mathf.Abs(pixel.b - background.b);
                if (delta > 0.05f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountRenderableGraphics(GameObject root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        int count = 0;
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null &&
                graphic.gameObject.activeInHierarchy &&
                graphic.enabled &&
                graphic.canvasRenderer != null &&
                graphic.color.a > 0.001f)
            {
                count++;
            }
        }

        return count;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        Transform transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }
    }
}
