using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    /// <summary>Controller-first Unity port of React's Video, Keybinds, Voice, and Character settings panes.</summary>
    public sealed class WofSettingsPanelRuntime : MonoBehaviour
    {
        private enum Pane
        {
            Video,
            Keybinds,
            Voice,
            Character
        }

        private static readonly Color32 NeutralButton = new(42, 35, 48, 255);
        private static readonly Color32 FocusedButton = new(88, 28, 135, 255);
        private static readonly Color32 VideoAccent = new(250, 204, 21, 255);
        private static readonly Color32 KeybindAccent = new(165, 243, 252, 255);
        private static readonly Color32 VoiceAccent = new(110, 231, 183, 255);
        private static readonly Color32 CharacterAccent = new(249, 168, 212, 255);

        private readonly Dictionary<Button, Color> _idleColors = new();
        private readonly Dictionary<Button, Action<int>> _adjustments = new();
        private readonly Dictionary<string, Text> _bindingValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Text> _characterValues = new(StringComparer.Ordinal);
        private readonly List<Button> _navigation = new();
        private readonly List<Button>[] _paneControls =
        {
            new(), new(), new(), new()
        };

        private WofHud _hud;
        private Font _font;
        private Action _onBack;
        private WofUserSettings _settings;
        private WofSurvivalProfile _profile;
        private Button[] _tabs;
        private GameObject[] _paneRoots;
        private Button _backButton;
        private Pane _pane;
        private int _selection;
        private int _frameLimitIndex = 1;

        private Text _fullscreenValue;
        private Text _vSyncValue;
        private Text _frameLimitValue;
        private Text _mouseValue;
        private Text _controllerValue;
        private Text _hudScaleValue;
        private Text _arrowLookValue;
        private Text _voiceEnabledValue;
        private Text _voiceInputValue;
        private Text _voiceKeyValue;
        private Text _voiceVolumeValue;
        private Text _voiceRangeValue;
        private Text _voiceStatus;
        private WofLaunchWizardPreviewRenderer _characterPreview;
        private string _remappingAction;
        private bool _remapWaitingForRelease;

        public void Configure(WofHud generatedHud, Font generatedFont, Action onBack)
        {
            if (_tabs != null) return;
            _hud = generatedHud;
            _font = generatedFont;
            _onBack = onBack;
            _settings = WofUserSettingsStore.Load();
            _profile = WofSurvivalProfileStore.Load() ?? new WofSurvivalProfile { playerName = "WIZARD" };
            BuildView();
            ApplyRuntimeSettings();
            SetPane(Pane.Video, true);
        }

        public void Open()
        {
            _settings = WofUserSettingsStore.Load();
            _profile = WofSurvivalProfileStore.Load() ?? _profile ?? new WofSurvivalProfile { playerName = "WIZARD" };
            ApplyRuntimeSettings();
            RefreshLabels();
            SetPane(Pane.Video, true);
        }

        public void HandleInput()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if (!string.IsNullOrEmpty(_remappingAction))
            {
                if (keyboard?.escapeKey.wasPressedThisFrame ?? false)
                {
                    _remappingAction = null;
                    _remapWaitingForRelease = false;
                    RefreshLabels();
                    return;
                }
                if (_remapWaitingForRelease)
                {
                    if (WofControllerBindings.AreAllReleased(gamepad)) _remapWaitingForRelease = false;
                    return;
                }
                if (WofControllerBindings.TryGetPressedButton(gamepad, out var pressedButton))
                {
                    var action = _remappingAction;
                    _remappingAction = null;
                    WofControllerBindingRules.SetButton(_settings.controllerBindings, action, pressedButton);
                    SaveAndApplySettings();
                    Debug.Log($"[WOF-AUTOMATION] CONTROLLER_BINDING action={action} button={pressedButton}");
                }
                return;
            }
            if ((keyboard?.escapeKey.wasPressedThisFrame ?? false) ||
                WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuBack))
            {
                _onBack?.Invoke();
                return;
            }

            if ((gamepad?.leftShoulder.wasPressedThisFrame ?? false))
            {
                CyclePane(-1);
                return;
            }
            if ((gamepad?.rightShoulder.wasPressedThisFrame ?? false))
            {
                CyclePane(1);
                return;
            }

            if ((keyboard?.upArrowKey.wasPressedThisFrame ?? false) ||
                (gamepad?.dpad.up.wasPressedThisFrame ?? false))
            {
                SetSelection(_selection - 1);
                return;
            }
            if ((keyboard?.downArrowKey.wasPressedThisFrame ?? false) ||
                (gamepad?.dpad.down.wasPressedThisFrame ?? false))
            {
                SetSelection(_selection + 1);
                return;
            }

            var left = (keyboard?.leftArrowKey.wasPressedThisFrame ?? false) ||
                       (gamepad?.dpad.left.wasPressedThisFrame ?? false);
            var right = (keyboard?.rightArrowKey.wasPressedThisFrame ?? false) ||
                        (gamepad?.dpad.right.wasPressedThisFrame ?? false);
            if (left || right)
            {
                if (_selection < _tabs.Length) CyclePane(left ? -1 : 1);
                else AdjustSelected(left ? -1 : 1);
                return;
            }

            if ((keyboard?.enterKey.wasPressedThisFrame ?? false) ||
                WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuSelect))
            {
                if (_selection >= 0 && _selection < _navigation.Count)
                    _navigation[_selection].onClick.Invoke();
            }
        }

        private void BuildView()
        {
            var root = GetComponent<RectTransform>();
            var compact = Screen.width <= 1000 || Screen.height <= 600;
            SetRect(root, compact ? new Vector2(0.025f, 0.025f) : new Vector2(0.12f, 0.05f),
                compact ? new Vector2(0.975f, 0.975f) : new Vector2(0.88f, 0.95f));

            var title = CreateText("SettingsTitle", transform, "SETTINGS", 34, TextAnchor.MiddleCenter, Color.white);
            SetRect(title.rectTransform, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.99f));

            var tabLabels = new[] { "VIDEO", "KEYBINDS", "VOICE", "CHARACTER" };
            var accents = new[] { VideoAccent, KeybindAccent, VoiceAccent, CharacterAccent };
            _tabs = new Button[tabLabels.Length];
            for (var index = 0; index < _tabs.Length; index++)
            {
                var captured = (Pane)index;
                var minX = 0.04f + index * 0.23f;
                var tab = CreateButton($"{captured}Tab", transform, tabLabels[index], NeutralButton, 15);
                SetRect(tab.GetComponent<RectTransform>(), new Vector2(minX, 0.835f), new Vector2(minX + 0.215f, 0.905f));
                tab.onClick.AddListener(() => SetPane(captured, true));
                _idleColors[tab] = accents[index] * new Color(1f, 1f, 1f, 0.34f);
                _tabs[index] = tab;
            }

            _paneRoots = new GameObject[4];
            for (var index = 0; index < _paneRoots.Length; index++)
            {
                var paneRoot = CreatePanel($"{(Pane)index}Pane", transform, new Color32(0, 0, 0, 0));
                paneRoot.GetComponent<Image>().raycastTarget = false;
                SetRect(paneRoot.GetComponent<RectTransform>(), new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.82f));
                _paneRoots[index] = paneRoot;
            }

            BuildVideoPane();
            BuildKeybindsPane();
            BuildVoicePane();
            BuildCharacterPane();

            _backButton = CreateButton("Back", transform, "BACK", NeutralButton, 18);
            SetRect(_backButton.GetComponent<RectTransform>(), new Vector2(0.36f, 0.025f), new Vector2(0.64f, 0.105f));
            _backButton.onClick.AddListener(() => _onBack?.Invoke());
            _idleColors[_backButton] = NeutralButton;
            RefreshLabels();
        }

        private void BuildVideoPane()
        {
            var parent = _paneRoots[(int)Pane.Video].transform;
            CreateSectionTitle(parent, "VIDEO / DISPLAY", VideoAccent);
            var fullscreen = CreateSettingButton("Fullscreen", parent, "DISPLAY MODE", out _fullscreenValue, VideoAccent);
            SetRect(fullscreen.GetComponent<RectTransform>(), new Vector2(0.12f, 0.62f), new Vector2(0.88f, 0.78f));
            fullscreen.onClick.AddListener(ToggleFullscreen);
            AddControl(Pane.Video, fullscreen, _ => ToggleFullscreen());

            var vSync = CreateSettingButton("VSync", parent, "VSYNC", out _vSyncValue, VideoAccent);
            SetRect(vSync.GetComponent<RectTransform>(), new Vector2(0.12f, 0.41f), new Vector2(0.88f, 0.57f));
            vSync.onClick.AddListener(ToggleVSync);
            AddControl(Pane.Video, vSync, _ => ToggleVSync());

            var frame = CreateSettingButton("FrameLimit", parent, "FRAME LIMIT", out _frameLimitValue, VideoAccent);
            SetRect(frame.GetComponent<RectTransform>(), new Vector2(0.12f, 0.20f), new Vector2(0.88f, 0.36f));
            frame.onClick.AddListener(CycleFrameLimit);
            AddControl(Pane.Video, frame, _ => CycleFrameLimit());
        }

        private void BuildKeybindsPane()
        {
            var parent = _paneRoots[(int)Pane.Keybinds].transform;
            CreateSectionTitle(parent, "CONTROLS / REMAP", KeybindAccent);
            var mouse = CreateSettingButton("MouseSensitivity", parent, "LOOK SENSITIVITY", out _mouseValue, KeybindAccent);
            SetRect(mouse.GetComponent<RectTransform>(), new Vector2(0f, 0.72f), new Vector2(0.49f, 0.86f));
            mouse.onClick.AddListener(() => SetMouseSensitivity(WofUserSettingsRules.DefaultMouseSensitivity));
            AddControl(Pane.Keybinds, mouse, direction => SetMouseSensitivity(_settings.mouseSensitivity + direction * 0.00025f));

            var controller = CreateSettingButton("ControllerSensitivity", parent, "JOYSTICK SENSITIVITY", out _controllerValue, KeybindAccent);
            SetRect(controller.GetComponent<RectTransform>(), new Vector2(0.51f, 0.72f), new Vector2(1f, 0.86f));
            controller.onClick.AddListener(() => SetControllerSensitivity(WofUserSettingsRules.DefaultControllerLookSensitivity));
            AddControl(Pane.Keybinds, controller, direction => SetControllerSensitivity(_settings.controllerLookSensitivity + direction * 0.25f));

            var hudScale = CreateSettingButton("HudTextScale", parent, "HUD TEXT SIZE", out _hudScaleValue, KeybindAccent);
            SetRect(hudScale.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(0.49f, 0.69f));
            hudScale.onClick.AddListener(() => SetHudTextScale(WofUserSettingsRules.DefaultHudTextScale));
            AddControl(Pane.Keybinds, hudScale, direction => SetHudTextScale(_settings.hudTextScale + direction * 0.05f));

            var arrow = CreateSettingButton("ArrowLook", parent, "ARROW KEY LOOK", out _arrowLookValue, KeybindAccent);
            SetRect(arrow.GetComponent<RectTransform>(), new Vector2(0.51f, 0.55f), new Vector2(1f, 0.69f));
            arrow.onClick.AddListener(ToggleArrowLook);
            AddControl(Pane.Keybinds, arrow, _ => ToggleArrowLook());

            for (var index = 0; index < WofControllerActions.All.Length; index++)
            {
                var action = WofControllerActions.All[index];
                var column = index % 4;
                var row = index / 4;
                var minX = column * 0.255f;
                var maxX = minX + 0.235f;
                var maxY = 0.50f - row * 0.12f;
                var minY = maxY - 0.095f;
                var remap = CreateSettingButton($"Remap{action}", parent, WofControllerActions.Label(action), out var value, KeybindAccent);
                SetRect(remap.GetComponent<RectTransform>(), new Vector2(minX, minY), new Vector2(maxX, maxY));
                remap.onClick.AddListener(() => BeginControllerRemap(action));
                AddControl(Pane.Keybinds, remap, _ => BeginControllerRemap(action));
                _bindingValues[action] = value;
            }
        }

        private void BuildVoicePane()
        {
            var parent = _paneRoots[(int)Pane.Voice].transform;
            CreateSectionTitle(parent, "PROXIMITY VOICE / MIC", VoiceAccent);
            var enabled = CreateSettingButton("VoiceEnabled", parent, "VOICE CHAT", out _voiceEnabledValue, VoiceAccent);
            SetRect(enabled.GetComponent<RectTransform>(), new Vector2(0f, 0.67f), new Vector2(0.49f, 0.84f));
            enabled.onClick.AddListener(ReportVoiceUnavailable);
            AddControl(Pane.Voice, enabled, _ => ReportVoiceUnavailable());

            var input = CreateSettingButton("VoiceInput", parent, "INPUT MODE", out _voiceInputValue, VoiceAccent);
            SetRect(input.GetComponent<RectTransform>(), new Vector2(0.51f, 0.67f), new Vector2(1f, 0.84f));
            input.onClick.AddListener(ToggleVoiceInputMode);
            AddControl(Pane.Voice, input, _ => ToggleVoiceInputMode());

            var key = CreateSettingButton("VoiceKey", parent, "PRESS-TO-TALK KEY", out _voiceKeyValue, VoiceAccent);
            SetRect(key.GetComponent<RectTransform>(), new Vector2(0f, 0.46f), new Vector2(0.49f, 0.63f));
            key.onClick.AddListener(ReportVoiceUnavailable);
            AddControl(Pane.Voice, key, _ => ReportVoiceUnavailable());

            var volume = CreateSettingButton("VoiceVolume", parent, "VOICE VOLUME", out _voiceVolumeValue, VoiceAccent);
            SetRect(volume.GetComponent<RectTransform>(), new Vector2(0.51f, 0.46f), new Vector2(1f, 0.63f));
            volume.onClick.AddListener(() => SetVoiceVolume(WofUserSettingsRules.DefaultVoiceOutputVolume));
            AddControl(Pane.Voice, volume, direction => SetVoiceVolume(_settings.voiceOutputVolume + direction * 0.05f));

            var range = CreateSettingButton("VoiceRange", parent, "PROXIMITY RANGE", out _voiceRangeValue, VoiceAccent);
            SetRect(range.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(0.49f, 0.42f));
            range.onClick.AddListener(() => SetVoiceRange(WofUserSettingsRules.DefaultVoiceProximityRange));
            AddControl(Pane.Voice, range, direction => SetVoiceRange(_settings.voiceProximityRange + direction * 2f));

            var status = CreateCard(parent, "ACTIVITY / STATUS", VoiceAccent);
            SetRect(status.GetComponent<RectTransform>(), new Vector2(0.51f, 0.08f), new Vector2(1f, 0.42f));
            _voiceStatus = CreateText("Status", status.transform,
                "STATUS: UNITY NETWORK VOICE TRANSPORT NOT PORTED\n\nVOICE REMAINS OFF; THESE SAVED VALUES DO NOT CLAIM A WORKING MICROPHONE SESSION.",
                14, TextAnchor.MiddleCenter, new Color32(253, 230, 138, 255));
            SetRect(_voiceStatus.rectTransform, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.86f));
        }

        private void BuildCharacterPane()
        {
            var parent = _paneRoots[(int)Pane.Character].transform;
            CreateSectionTitle(parent, "CHARACTER CUSTOMIZATION / BASE SPRITE", CharacterAccent);
            var previewCard = CreateCard(parent, "PREVIEW", CharacterAccent);
            SetRect(previewCard.GetComponent<RectTransform>(), new Vector2(0f, 0.04f), new Vector2(0.34f, 0.85f));
            var previewObject = new GameObject("CharacterPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewObject.transform.SetParent(previewCard.transform, false);
            var previewImage = previewObject.GetComponent<Image>();
            previewImage.raycastTarget = false;
            previewImage.preserveAspect = true;
            SetRect(previewImage.rectTransform, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.88f));
            _characterPreview = previewObject.AddComponent<WofLaunchWizardPreviewRenderer>();

            var controls = new (string field, string label, string[] options)[]
            {
                ("skinColor", "SKIN COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("topColor", "TOP COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("pantsColor", "PANTS COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("shoesColor", "SHOES COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("hatColor", "HAT COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("hairColor", "HAIR COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("facialHairColor", "FACIAL HAIR COLOR", WofCharacterCustomizationRules.ColorPresets),
                ("topStyle", "TOP STYLE", WofCharacterCustomizationRules.TopStyles),
                ("pantsStyle", "PANTS STYLE", WofCharacterCustomizationRules.PantsStyles),
                ("shoesStyle", "SHOES", WofCharacterCustomizationRules.ShoesStyles),
                ("hatStyle", "HAT", WofCharacterCustomizationRules.HatStyles),
                ("hairStyle", "HAIR", WofCharacterCustomizationRules.HairStyles),
                ("facialHairStyle", "FACIAL HAIR", WofCharacterCustomizationRules.FacialHairStyles),
                ("eyeStyle", "EYES", WofCharacterCustomizationRules.EyeStyles),
                ("mouthStyle", "MOUTH", WofCharacterCustomizationRules.MouthStyles)
            };
            for (var index = 0; index < controls.Length; index++)
            {
                var control = controls[index];
                var column = index % 3;
                var row = index / 3;
                var minX = 0.37f + column * 0.215f;
                var maxX = minX + 0.195f;
                var maxY = 0.84f - row * 0.16f;
                var minY = maxY - 0.13f;
                var button = CreateSettingButton(control.field, parent, control.label, out var value, CharacterAccent);
                SetRect(button.GetComponent<RectTransform>(), new Vector2(minX, minY), new Vector2(maxX, maxY));
                button.onClick.AddListener(() => CycleProfileField(control.field, control.options, 1));
                AddControl(Pane.Character, button, direction => CycleProfileField(control.field, control.options, direction));
                _characterValues[control.field] = value;
            }
        }

        private void AddControl(Pane pane, Button button, Action<int> adjustment)
        {
            _paneControls[(int)pane].Add(button);
            _idleColors[button] = NeutralButton;
            if (adjustment != null) _adjustments[button] = adjustment;
        }

        private void SetPane(Pane pane, bool focusTab)
        {
            _pane = pane;
            for (var index = 0; index < _paneRoots.Length; index++)
                _paneRoots[index].SetActive(index == (int)pane);

            _navigation.Clear();
            _navigation.AddRange(_tabs);
            _navigation.AddRange(_paneControls[(int)pane]);
            _navigation.Add(_backButton);
            RefreshTabColors();
            RefreshLabels();
            SetSelection(focusTab ? (int)pane : Mathf.Clamp(_selection, 0, _navigation.Count - 1));
            Debug.Log($"[WOF-AUTOMATION] SETTINGS_PANE pane={pane}");
        }

        private void CyclePane(int direction)
        {
            var next = ((int)_pane + direction + _paneRoots.Length) % _paneRoots.Length;
            SetPane((Pane)next, true);
        }

        private void SetSelection(int selection)
        {
            if (_navigation.Count == 0) return;
            _selection = (selection % _navigation.Count + _navigation.Count) % _navigation.Count;
            for (var index = 0; index < _navigation.Count; index++)
            {
                var button = _navigation[index];
                button.GetComponent<Image>().color = index == _selection
                    ? FocusedButton
                    : ResolveIdleColor(button);
            }
            RefreshTabColors();
            if (_selection >= 0 && _selection < _navigation.Count)
                _navigation[_selection].GetComponent<Image>().color = FocusedButton;
            EventSystem.current?.SetSelectedGameObject(_navigation[_selection].gameObject);
        }

        private void AdjustSelected(int direction)
        {
            if (_selection < 0 || _selection >= _navigation.Count) return;
            var button = _navigation[_selection];
            if (_adjustments.TryGetValue(button, out var adjustment)) adjustment(direction);
            else button.onClick.Invoke();
        }

        private void RefreshTabColors()
        {
            for (var index = 0; index < _tabs.Length; index++)
            {
                var baseColor = index == (int)_pane
                    ? new[] { VideoAccent, KeybindAccent, VoiceAccent, CharacterAccent }[index] * new Color(1f, 1f, 1f, 0.58f)
                    : ResolveIdleColor(_tabs[index]);
                _tabs[index].GetComponent<Image>().color = baseColor;
            }
        }

        private Color ResolveIdleColor(Button button)
        {
            return _idleColors.TryGetValue(button, out var color) ? color : NeutralButton;
        }

        private void ToggleFullscreen()
        {
            Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            RefreshLabels();
        }

        private void ToggleVSync()
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount > 0 ? 0 : 1;
            RefreshLabels();
        }

        private void CycleFrameLimit()
        {
            _frameLimitIndex = (_frameLimitIndex + 1) % 3;
            Application.targetFrameRate = _frameLimitIndex == 0 ? 60 : _frameLimitIndex == 1 ? 120 : -1;
            RefreshLabels();
        }

        private void SetMouseSensitivity(float value)
        {
            _settings.mouseSensitivity = Mathf.Clamp(value, 0.0005f, 0.006f);
            SaveAndApplySettings();
        }

        private void SetControllerSensitivity(float value)
        {
            _settings.controllerLookSensitivity = Mathf.Clamp(value, 0.8f, 6f);
            SaveAndApplySettings();
        }

        private void SetHudTextScale(float value)
        {
            _settings.hudTextScale = Mathf.Clamp(value, 0.75f, 1.45f);
            SaveAndApplySettings();
        }

        private void ToggleArrowLook()
        {
            _settings.keyboardArrowLookEnabled = !_settings.keyboardArrowLookEnabled;
            SaveAndApplySettings();
        }

        private void ReportVoiceUnavailable()
        {
            _settings.voiceChatEnabled = false;
            SaveAndApplySettings();
            if (_voiceStatus != null)
                _voiceStatus.text = "STATUS: UNITY NETWORK VOICE TRANSPORT NOT PORTED\n\nVOICE WAS NOT ENABLED. NO MICROPHONE OR NEARBY-PLAYER AUDIO IS BEING FAKED.";
            Debug.LogWarning("[WOF] Voice chat was not enabled because the Unity network voice transport has not been ported.");
        }

        private void ToggleVoiceInputMode()
        {
            _settings.voiceInputMode = _settings.voiceInputMode == "pushToTalk" ? "openMic" : "pushToTalk";
            SaveAndApplySettings();
        }

        private void SetVoiceVolume(float value)
        {
            _settings.voiceOutputVolume = Mathf.Clamp01(value);
            SaveAndApplySettings();
        }

        private void SetVoiceRange(float value)
        {
            _settings.voiceProximityRange = Mathf.Clamp(value, 8f, 64f);
            SaveAndApplySettings();
        }

        private void SaveAndApplySettings()
        {
            WofUserSettingsStore.Save(_settings);
            ApplyRuntimeSettings();
            RefreshLabels();
        }

        private void ApplyRuntimeSettings()
        {
            WofInputRouter.ConfigureLookSettings(
                _settings.mouseSensitivity,
                _settings.controllerLookSensitivity,
                _settings.keyboardArrowLookEnabled);
            WofInputRouter.ConfigureControllerBindings(_settings.controllerBindings);
            _hud?.SetTextScale(_settings.hudTextScale);
        }

        private void BeginControllerRemap(string action)
        {
            _remappingAction = action;
            _remapWaitingForRelease = true;
            RefreshLabels();
            Debug.Log($"[WOF-AUTOMATION] CONTROLLER_REMAP_WAIT action={action}");
        }

        private void CycleProfileField(string field, string[] options, int direction)
        {
            var next = WofCharacterCustomizationRules.Next(options, GetProfileField(field), direction);
            SetProfileField(field, next);
            SaveProfile();
        }

        private string GetProfileField(string field)
        {
            return field switch
            {
                "skinColor" => _profile.skinColor, "topColor" => _profile.topColor,
                "pantsColor" => _profile.pantsColor, "shoesColor" => _profile.shoesColor,
                "hatColor" => _profile.hatColor, "hairColor" => _profile.hairColor,
                "facialHairColor" => _profile.facialHairColor, "topStyle" => _profile.topStyle,
                "pantsStyle" => _profile.pantsStyle, "shoesStyle" => _profile.shoesStyle,
                "hatStyle" => _profile.hatStyle, "hairStyle" => _profile.hairStyle,
                "facialHairStyle" => _profile.facialHairStyle, "eyeStyle" => _profile.eyeStyle,
                "mouthStyle" => _profile.mouthStyle,
                _ => string.Empty
            };
        }

        private void SetProfileField(string field, string value)
        {
            switch (field)
            {
                case "skinColor": _profile.skinColor = value; break;
                case "topColor": _profile.topColor = value; break;
                case "pantsColor": _profile.pantsColor = value; break;
                case "shoesColor": _profile.shoesColor = value; break;
                case "hatColor": _profile.hatColor = value; break;
                case "hairColor": _profile.hairColor = value; break;
                case "facialHairColor": _profile.facialHairColor = value; break;
                case "topStyle": _profile.topStyle = value; break;
                case "pantsStyle": _profile.pantsStyle = value; break;
                case "shoesStyle": _profile.shoesStyle = value; break;
                case "hatStyle": _profile.hatStyle = value; break;
                case "hairStyle": _profile.hairStyle = value; break;
                case "facialHairStyle": _profile.facialHairStyle = value; break;
                case "eyeStyle": _profile.eyeStyle = value; break;
                case "mouthStyle": _profile.mouthStyle = value; break;
            }
        }

        private void SaveProfile()
        {
            WofSurvivalProfileStore.Save(_profile);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (_settings == null) return;
            if (_fullscreenValue != null) _fullscreenValue.text = Screen.fullScreen ? "FULLSCREEN" : "WINDOWED";
            if (_vSyncValue != null) _vSyncValue.text = QualitySettings.vSyncCount > 0 ? "ON" : "OFF";
            if (_frameLimitValue != null) _frameLimitValue.text = _frameLimitIndex == 0 ? "60" : _frameLimitIndex == 1 ? "120" : "UNCAPPED";
            if (_mouseValue != null) _mouseValue.text = $"{Mathf.RoundToInt(_settings.mouseSensitivity / WofUserSettingsRules.DefaultMouseSensitivity * 100f)}%";
            if (_controllerValue != null) _controllerValue.text = $"{Mathf.RoundToInt(_settings.controllerLookSensitivity / 2.65f * 100f)}%";
            if (_hudScaleValue != null) _hudScaleValue.text = $"{Mathf.RoundToInt(_settings.hudTextScale * 100f)}%";
            if (_arrowLookValue != null) _arrowLookValue.text = _settings.keyboardArrowLookEnabled ? "ENABLED" : "DISABLED";
            foreach (var pair in _bindingValues)
            {
                pair.Value.text = pair.Key == _remappingAction
                    ? "PRESS BUTTON"
                    : WofControllerButtons.Label(WofControllerBindingRules.GetButton(_settings.controllerBindings, pair.Key));
            }
            if (_voiceEnabledValue != null) _voiceEnabledValue.text = "UNAVAILABLE";
            if (_voiceInputValue != null) _voiceInputValue.text = _settings.voiceInputMode == "pushToTalk" ? "PRESS TO TALK" : "OPEN MIC";
            if (_voiceKeyValue != null) _voiceKeyValue.text = _settings.voicePushToTalkKey;
            if (_voiceVolumeValue != null) _voiceVolumeValue.text = $"{Mathf.RoundToInt(_settings.voiceOutputVolume * 100f)}%";
            if (_voiceRangeValue != null) _voiceRangeValue.text = $"{Mathf.RoundToInt(_settings.voiceProximityRange)}m";
            if (_profile == null) return;
            foreach (var pair in _characterValues)
            {
                var value = GetProfileField(pair.Key);
                pair.Value.text = pair.Key.EndsWith("Color", StringComparison.Ordinal)
                    ? value.ToUpperInvariant()
                    : WofLaunchRules.FormatOption(value).ToUpperInvariant();
            }
            _characterPreview?.Render(_profile);
        }

        private void CreateSectionTitle(Transform parent, string value, Color color)
        {
            var title = CreateText("SectionTitle", parent, value, 15, TextAnchor.MiddleLeft, color);
            SetRect(title.rectTransform, new Vector2(0f, 0.87f), new Vector2(1f, 1f));
        }

        private GameObject CreateCard(Transform parent, string title, Color color)
        {
            var card = CreatePanel(title.Replace(" ", string.Empty), parent, new Color32(12, 12, 17, 235));
            var outline = card.AddComponent<Outline>();
            outline.effectColor = color * new Color(1f, 1f, 1f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);
            var label = CreateText("Title", card.transform, title, 13, TextAnchor.MiddleLeft, color);
            SetRect(label.rectTransform, new Vector2(0.04f, 0.87f), new Vector2(0.96f, 0.99f));
            return card;
        }

        private Button CreateSettingButton(string name, Transform parent, string label, out Text value, Color accent)
        {
            var button = CreateButton(name, parent, label, NeutralButton, 15);
            var labelText = button.transform.Find("Label").GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleLeft;
            SetRect(labelText.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.63f, 1f));
            value = CreateText("Value", button.transform, string.Empty, 15, TextAnchor.MiddleRight, accent);
            SetRect(value.rectTransform, new Vector2(0.60f, 0f), new Vector2(0.96f, 1f));
            return button;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return item;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(7, size / 2);
            text.resizeTextMaxSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color, int fontSize)
        {
            var item = CreatePanel(name, parent, color);
            var button = item.AddComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            var text = CreateText("Label", item.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
