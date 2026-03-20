using UnityEngine;
using UnityEngine.UI;

public class AudioSettingCtrl : BaseSettingCtrl
{
    [Header("Optional Existing Bindings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider uiVolumeSlider;
    [SerializeField] private Toggle masterMuteToggle;
    [SerializeField] private Toggle musicMuteToggle;
    [SerializeField] private Toggle sfxMuteToggle;
    [SerializeField] private Toggle uiMuteToggle;
    [SerializeField] private Text masterValueText;
    [SerializeField] private Text musicValueText;
    [SerializeField] private Text sfxValueText;
    [SerializeField] private Text uiValueText;

    private bool isRefreshing;
    private bool runtimeUiBuilt;

    protected override void OnInitialize()
    {
        EnsureUi();
        RefreshFromSound();
    }

    private void OnEnable()
    {
        EnsureUi();
        BindListeners();
        RefreshFromSound();
    }

    private void OnDisable()
    {
        UnbindListeners();
    }

    private void BindListeners()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
        }

        if (masterMuteToggle != null)
        {
            masterMuteToggle.onValueChanged.AddListener(OnMasterMuteChanged);
        }

        if (musicMuteToggle != null)
        {
            musicMuteToggle.onValueChanged.AddListener(OnMusicMuteChanged);
        }

        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.onValueChanged.AddListener(OnSfxMuteChanged);
        }

        if (uiMuteToggle != null)
        {
            uiMuteToggle.onValueChanged.AddListener(OnUiMuteChanged);
        }
    }

    private void UnbindListeners()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.onValueChanged.RemoveListener(OnUiVolumeChanged);
        }

        if (masterMuteToggle != null)
        {
            masterMuteToggle.onValueChanged.RemoveListener(OnMasterMuteChanged);
        }

        if (musicMuteToggle != null)
        {
            musicMuteToggle.onValueChanged.RemoveListener(OnMusicMuteChanged);
        }

        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.onValueChanged.RemoveListener(OnSfxMuteChanged);
        }

        if (uiMuteToggle != null)
        {
            uiMuteToggle.onValueChanged.RemoveListener(OnUiMuteChanged);
        }
    }

    private void RefreshFromSound()
    {
        var sound = Resolve<SoundMgr>();
        if (sound == null)
        {
            return;
        }

        isRefreshing = true;

        SetSlider(masterVolumeSlider, sound.MasterVolume);
        SetSlider(musicVolumeSlider, sound.MusicVolume);
        SetSlider(sfxVolumeSlider, sound.SfxVolume);
        SetSlider(uiVolumeSlider, sound.UiVolume);

        SetToggle(masterMuteToggle, sound.IsChannelMuted(SoundChannel.Master));
        SetToggle(musicMuteToggle, sound.IsChannelMuted(SoundChannel.Music));
        SetToggle(sfxMuteToggle, sound.IsChannelMuted(SoundChannel.Sfx));
        SetToggle(uiMuteToggle, sound.IsChannelMuted(SoundChannel.Ui));

        SetValueText(masterValueText, sound.MasterVolume);
        SetValueText(musicValueText, sound.MusicVolume);
        SetValueText(sfxValueText, sound.SfxVolume);
        SetValueText(uiValueText, sound.UiVolume);

        isRefreshing = false;
    }

    private void EnsureUi()
    {
        if (masterVolumeSlider != null && musicVolumeSlider != null && sfxVolumeSlider != null && uiVolumeSlider != null)
        {
            return;
        }

        if (runtimeUiBuilt)
        {
            return;
        }

        runtimeUiBuilt = true;

        var verticalLayout = GetOrAdd<VerticalLayoutGroup>(gameObject);
        verticalLayout.spacing = 16f;
        verticalLayout.padding = new RectOffset(24, 24, 24, 24);
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = false;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        var fitter = GetOrAdd<ContentSizeFitter>(gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateChannelRow("Master", out masterVolumeSlider, out masterMuteToggle, out masterValueText);
        CreateChannelRow("Music", out musicVolumeSlider, out musicMuteToggle, out musicValueText);
        CreateChannelRow("SFX", out sfxVolumeSlider, out sfxMuteToggle, out sfxValueText);
        CreateChannelRow("UI", out uiVolumeSlider, out uiMuteToggle, out uiValueText);
    }

    private void CreateChannelRow(string label, out Slider slider, out Toggle toggle, out Text valueText)
    {
        var row = new GameObject($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(transform, false);

        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        var rowLayoutElement = row.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 44f;

        var labelText = CreateText($"{label}Label", row.transform, label, TextAnchor.MiddleLeft);
        var labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 80f;

        var sliderObject = DefaultControls.CreateSlider(CreateDefaultResources());
        sliderObject.name = $"{label}Slider";
        sliderObject.transform.SetParent(row.transform, false);
        slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        var sliderLayout = sliderObject.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1f;
        sliderLayout.minWidth = 180f;

        var muteLabel = CreateText($"{label}MuteLabel", row.transform, "Mute", TextAnchor.MiddleRight);
        var muteLabelLayout = muteLabel.gameObject.AddComponent<LayoutElement>();
        muteLabelLayout.preferredWidth = 44f;

        var toggleObject = DefaultControls.CreateToggle(CreateDefaultResources());
        toggleObject.name = $"{label}MuteToggle";
        toggleObject.transform.SetParent(row.transform, false);
        toggle = toggleObject.GetComponent<Toggle>();
        var toggleLayout = toggleObject.AddComponent<LayoutElement>();
        toggleLayout.preferredWidth = 24f;

        valueText = CreateText($"{label}Value", row.transform, "100%", TextAnchor.MiddleRight);
        var valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 52f;
    }

    private DefaultControls.Resources CreateDefaultResources()
    {
        return new DefaultControls.Resources
        {
            standard = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd"),
            background = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd"),
            inputField = Resources.GetBuiltinResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = Resources.GetBuiltinResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = Resources.GetBuiltinResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = Resources.GetBuiltinResource<Sprite>("UI/Skin/UIMask.psd"),
        };
    }

    private static Text CreateText(string name, Transform parent, string content, TextAnchor anchor)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<Text>();
        text.text = content;
        text.alignment = anchor;
        text.color = new Color(0.16f, 0.18f, 0.23f, 1f);
        text.fontSize = 18;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        if (target.TryGetComponent<T>(out var component))
        {
            return component;
        }

        return target.AddComponent<T>();
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetMasterVolume(value);
        SetValueText(masterValueText, value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetBgmVolume(value);
        SetValueText(musicValueText, value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetSfxVolume(value);
        SetValueText(sfxValueText, value);
    }

    private void OnUiVolumeChanged(float value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetUiVolume(value);
        SetValueText(uiValueText, value);
    }

    private void OnMasterMuteChanged(bool value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetMasterMuted(value);
    }

    private void OnMusicMuteChanged(bool value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetMusicMuted(value);
    }

    private void OnSfxMuteChanged(bool value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetSfxMuted(value);
    }

    private void OnUiMuteChanged(bool value)
    {
        if (isRefreshing)
        {
            return;
        }

        Resolve<SoundMgr>()?.SetUiMuted(value);
    }

    private static void SetSlider(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }

    private static void SetToggle(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private static void SetValueText(Text text, float value)
    {
        if (text != null)
        {
            text.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
