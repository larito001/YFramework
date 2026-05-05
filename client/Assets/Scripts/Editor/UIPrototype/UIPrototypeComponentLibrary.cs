using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIPrototypeComponentLibrary
{
    private readonly List<GameObject> prefabCache = new List<GameObject>();
    private bool cacheDirty = true;

    public List<GameObject> LoadComponentPrefabs()
    {
        if (!cacheDirty)
        {
            return new List<GameObject>(prefabCache);
        }

        prefabCache.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UIPrototypePathUtility.ComponentLibraryPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabCache.Add(prefab);
            }
        }

        cacheDirty = false;
        return new List<GameObject>(prefabCache);
    }

    public void ClearCache()
    {
        cacheDirty = true;
    }

    public GameObject AddComponentToParent(GameObject componentPrefab, Transform parent)
    {
        if (componentPrefab == null || parent == null)
        {
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(componentPrefab) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(componentPrefab);
        }

        instance.transform.SetParent(parent, false);
        EnsureSemanticComponent(instance);
        RenameWithUniqueDefault(instance, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Add UI Prototype Component");
        return instance;
    }

    public void CreateDefaultComponentPrefabs()
    {
        EnsureAssetFolder(UIPrototypePathUtility.ComponentLibraryPath);

        CreatePrefabIfMissing("Button", CreateButton);
        CreatePrefabIfMissing("Text", CreateText);
        CreatePrefabIfMissing("Image", CreateImage);
        CreatePrefabIfMissing("Slider", CreateSlider);
        CreatePrefabIfMissing("ScrollView", CreateScrollView);
        CreatePrefabIfMissing("InputField", CreateInputField);
        AssetDatabase.Refresh();
        ClearCache();
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string normalizedPath = UIPrototypePathUtility.NormalizeAssetPath(folderPath);
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return;
        }

        string[] parts = normalizedPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
                AssetDatabase.Refresh();
            }

            current = next;
        }
    }

    private static void EnsureSemanticComponent(GameObject instance)
    {
        if (instance.GetComponent<YUIElement>() != null)
        {
            return;
        }

        Type type = UIPrefabScanner.GetRecommendedSemanticType(instance);
        if (type != null)
        {
            instance.AddComponent(type);
        }
    }

    private static void RenameWithUniqueDefault(GameObject instance, Transform parent)
    {
        YUIElement element = instance.GetComponent<YUIElement>();
        string elementType = element == null ? "UI" : element.ElementType;
        string prefix = UIPrefabScanner.GetDefaultNamePrefix(elementType);
        string baseName = prefix + "New";
        string candidate = baseName;
        int index = 1;

        while (HasChildNamed(parent, candidate, instance.transform))
        {
            candidate = baseName + index;
            index++;
        }

        instance.name = candidate;
    }

    private static bool HasChildNamed(Transform parent, string name, Transform ignore)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != ignore && child.name == name)
            {
                return true;
            }
        }

        return false;
    }

    private static void CreatePrefabIfMissing(string name, Func<GameObject> factory)
    {
        string path = UIPrototypePathUtility.ComponentLibraryPath + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return;
        }

        GameObject go = factory();
        try
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static GameObject CreateButton()
    {
        GameObject button = CreateUIObject("Button");
        Image image = button.AddComponent<Image>();
        image.color = new Color(0.18f, 0.32f, 0.54f, 1f);
        button.AddComponent<Button>();
        button.AddComponent<YButton>();
        SetSize(button, 220f, 72f);

        GameObject label = CreateUIObject("Txt_Label");
        label.transform.SetParent(button.transform, false);
        Text text = label.AddComponent<Text>();
        text.text = "Button";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = GetBuiltinFont();
        Stretch(label);
        return button;
    }

    private static GameObject CreateText()
    {
        GameObject textObject = CreateUIObject("Text");
        Text text = textObject.AddComponent<Text>();
        text.text = "Text";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = GetBuiltinFont();
        textObject.AddComponent<YText>();
        SetSize(textObject, 220f, 56f);
        return textObject;
    }

    private static GameObject CreateImage()
    {
        GameObject imageObject = CreateUIObject("Image");
        Image image = imageObject.AddComponent<Image>();
        image.color = new Color(0.28f, 0.45f, 0.36f, 1f);
        imageObject.AddComponent<YImage>();
        SetSize(imageObject, 160f, 160f);
        return imageObject;
    }

    private static GameObject CreateSlider()
    {
        GameObject sliderObject = CreateUIObject("Slider");
        sliderObject.AddComponent<Slider>();
        sliderObject.AddComponent<YSlider>();
        SetSize(sliderObject, 260f, 40f);
        return sliderObject;
    }

    private static GameObject CreateScrollView()
    {
        GameObject scrollObject = CreateUIObject("ScrollView");
        Image image = scrollObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.18f);
        scrollObject.AddComponent<ScrollRect>();
        scrollObject.AddComponent<YScrollView>();
        SetSize(scrollObject, 420f, 360f);

        GameObject viewport = CreateUIObject("Viewport");
        viewport.transform.SetParent(scrollObject.transform, false);
        viewport.AddComponent<RectMask2D>();
        Stretch(viewport);

        GameObject content = CreateUIObject("Content");
        content.transform.SetParent(viewport.transform, false);
        Stretch(content);

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        return scrollObject;
    }

    private static GameObject CreateInputField()
    {
        GameObject inputObject = CreateUIObject("InputField");
        Image image = inputObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.85f);
        InputField input = inputObject.AddComponent<InputField>();
        inputObject.AddComponent<YInput>();
        SetSize(inputObject, 260f, 64f);

        GameObject textObject = CreateUIObject("Text");
        textObject.transform.SetParent(inputObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.color = Color.black;
        text.font = GetBuiltinFont();
        text.alignment = TextAnchor.MiddleLeft;
        Stretch(textObject);
        input.textComponent = text;
        return inputObject;
    }

    private static GameObject CreateUIObject(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static void SetSize(GameObject go, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(GameObject go)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
