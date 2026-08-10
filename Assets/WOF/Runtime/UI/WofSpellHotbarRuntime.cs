using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WOF
{
    /// <summary>Restores React's independent ten-slot spell bars for both hands.</summary>
    public sealed class WofSpellHotbarRuntime : MonoBehaviour
    {
        public const int SlotCount = 10;
        private const float DirectionRepeatDelay = 0.24f;

        public static readonly WofSpellId[] ReactDefaultLeft =
        {
            WofSpellId.SpeedBoost,
            WofSpellId.Fireball,
            WofSpellId.IceShard,
            WofSpellId.ArcaneBeam,
            WofSpellId.Heal,
            WofSpellId.IceSpell,
            WofSpellId.RingsOfPower,
            WofSpellId.Lightning,
            WofSpellId.SmokeBomb,
            WofSpellId.Portal
        };

        public static readonly WofSpellId[] ReactDefaultRight =
        {
            WofSpellId.JumpBoost,
            WofSpellId.Lightning,
            WofSpellId.Portal,
            WofSpellId.Grab,
            WofSpellId.Tornado,
            WofSpellId.MeteorShower,
            WofSpellId.Fireball,
            WofSpellId.IceSpell,
            WofSpellId.SmokeBomb,
            WofSpellId.Kunai
        };

        [SerializeField] private WofHud hud;

        private WofSpellId[] _left = (WofSpellId[])ReactDefaultLeft.Clone();
        private WofSpellId[] _right = (WofSpellId[])ReactDefaultRight.Clone();
        private int _leftSelected;
        private int _rightSelected;
        private float _nextLeftRepeatAt;
        private float _nextRightRepeatAt;
        private WofPlayerController _localPlayer;
        private float _nextPlayerResolveAt;

        public static WofSpellHotbarRuntime Instance { get; private set; }

        public void ConfigureGeneratedView(WofHud generatedHud)
        {
            hud = generatedHud;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            RefreshHud();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (hud == null || !hud.IsGameplayVisible || WofInputRouter.GameplaySuppressed ||
                WofSpellMenuRuntime.IsOpen || WofNavigationMapRuntime.IsExpanded ||
                WofPauseAndScoreboardRuntime.IsAnyMenuOpen)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var slot = ReadPressedNumberSlot(keyboard);
            if (slot >= 0)
            {
                SelectSlot(keyboard?.qKey.isPressed == true ? WofHandSide.Right : WofHandSide.Left, slot);
            }

            var gamepad = Gamepad.current;
            if (!WofInputRouter.IsControllerGameplayActive(gamepad)) return;
            UpdateControllerHand(gamepad, WofHandSide.Left, gamepad.leftShoulder.isPressed, ref _nextLeftRepeatAt);
            UpdateControllerHand(gamepad, WofHandSide.Right, gamepad.rightShoulder.isPressed, ref _nextRightRepeatAt);
        }

        public int GetSelectedSlot(WofHandSide hand)
        {
            return hand == WofHandSide.Left ? _leftSelected : _rightSelected;
        }

        public WofSpellId GetSlotSpell(WofHandSide hand, int slot)
        {
            return (hand == WofHandSide.Left ? _left : _right)[Mathf.Clamp(slot, 0, SlotCount - 1)];
        }

        public void AssignSpellToSelectedSlot(WofHandSide hand, WofSpellId spell)
        {
            AssignSpellToSlot(hand, GetSelectedSlot(hand), spell);
        }

        public void AssignSpellToSlot(WofHandSide hand, int slot, WofSpellId spell)
        {
            if (!WofSpellLoadout.IsValid((int)spell)) return;
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            var bar = hand == WofHandSide.Left ? _left : _right;
            bar[slot] = spell;
            SelectSlot(hand, slot);
            Debug.Log($"[WOF-AUTOMATION] SPELL_HOTBAR_ASSIGNED hand={hand} slot={slot + 1} spell={spell}");
        }

        public void SelectSlot(WofHandSide hand, int slot)
        {
            slot = WrapSlot(slot);
            if (hand == WofHandSide.Left) _leftSelected = slot;
            else _rightSelected = slot;
            ResolveLocalPlayer();
            var spell = GetSlotSpell(hand, slot);
            _localPlayer?.EquipSpell(hand, spell);
            RefreshHud();
            Debug.Log($"[WOF-AUTOMATION] SPELL_HOTBAR_SELECTED hand={hand} slot={slot + 1} spell={spell}");
        }

        internal static int WrapSlot(int slot)
        {
            return ((slot % SlotCount) + SlotCount) % SlotCount;
        }

        internal static int ReadPressedNumberSlot(Keyboard keyboard)
        {
            if (keyboard == null) return -1;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) return 6;
            if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) return 7;
            if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame) return 8;
            if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame) return 9;
            return -1;
        }

        private void UpdateControllerHand(
            Gamepad gamepad,
            WofHandSide hand,
            bool bumperHeld,
            ref float nextRepeatAt)
        {
            var bumperPressed = hand == WofHandSide.Left
                ? gamepad.leftShoulder.wasPressedThisFrame
                : gamepad.rightShoulder.wasPressedThisFrame;
            if (!bumperHeld)
            {
                nextRepeatAt = 0f;
                return;
            }

            var direction = gamepad.dpad.left.isPressed ? -1 : 1;
            var directionalHeld = gamepad.dpad.left.isPressed || gamepad.dpad.right.isPressed;
            var directionalPressed = gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame;
            var shouldAdvance = bumperPressed || directionalPressed ||
                                directionalHeld && nextRepeatAt > 0f && Time.unscaledTime >= nextRepeatAt;
            if (!shouldAdvance) return;
            SelectSlot(hand, GetSelectedSlot(hand) + direction);
            nextRepeatAt = Time.unscaledTime + DirectionRepeatDelay;
        }

        private void ResolveLocalPlayer()
        {
            if (_localPlayer != null || Time.unscaledTime < _nextPlayerResolveAt) return;
            _nextPlayerResolveAt = Time.unscaledTime + 0.25f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            _localPlayer = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
        }

        private void RefreshHud()
        {
            hud?.SetHotbarSelection(_leftSelected, _rightSelected);
        }
    }
}
