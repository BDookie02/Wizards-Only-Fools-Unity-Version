using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    public enum WofLaunchStage
    {
        Save,
        NewWizard,
        Multiplayer,
        Lobby
    }

    public sealed class WofLaunchFlow : MonoBehaviour
    {
        private static readonly string[] ColorPresets =
        {
            "#d6cf91", "#8d5524", "#c68642", "#f1c27d", "#ffdbac",
            "#f472b6", "#60a5fa", "#22c55e", "#facc15", "#f8fafc"
        };

        private static readonly string[] HatStyles =
        {
            "none", "wizard", "floppy-wizard", "cap", "hood", "pharaoh"
        };

        private static readonly string[] HairStyles =
        {
            "none", "short", "bob", "spikes", "long"
        };

        [SerializeField] private WofBootstrap bootstrap;
        [SerializeField] private GameObject saveStage;
        [SerializeField] private GameObject newWizardStage;
        [SerializeField] private GameObject multiplayerStage;
        [SerializeField] private GameObject lobbyStage;

        [Header("Save")]
        [SerializeField] private Button newButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Text continueButtonLabel;
        [SerializeField] private Button multiplayerButton;

        [Header("New wizard")]
        [SerializeField] private WofLaunchWizardPreviewRenderer wizardPreview;
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private Button outfitButton;
        [SerializeField] private Text outfitButtonLabel;
        [SerializeField] private Button skinButton;
        [SerializeField] private Text skinButtonLabel;
        [SerializeField] private Button hairColorButton;
        [SerializeField] private Text hairColorButtonLabel;
        [SerializeField] private Button hatButton;
        [SerializeField] private Text hatButtonLabel;
        [SerializeField] private Button hairButton;
        [SerializeField] private Text hairButtonLabel;
        [SerializeField] private Button startSoloButton;
        [SerializeField] private Button startSurvivalMultiplayerButton;
        [SerializeField] private Button newBackButton;

        [Header("Multiplayer")]
        [SerializeField] private Button customLobbyButton;
        [SerializeField] private Button survivalMultiplayerButton;
        [SerializeField] private Button multiplayerBackButton;

        [Header("Lobby")]
        [SerializeField] private Text lobbyTitle;
        [SerializeField] private InputField inviteCodeInput;
        [SerializeField] private InputField mobileLinkInput;
        [SerializeField] private Button createLobbyButton;
        [SerializeField] private Text createLobbyButtonLabel;
        [SerializeField] private Button copyMobileLinkButton;
        [SerializeField] private Text copyMobileLinkButtonLabel;
        [SerializeField] private Button lobbyBackButton;
        [SerializeField] private Text launchStatus;

        private WofSurvivalProfile _profile;
        private WofLaunchStage _stage;
        private bool _survivalLobby;
        private int _outfitColorIndex;
        private int _skinColorIndex;
        private int _hairColorIndex;
        private int _hatStyleIndex;
        private int _hairStyleIndex;
        private string _outfitColor = "#7c3aed";
        private string _skinColor = "#d6cf91";
        private string _hairColor = "#3f2a1d";
        private string _hatStyle = "floppy-wizard";
        private string _hairStyle = "none";
        private bool _initialized;
        private bool _publicSessionOperationInProgress;
        private Button _lanHostButton;

        public WofLaunchStage Stage => _stage;

        private void Awake()
        {
            newButton?.onClick.AddListener(ShowNewWizard);
            continueButton?.onClick.AddListener(ContinueProfile);
            multiplayerButton?.onClick.AddListener(ShowMultiplayer);
            outfitButton?.onClick.AddListener(CycleOutfitColor);
            skinButton?.onClick.AddListener(CycleSkinColor);
            hairColorButton?.onClick.AddListener(CycleHairColor);
            hatButton?.onClick.AddListener(CycleHatStyle);
            hairButton?.onClick.AddListener(CycleHairStyle);
            startSoloButton?.onClick.AddListener(StartNewSolo);
            startSurvivalMultiplayerButton?.onClick.AddListener(StartNewSurvivalMultiplayer);
            newBackButton?.onClick.AddListener(ShowSave);
            customLobbyButton?.onClick.AddListener(ShowCustomLobby);
            survivalMultiplayerButton?.onClick.AddListener(ShowSurvivalLobby);
            multiplayerBackButton?.onClick.AddListener(ShowSave);
            createLobbyButton?.onClick.AddListener(CreateLobby);
            copyMobileLinkButton?.onClick.AddListener(JoinLobby);
            inviteCodeInput?.onSubmit.AddListener(SubmitInviteCode);
            lobbyBackButton?.onClick.AddListener(ShowMultiplayer);
            CreateLanHostControl();

            _profile = WofSurvivalProfileStore.Load();
            if (_profile != null)
            {
                ApplyProfile(_profile);
            }

            if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text))
            {
                playerNameInput.text = _profile?.playerName ?? WofLaunchRules.MakeWizardName();
            }

            if (inviteCodeInput != null)
            {
                inviteCodeInput.text = string.Empty;
            }

            RefreshOptionLabels();
            RefreshContinueButton();
            RefreshLobbyLink();
            ApplyAutomationPreviewProbe();
            _initialized = true;
        }

        private void OnDestroy()
        {
            inviteCodeInput?.onSubmit.RemoveListener(SubmitInviteCode);
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            ShowStage(ParseAutomationStage() ?? WofLaunchStage.Save);
        }

        private void Update()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var keyboardBack = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            var gamepad = Gamepad.current;
            var gamepadBack = WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuBack);
            var gamepadStart = WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.Pause);
            if (gamepadStart)
            {
                InvokeSelectedControllerAction();
                return;
            }

            if (!keyboardBack && !gamepadBack)
            {
                return;
            }

            if (_stage == WofLaunchStage.NewWizard || _stage == WofLaunchStage.Multiplayer)
            {
                ShowSave();
            }
            else if (_stage == WofLaunchStage.Lobby)
            {
                ShowMultiplayer();
            }
        }

        public void ShowSave()
        {
            RefreshContinueButton();
            ShowStage(WofLaunchStage.Save);
        }

        public void ShowNewWizard()
        {
            ShowStage(WofLaunchStage.NewWizard);
            if (playerNameInput != null && !WasControllerMenuSubmitThisFrame())
            {
                playerNameInput.ActivateInputField();
            }
        }

        public void ShowMultiplayer()
        {
            ShowStage(WofLaunchStage.Multiplayer);
        }

        private void ShowCustomLobby()
        {
            _survivalLobby = false;
            ShowLobby();
        }

        private void ShowSurvivalLobby()
        {
            if (_profile == null)
            {
                ShowNewWizard();
                return;
            }

            _survivalLobby = true;
            ShowLobby();
        }

        private void ShowLobby()
        {
            if (lobbyTitle != null)
            {
                lobbyTitle.text = _survivalLobby ? "SURVIVAL MULTIPLAYER" : "CUSTOM LOBBY";
            }

            if (createLobbyButtonLabel != null)
            {
                createLobbyButtonLabel.text = "PUBLIC HOST";
            }

            RefreshLobbyLink();
            ShowStage(WofLaunchStage.Lobby);
        }

        private void ShowStage(WofLaunchStage stage)
        {
            _stage = stage;
            saveStage?.SetActive(stage == WofLaunchStage.Save);
            newWizardStage?.SetActive(stage == WofLaunchStage.NewWizard);
            multiplayerStage?.SetActive(stage == WofLaunchStage.Multiplayer);
            lobbyStage?.SetActive(stage == WofLaunchStage.Lobby);

            Button selected = stage switch
            {
                WofLaunchStage.Save => newButton,
                WofLaunchStage.NewWizard => outfitButton,
                WofLaunchStage.Multiplayer => customLobbyButton,
                WofLaunchStage.Lobby => createLobbyButton,
                _ => null
            };
            if (selected != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }

            Debug.Log($"[WOF] LAUNCH_STAGE {stage}");
        }

        private void InvokeSelectedControllerAction()
        {
            var selectedObject = EventSystem.current?.currentSelectedGameObject;
            var selectedButton = selectedObject == null ? null : selectedObject.GetComponent<Button>();
            if (selectedButton == null || !selectedButton.IsInteractable() || !selectedButton.gameObject.activeInHierarchy)
            {
                selectedButton = _stage switch
                {
                    WofLaunchStage.Save => newButton,
                    WofLaunchStage.NewWizard => outfitButton,
                    WofLaunchStage.Multiplayer => customLobbyButton,
                    WofLaunchStage.Lobby => createLobbyButton,
                    _ => null
                };
            }

            if (selectedButton != null && selectedButton.IsInteractable() && selectedButton.gameObject.activeInHierarchy)
            {
                selectedButton.onClick.Invoke();
            }
        }

        private static bool WasControllerMenuSubmitThisFrame()
        {
            var gamepad = Gamepad.current;
            return gamepad != null &&
                   (WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuSelect) ||
                    WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.Pause));
        }

        private void ContinueProfile()
        {
            if (_profile == null)
            {
                return;
            }

            if (string.Equals(_profile.lastMode, "multiplayer-survival", StringComparison.OrdinalIgnoreCase))
            {
                _survivalLobby = true;
                ShowLobby();
                return;
            }

            bootstrap?.StartSolo();
        }

        private void StartNewSolo()
        {
            if (!TrySaveNewProfile("solo-survival"))
            {
                return;
            }

            bootstrap?.StartSolo();
        }

        private void StartNewSurvivalMultiplayer()
        {
            if (!TrySaveNewProfile("multiplayer-survival"))
            {
                return;
            }

            _survivalLobby = true;
            ShowLobby();
        }

        private bool TrySaveNewProfile(string mode)
        {
            var playerName = WofLaunchRules.SanitizePlayerName(playerNameInput?.text);
            if (playerName.Length < 2)
            {
                SetStatus("ENTER AT LEAST TWO NAME CHARACTERS");
                return false;
            }

            _profile = new WofSurvivalProfile
            {
                playerName = playerName,
                topColor = _outfitColor,
                skinColor = _skinColor,
                hairColor = _hairColor,
                hatStyle = _hatStyle,
                hairStyle = _hairStyle,
                survivalLevel = 1,
                survivalXp = 0,
                lastMode = mode
            };
            var saved = WofSurvivalProfileStore.Save(_profile);
            SetStatus(saved ? "SURVIVAL PROFILE SAVED" : "SURVIVAL PROFILE COULD NOT BE SAVED");
            RefreshContinueButton();
            return saved;
        }

        private async void CreateLobby()
        {
            if (_publicSessionOperationInProgress)
            {
                return;
            }

            if (_survivalLobby && _profile != null)
            {
                _profile.lastMode = "multiplayer-survival";
                WofSurvivalProfileStore.Save(_profile);
            }

            bootstrap?.SetSurvivalSession(_survivalLobby);
            if (bootstrap == null)
            {
                SetStatus(WofPublicSessionRules.NetworkConfigurationRequired);
                return;
            }

            _publicSessionOperationInProgress = true;
            SetPublicSessionControlsInteractable(false);
            try
            {
                var joinCode = await bootstrap.StartPublicHostAsync();
                if (string.IsNullOrEmpty(joinCode))
                {
                    return;
                }

                if (inviteCodeInput != null)
                {
                    inviteCodeInput.text = joinCode;
                }

                if (mobileLinkInput != null)
                {
                    mobileLinkInput.text = $"PUBLIC INVITE CODE: {joinCode}";
                }

                if (copyMobileLinkButtonLabel != null)
                {
                    copyMobileLinkButtonLabel.text = "COPY INVITE CODE";
                }
            }
            finally
            {
                _publicSessionOperationInProgress = false;
                SetPublicSessionControlsInteractable(true);
            }
        }

        private void CreateLanLobby()
        {
            if (_survivalLobby && _profile != null)
            {
                _profile.lastMode = "multiplayer-survival";
                WofSurvivalProfileStore.Save(_profile);
            }

            bootstrap?.SetSurvivalSession(_survivalLobby);
            bootstrap?.StartHost();
        }

        private async void JoinLobby()
        {
            var joinCode = WofPublicSessionRules.NormalizeJoinCode(inviteCodeInput?.text);
            if (bootstrap != null && bootstrap.Mode != WofSessionMode.None)
            {
                if (string.IsNullOrEmpty(joinCode))
                {
                    return;
                }

                GUIUtility.systemCopyBuffer = joinCode;
                if (copyMobileLinkButtonLabel != null)
                {
                    copyMobileLinkButtonLabel.text = "INVITE CODE COPIED";
                }
                return;
            }

            if (_publicSessionOperationInProgress)
            {
                return;
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                SetStatus(WofPublicSessionRules.JoinCodeRequired);
                return;
            }

            bootstrap?.SetSurvivalSession(_survivalLobby);
            if (bootstrap != null)
            {
                _publicSessionOperationInProgress = true;
                SetPublicSessionControlsInteractable(false);
                try
                {
                    await bootstrap.StartPublicClientAsync(joinCode);
                }
                finally
                {
                    _publicSessionOperationInProgress = false;
                    SetPublicSessionControlsInteractable(true);
                }
            }
        }

        private void SubmitInviteCode(string _)
        {
            if (inviteCodeInput == null || inviteCodeInput.wasCanceled)
            {
                return;
            }

            inviteCodeInput.DeactivateInputField();
            if (copyMobileLinkButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(copyMobileLinkButton.gameObject);
            }

            JoinLobby();
        }

        private void SetPublicSessionControlsInteractable(bool interactable)
        {
            if (createLobbyButton != null)
            {
                createLobbyButton.interactable = interactable;
            }

            if (copyMobileLinkButton != null)
            {
                copyMobileLinkButton.interactable = interactable;
            }

            if (inviteCodeInput != null)
            {
                inviteCodeInput.interactable = interactable;
            }
        }

        private void RefreshLobbyLink()
        {
            if (createLobbyButtonLabel != null)
            {
                createLobbyButtonLabel.text = "PUBLIC HOST";
            }

            var inviteLabel = inviteCodeInput?.transform.parent.Find("InviteCodeLabel")?.GetComponent<Text>();
            if (inviteLabel != null)
            {
                inviteLabel.text = "PUBLIC INVITE CODE";
            }

            var sessionLabel = mobileLinkInput?.transform.parent.Find("MobileLinkLabel")?.GetComponent<Text>();
            if (sessionLabel != null)
            {
                sessionLabel.text = "PUBLIC SESSION";
            }

            if (mobileLinkInput != null)
            {
                mobileLinkInput.text = "PUBLIC RELAY: CREATE OR JOIN";
            }

            if (copyMobileLinkButtonLabel != null)
            {
                copyMobileLinkButtonLabel.text = "JOIN INVITE CODE";
            }
        }

        private void CreateLanHostControl()
        {
            if (createLobbyButton == null || _lanHostButton != null)
            {
                return;
            }

            var createRect = createLobbyButton.GetComponent<RectTransform>();
            createRect.anchorMin = new Vector2(0.08f, 0.30f);
            createRect.anchorMax = new Vector2(0.60f, 0.41f);
            createRect.offsetMin = Vector2.zero;
            createRect.offsetMax = Vector2.zero;

            _lanHostButton = Instantiate(createLobbyButton, createLobbyButton.transform.parent);
            _lanHostButton.gameObject.name = "LanHostButton";
            _lanHostButton.onClick.RemoveAllListeners();
            _lanHostButton.onClick.AddListener(CreateLanLobby);
            var lanRect = _lanHostButton.GetComponent<RectTransform>();
            lanRect.anchorMin = new Vector2(0.63f, 0.30f);
            lanRect.anchorMax = new Vector2(0.92f, 0.41f);
            lanRect.offsetMin = Vector2.zero;
            lanRect.offsetMax = Vector2.zero;
            var label = _lanHostButton.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = "LAN HOST";
            }
        }

        private void CycleOutfitColor()
        {
            _outfitColorIndex = NextIndex(ColorPresets, _outfitColor, _outfitColorIndex);
            _outfitColor = ColorPresets[_outfitColorIndex];
            RefreshOptionLabels();
        }

        private void CycleSkinColor()
        {
            _skinColorIndex = NextIndex(ColorPresets, _skinColor, _skinColorIndex);
            _skinColor = ColorPresets[_skinColorIndex];
            RefreshOptionLabels();
        }

        private void CycleHairColor()
        {
            _hairColorIndex = NextIndex(ColorPresets, _hairColor, _hairColorIndex);
            _hairColor = ColorPresets[_hairColorIndex];
            RefreshOptionLabels();
        }

        private void CycleHatStyle()
        {
            _hatStyleIndex = NextIndex(HatStyles, _hatStyle, _hatStyleIndex);
            _hatStyle = HatStyles[_hatStyleIndex];
            RefreshOptionLabels();
        }

        private void CycleHairStyle()
        {
            _hairStyleIndex = NextIndex(HairStyles, _hairStyle, _hairStyleIndex);
            _hairStyle = HairStyles[_hairStyleIndex];
            RefreshOptionLabels();
        }

        private static int NextIndex(string[] values, string current, int cachedIndex)
        {
            var index = Array.IndexOf(values, current);
            if (index < 0)
            {
                index = Mathf.Clamp(cachedIndex, 0, values.Length - 1);
            }

            return WofLaunchRules.WrapOptionIndex(index + 1, values.Length);
        }

        private void RefreshOptionLabels()
        {
            SetOptionLabel(outfitButtonLabel, "OUTFIT", _outfitColor);
            SetOptionLabel(skinButtonLabel, "SKIN", _skinColor);
            SetOptionLabel(hairColorButtonLabel, "HAIR COLOR", _hairColor);
            SetOptionLabel(hatButtonLabel, "HAT", WofLaunchRules.FormatOption(_hatStyle));
            SetOptionLabel(hairButtonLabel, "HAIR", WofLaunchRules.FormatOption(_hairStyle));
            wizardPreview?.Render(_outfitColor, _skinColor, _hairColor, _hatStyle, _hairStyle);
        }

        private void RefreshContinueButton()
        {
            if (continueButton != null)
            {
                continueButton.interactable = _profile != null;
            }

            if (continueButtonLabel != null)
            {
                continueButtonLabel.text = _profile == null
                    ? "CONTINUE"
                    : $"CONTINUE LVL {_profile.survivalLevel} - {_profile.playerName}";
                continueButtonLabel.color = _profile == null
                    ? new Color32(207, 250, 254, 115)
                    : new Color32(207, 250, 254, 255);
            }
        }

        private void ApplyProfile(WofSurvivalProfile profile)
        {
            _outfitColor = profile.topColor;
            _skinColor = profile.skinColor;
            _hairColor = profile.hairColor;
            _hatStyle = profile.hatStyle;
            _hairStyle = profile.hairStyle;
        }

        private void SetStatus(string value)
        {
            if (launchStatus != null)
            {
                launchStatus.text = value;
            }

            Debug.Log($"[WOF] {value}");
        }

        private static void SetOptionLabel(Text target, string title, string value)
        {
            if (target != null)
            {
                target.text = $"{title}\n{value}";
            }
        }

        private static WofLaunchStage? ParseAutomationStage()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                const string prefix = "--wof-launch-stage=";
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return argument.Substring(prefix.Length).Trim().ToLowerInvariant() switch
                {
                    "save" => WofLaunchStage.Save,
                    "new" => WofLaunchStage.NewWizard,
                    "multiplayer" => WofLaunchStage.Multiplayer,
                    "lobby" => WofLaunchStage.Lobby,
                    _ => WofLaunchStage.Save
                };
            }

            return null;
        }

        private void ApplyAutomationPreviewProbe()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.Equals("--wof-launch-preview-probe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                outfitButton?.onClick.Invoke();
                skinButton?.onClick.Invoke();
                hairColorButton?.onClick.Invoke();
                hatButton?.onClick.Invoke();
                hairButton?.onClick.Invoke();
                Debug.Log($"[WOF-AUTOMATION] LAUNCH_PREVIEW_PROBE top={_outfitColor} skin={_skinColor} hairColor={_hairColor} hat={_hatStyle} hair={_hairStyle}");
                return;
            }
        }
    }
}
