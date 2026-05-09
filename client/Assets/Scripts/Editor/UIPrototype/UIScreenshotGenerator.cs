using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UIScreenshotGenerator
{
    public string Generate(GameObject prefabRoot, string prefabAssetPath, UIViewPlanInfo planInfo, int width, int height)
    {
        string screenshotAssetPath = UIPrototypePathUtility.GetScreenshotPath(prefabAssetPath, planInfo);
        string fullPath = UIPrototypePathUtility.AssetPathToFullPath(screenshotAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previous = null;
        Camera camera = null;

        try
        {
            GameObject instance = Object.Instantiate(prefabRoot);
            SceneManager.MoveGameObjectToScene(instance, previewScene);

            GameObject cameraObject = new GameObject("UIPrototypeScreenshotCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.65f, 0.70f, 1f);
            camera.cullingMask = ~0;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            PrepareCanvases(instance, camera, width, height);

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, camera.backgroundColor);
            ForceRebuildLayouts(instance);
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

            EditorSceneManager.ClosePreviewScene(previewScene);
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
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f + i * 0.01f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = i;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(width, height);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.anchoredPosition3D = Vector3.zero;
            }
        }
    }

    private static void ForceRebuildLayouts(GameObject root)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransforms[i]);
            rectTransforms[i].ForceUpdateRectTransforms();
        }

        Canvas.ForceUpdateCanvases();
    }

    private static bool HasVisiblePixels(Texture2D texture, Color background)
    {
        int stepX = Mathf.Max(1, texture.width / 80);
        int stepY = Mathf.Max(1, texture.height / 80);
        for (int y = 0; y < texture.height; y += stepY)
        {
            for (int x = 0; x < texture.width; x += stepX)
            {
                Color pixel = texture.GetPixel(x, y);
                float delta = Mathf.Abs(pixel.r - background.r) +
                              Mathf.Abs(pixel.g - background.g) +
                              Mathf.Abs(pixel.b - background.b);
                if (delta > 0.08f)
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
