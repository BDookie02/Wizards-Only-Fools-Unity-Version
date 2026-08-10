using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace WOF
{
    public static class WofControllerActions
    {
        public const string LeftCast = "leftCast";
        public const string RightCast = "rightCast";
        public const string Jump = "jump";
        public const string Slide = "slide";
        public const string Sprint = "sprint";
        public const string Inventory = "inventory";
        public const string Interact = "interact";
        public const string SpellMenu = "spellMenu";
        public const string Map = "map";
        public const string Scoreboard = "scoreboard";
        public const string Pause = "pause";
        public const string MenuSelect = "menuSelect";
        public const string MenuBack = "menuBack";
        public const string LeftHotbar = "leftHotbar";
        public const string RightHotbar = "rightHotbar";
        public const string VoicePushToTalk = "voicePushToTalk";

        public static readonly string[] All =
        {
            LeftCast, RightCast, Jump, Slide, Sprint, Inventory, Interact, SpellMenu,
            Map, Scoreboard, Pause, MenuSelect, MenuBack, LeftHotbar, RightHotbar,
            VoicePushToTalk
        };

        public static string Label(string action)
        {
            return action switch
            {
                LeftCast => "LEFT CAST",
                RightCast => "RIGHT CAST",
                Jump => "JUMP / THRUSTER",
                Slide => "SLIDE",
                Sprint => "SPRINT TOGGLE",
                Inventory => "INVENTORY",
                Interact => "INTERACT",
                SpellMenu => "SPELL BOOK",
                Map => "MAP",
                Scoreboard => "PLAYER LIST",
                Pause => "PAUSE / RESUME",
                MenuSelect => "MENU SELECT",
                MenuBack => "MENU BACK",
                LeftHotbar => "LEFT HOTBAR",
                RightHotbar => "RIGHT HOTBAR",
                VoicePushToTalk => "VOICE PTT",
                _ => action ?? string.Empty
            };
        }
    }

    public static class WofControllerButtons
    {
        public const string A = "a";
        public const string B = "b";
        public const string X = "x";
        public const string Y = "y";
        public const string LeftBumper = "leftBumper";
        public const string RightBumper = "rightBumper";
        public const string LeftTrigger = "leftTrigger";
        public const string RightTrigger = "rightTrigger";
        public const string Back = "back";
        public const string Start = "start";
        public const string LeftStick = "leftStick";
        public const string RightStick = "rightStick";
        public const string DpadUp = "dpadUp";
        public const string DpadDown = "dpadDown";
        public const string DpadLeft = "dpadLeft";
        public const string DpadRight = "dpadRight";

        public static readonly string[] All =
        {
            A, B, X, Y, LeftBumper, RightBumper, LeftTrigger, RightTrigger,
            Back, Start, LeftStick, RightStick, DpadUp, DpadDown, DpadLeft, DpadRight
        };

        public static string Label(string button)
        {
            return button switch
            {
                A => "A", B => "B", X => "X", Y => "Y",
                LeftBumper => "LB", RightBumper => "RB",
                LeftTrigger => "LT", RightTrigger => "RT",
                Back => "SELECT", Start => "START",
                LeftStick => "LEFT STICK", RightStick => "RIGHT STICK",
                DpadUp => "D-PAD UP", DpadDown => "D-PAD DOWN",
                DpadLeft => "D-PAD LEFT", DpadRight => "D-PAD RIGHT",
                _ => button ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class WofControllerBindingEntry
    {
        public string action;
        public string button;
    }

    public static class WofControllerBindingRules
    {
        private static readonly Dictionary<string, string> Defaults = new(StringComparer.Ordinal)
        {
            [WofControllerActions.LeftCast] = WofControllerButtons.LeftTrigger,
            [WofControllerActions.RightCast] = WofControllerButtons.RightTrigger,
            [WofControllerActions.Jump] = WofControllerButtons.A,
            [WofControllerActions.Slide] = WofControllerButtons.B,
            [WofControllerActions.Sprint] = WofControllerButtons.LeftStick,
            [WofControllerActions.Inventory] = WofControllerButtons.DpadRight,
            [WofControllerActions.Interact] = WofControllerButtons.X,
            [WofControllerActions.SpellMenu] = WofControllerButtons.DpadUp,
            [WofControllerActions.Map] = WofControllerButtons.DpadLeft,
            [WofControllerActions.Scoreboard] = WofControllerButtons.Back,
            [WofControllerActions.Pause] = WofControllerButtons.Start,
            [WofControllerActions.MenuSelect] = WofControllerButtons.A,
            [WofControllerActions.MenuBack] = WofControllerButtons.B,
            [WofControllerActions.LeftHotbar] = WofControllerButtons.LeftBumper,
            [WofControllerActions.RightHotbar] = WofControllerButtons.RightBumper,
            [WofControllerActions.VoicePushToTalk] = WofControllerButtons.RightStick
        };

        public static WofControllerBindingEntry[] CreateDefaults()
        {
            var entries = new WofControllerBindingEntry[WofControllerActions.All.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var action = WofControllerActions.All[index];
                entries[index] = new WofControllerBindingEntry { action = action, button = Defaults[action] };
            }
            return entries;
        }

        public static WofControllerBindingEntry[] Normalize(WofControllerBindingEntry[] entries)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || !Defaults.ContainsKey(entry.action) ||
                        Array.IndexOf(WofControllerButtons.All, entry.button) < 0) continue;
                    values[entry.action] = entry.button;
                }
            }

            var normalized = CreateDefaults();
            foreach (var entry in normalized)
            {
                if (values.TryGetValue(entry.action, out var button)) entry.button = button;
            }
            return normalized;
        }

        public static string GetButton(WofControllerBindingEntry[] entries, string action)
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry != null && entry.action == action && Array.IndexOf(WofControllerButtons.All, entry.button) >= 0)
                        return entry.button;
                }
            }
            return Defaults.TryGetValue(action, out var fallback) ? fallback : string.Empty;
        }

        public static void SetButton(WofControllerBindingEntry[] entries, string action, string button)
        {
            if (entries == null || Array.IndexOf(WofControllerButtons.All, button) < 0) return;
            foreach (var entry in entries)
            {
                if (entry == null || entry.action != action) continue;
                entry.button = button;
                return;
            }
        }
    }

    public static class WofControllerBindings
    {
        private static WofControllerBindingEntry[] s_Entries = WofControllerBindingRules.CreateDefaults();
        private static bool s_Configured;

        public static void Configure(WofControllerBindingEntry[] entries)
        {
            s_Entries = WofControllerBindingRules.Normalize(entries);
            s_Configured = true;
        }

        public static string GetButton(string action)
        {
            EnsureConfigured();
            return WofControllerBindingRules.GetButton(s_Entries, action);
        }

        public static bool IsPressed(Gamepad gamepad, string action, float threshold = 0.5f)
        {
            var control = Resolve(gamepad, GetButton(action));
            return control != null && control.ReadUnprocessedValue() >= threshold;
        }

        public static bool WasPressedThisFrame(Gamepad gamepad, string action)
        {
            return Resolve(gamepad, GetButton(action))?.wasPressedThisFrame ?? false;
        }

        public static bool WasReleasedThisFrame(Gamepad gamepad, string action)
        {
            return Resolve(gamepad, GetButton(action))?.wasReleasedThisFrame ?? false;
        }

        public static bool TryGetPressedButton(Gamepad gamepad, out string button)
        {
            foreach (var candidate in WofControllerButtons.All)
            {
                if (!(Resolve(gamepad, candidate)?.wasPressedThisFrame ?? false)) continue;
                button = candidate;
                return true;
            }
            button = string.Empty;
            return false;
        }

        public static bool AreAllReleased(Gamepad gamepad)
        {
            if (gamepad == null) return true;
            foreach (var candidate in WofControllerButtons.All)
            {
                if (Resolve(gamepad, candidate)?.isPressed ?? false) return false;
            }
            return true;
        }

        private static ButtonControl Resolve(Gamepad gamepad, string button)
        {
            if (gamepad == null) return null;
            return button switch
            {
                WofControllerButtons.A => gamepad.buttonSouth,
                WofControllerButtons.B => gamepad.buttonEast,
                WofControllerButtons.X => gamepad.buttonWest,
                WofControllerButtons.Y => gamepad.buttonNorth,
                WofControllerButtons.LeftBumper => gamepad.leftShoulder,
                WofControllerButtons.RightBumper => gamepad.rightShoulder,
                WofControllerButtons.LeftTrigger => gamepad.leftTrigger,
                WofControllerButtons.RightTrigger => gamepad.rightTrigger,
                WofControllerButtons.Back => gamepad.selectButton,
                WofControllerButtons.Start => gamepad.startButton,
                WofControllerButtons.LeftStick => gamepad.leftStickButton,
                WofControllerButtons.RightStick => gamepad.rightStickButton,
                WofControllerButtons.DpadUp => gamepad.dpad.up,
                WofControllerButtons.DpadDown => gamepad.dpad.down,
                WofControllerButtons.DpadLeft => gamepad.dpad.left,
                WofControllerButtons.DpadRight => gamepad.dpad.right,
                _ => null
            };
        }

        private static void EnsureConfigured()
        {
            if (s_Configured) return;
            Configure(WofUserSettingsStore.Load().controllerBindings);
        }
    }
}
