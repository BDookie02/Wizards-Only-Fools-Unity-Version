using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    public enum WofHandSide
    {
        Left,
        Right
    }

    public static class WofInputRouter
    {
        private const float LegacyMouseAxisScale = 0.1f;
        internal const float ControllerStickDeadzone = 0.22f;
        internal const float ControllerTriggerThreshold = 0.45f;
        internal const float ControllerArmButtonThreshold = 0.35f;
        internal const float ControllerLookSensitivityRadiansPerSecond = 5.3f;
        internal const float ControllerLookVerticalMultiplier = 0.78f;
        internal const float KeyboardArrowLookRadiansPerSecond = 2.65f;
        private static readonly List<RaycastResult> s_UiRaycastResults = new();
        private static Vector2 s_MobileMove;
        private static Vector2 s_MobileLook;
        private static bool s_MobileLeftCastQueued;
        private static bool s_MobileRightCastQueued;
        private static bool s_MobileJumpHeld;
        private static bool s_ControllerGameplayRequested;
        private static bool s_ControllerGameplayArmed;
        private static bool s_ControllerLeftCastHeld;
        private static bool s_ControllerRightCastHeld;
        private static bool s_ControllerSprintHeld;
        private static bool s_ControllerSprintLatched;
        private static bool s_GameplaySuppressed;
        private static float s_MouseSensitivity = WofUserSettingsRules.DefaultMouseSensitivity;
        private static float s_ControllerLookSensitivity = WofUserSettingsRules.DefaultControllerLookSensitivity;
        private static bool s_KeyboardArrowLookEnabled = true;

        public static bool GameplaySuppressed => s_GameplaySuppressed;

        public static Vector2 ReadMove()
        {
            if (s_GameplaySuppressed)
            {
                return Vector2.zero;
            }
            var keyboard = Keyboard.current;
            var keyboardMove = keyboard == null
                ? Vector2.zero
                : new Vector2(
                    ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed),
                    ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed));
            var gamepad = Gamepad.current;
            var gamepadMove = IsControllerInputActive(gamepad)
                ? ResolveControllerStick(gamepad.leftStick.ReadUnprocessedValue())
                : Vector2.zero;
            return ResolveMove(keyboardMove, gamepadMove, s_MobileMove);
        }

        public static Vector2 ReadLook()
        {
            if (s_GameplaySuppressed)
            {
                s_MobileLook = Vector2.zero;
                return Vector2.zero;
            }
            var mouse = Mouse.current;
            var mouseScale = s_MouseSensitivity / WofUserSettingsRules.DefaultMouseSensitivity;
            var mouseDelta = mouse == null ? Vector2.zero : mouse.delta.ReadValue() * mouseScale;
            var gamepad = Gamepad.current;
            var controllerLook = IsControllerInputActive(gamepad)
                ? ResolveControllerLook(
                    ResolveControllerStick(gamepad.rightStick.ReadUnprocessedValue()),
                    Time.unscaledDeltaTime,
                    s_ControllerLookSensitivity)
                : Vector2.zero;
            var keyboard = Keyboard.current;
            var keyboardLook = s_KeyboardArrowLookEnabled && keyboard != null
                ? ResolveKeyboardArrowLook(
                    new Vector2(
                        ReadAxis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed),
                        ReadAxis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed)),
                    Time.unscaledDeltaTime,
                    mouseScale)
                : Vector2.zero;
            var look = ResolveLook(mouseDelta, controllerLook + keyboardLook, s_MobileLook);
            s_MobileLook = Vector2.zero;
            return look;
        }

        public static bool ReadJump()
        {
            if (s_GameplaySuppressed)
            {
                return false;
            }
            var gamepad = Gamepad.current;
            var controllerJump = IsControllerInputActive(gamepad) &&
                                 WofControllerBindings.IsPressed(gamepad, WofControllerActions.Jump, ControllerTriggerThreshold);
            return (Keyboard.current?.spaceKey.isPressed ?? false) || controllerJump || s_MobileJumpHeld;
        }

        public static bool ReadSprint(Vector2 move)
        {
            if (s_GameplaySuppressed)
            {
                s_ControllerSprintHeld = false;
                s_ControllerSprintLatched = false;
                return false;
            }
            var gamepad = Gamepad.current;
            var controllerActive = IsControllerInputActive(gamepad);
            var controllerSprintHeld = controllerActive &&
                                       WofControllerBindings.IsPressed(gamepad, WofControllerActions.Sprint, ControllerTriggerThreshold);
            var controllerSprintPressed = controllerSprintHeld && !s_ControllerSprintHeld;
            s_ControllerSprintHeld = controllerSprintHeld;
            s_ControllerSprintLatched = ResolveControllerSprintLatch(
                s_ControllerSprintLatched,
                controllerSprintPressed,
                move.sqrMagnitude > 0f);
            return (Keyboard.current?.leftShiftKey.isPressed ?? false) || s_ControllerSprintLatched;
        }

        public static bool ReadSlide()
        {
            if (s_GameplaySuppressed)
            {
                return false;
            }
            var gamepad = Gamepad.current;
            var controllerSlide = IsControllerInputActive(gamepad) &&
                                  WofControllerBindings.IsPressed(gamepad, WofControllerActions.Slide, ControllerTriggerThreshold);
            return (Keyboard.current?.cKey.isPressed ?? false) || controllerSlide;
        }

        public static bool ConsumeCast()
        {
            return ConsumeCast(out _);
        }

        public static bool ConsumeCast(out WofHandSide hand)
        {
            if (s_GameplaySuppressed)
            {
                s_MobileLeftCastQueued = false;
                s_MobileRightCastQueued = false;
                hand = WofHandSide.Right;
                return false;
            }
            var mouse = Mouse.current;
            var leftMousePressed = mouse?.leftButton.wasPressedThisFrame ?? false;
            var rightMousePressed = mouse?.rightButton.wasPressedThisFrame ?? false;
            var gamepad = Gamepad.current;
            var controllerActive = IsControllerInputActive(gamepad);
            var controllerLeftHeld = controllerActive &&
                                     WofControllerBindings.IsPressed(gamepad, WofControllerActions.LeftCast, ControllerTriggerThreshold);
            var controllerRightHeld = controllerActive &&
                                      WofControllerBindings.IsPressed(gamepad, WofControllerActions.RightCast, ControllerTriggerThreshold);
            var controllerLeftPressed = controllerLeftHeld && !s_ControllerLeftCastHeld;
            var controllerRightPressed = controllerRightHeld && !s_ControllerRightCastHeld;
            s_ControllerLeftCastHeld = controllerLeftHeld;
            s_ControllerRightCastHeld = controllerRightHeld;
            var cast = ResolveCastHand(
                leftMousePressed,
                rightMousePressed,
                (leftMousePressed || rightMousePressed) && IsMousePointerOverUi(mouse),
                controllerLeftPressed,
                controllerRightPressed,
                s_MobileLeftCastQueued,
                s_MobileRightCastQueued,
                out hand);
            s_MobileLeftCastQueued = false;
            s_MobileRightCastQueued = false;
            return cast;
        }

        public static bool HasTouchscreen => Touchscreen.current != null;

        public static float MouseSensitivity => s_MouseSensitivity;
        public static float ControllerLookSensitivity => s_ControllerLookSensitivity;
        public static bool KeyboardArrowLookEnabled => s_KeyboardArrowLookEnabled;

        public static void ConfigureLookSettings(float mouseSensitivity, float controllerLookSensitivity, bool keyboardArrowLookEnabled)
        {
            s_MouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.0005f, 0.006f);
            s_ControllerLookSensitivity = Mathf.Clamp(controllerLookSensitivity, 0.8f, 6f);
            s_KeyboardArrowLookEnabled = keyboardArrowLookEnabled;
        }

        public static void ConfigureControllerBindings(WofControllerBindingEntry[] entries)
        {
            WofControllerBindings.Configure(entries);
        }

        internal static bool IsControllerGameplayActive(Gamepad gamepad)
        {
            return IsControllerInputActive(gamepad);
        }

        public static void BeginControllerGameplay()
        {
            s_ControllerGameplayRequested = true;
            s_ControllerGameplayArmed = false;
            s_ControllerLeftCastHeld = false;
            s_ControllerRightCastHeld = false;
            s_ControllerSprintHeld = false;
            s_ControllerSprintLatched = false;
        }

        public static void EndControllerGameplay()
        {
            s_ControllerGameplayRequested = false;
            s_ControllerGameplayArmed = false;
            s_ControllerLeftCastHeld = false;
            s_ControllerRightCastHeld = false;
            s_ControllerSprintHeld = false;
            s_ControllerSprintLatched = false;
        }

        public static void SetGameplaySuppressed(bool suppressed)
        {
            s_GameplaySuppressed = suppressed;
            if (!suppressed)
            {
                return;
            }

            ResetTransientGameplayActions();
        }

        public static void ResetTransientGameplayActions()
        {
            ResetMobile();
            s_ControllerLeftCastHeld = false;
            s_ControllerRightCastHeld = false;
            s_ControllerSprintHeld = false;
            s_ControllerSprintLatched = false;
        }

        public static void SetMobileMove(Vector2 value) => s_MobileMove = Vector2.ClampMagnitude(value, 1f);
        public static void AddMobileLook(Vector2 delta) => s_MobileLook += delta;
        public static void QueueMobileCast(WofHandSide hand)
        {
            if (hand == WofHandSide.Left)
            {
                s_MobileLeftCastQueued = true;
            }
            else
            {
                s_MobileRightCastQueued = true;
            }
        }
        public static void SetMobileJump(bool held) => s_MobileJumpHeld = held;

        public static void ResetMobile()
        {
            s_MobileMove = Vector2.zero;
            s_MobileLook = Vector2.zero;
            s_MobileLeftCastQueued = false;
            s_MobileRightCastQueued = false;
            s_MobileJumpHeld = false;
        }

        internal static Vector2 ResolveMove(Vector2 keyboardMove, Vector2 mobileMove)
        {
            return ResolveMove(keyboardMove, Vector2.zero, mobileMove);
        }

        internal static Vector2 ResolveMove(Vector2 keyboardMove, Vector2 controllerMove, Vector2 mobileMove)
        {
            var combined = keyboardMove;
            if (controllerMove.sqrMagnitude > combined.sqrMagnitude)
            {
                combined = controllerMove;
            }
            if (mobileMove.sqrMagnitude > combined.sqrMagnitude)
            {
                combined = mobileMove;
            }
            return Vector2.ClampMagnitude(combined, 1f);
        }

        internal static bool ResolveCast(
            bool leftMousePressed,
            bool rightMousePressed,
            bool mousePointerOverUi,
            bool mobileLeftCastQueued,
            bool mobileRightCastQueued)
        {
            return ResolveCast(
                leftMousePressed,
                rightMousePressed,
                mousePointerOverUi,
                false,
                false,
                mobileLeftCastQueued,
                mobileRightCastQueued);
        }

        internal static bool ResolveCast(
            bool leftMousePressed,
            bool rightMousePressed,
            bool mousePointerOverUi,
            bool controllerLeftCastPressed,
            bool controllerRightCastPressed,
            bool mobileLeftCastQueued,
            bool mobileRightCastQueued)
        {
            return controllerLeftCastPressed ||
                   controllerRightCastPressed ||
                   mobileLeftCastQueued ||
                   mobileRightCastQueued ||
                   ((leftMousePressed || rightMousePressed) && !mousePointerOverUi);
        }

        internal static bool ResolveCastHand(
            bool leftMousePressed,
            bool rightMousePressed,
            bool mousePointerOverUi,
            bool mobileLeftCastQueued,
            bool mobileRightCastQueued,
            out WofHandSide hand)
        {
            return ResolveCastHand(
                leftMousePressed,
                rightMousePressed,
                mousePointerOverUi,
                false,
                false,
                mobileLeftCastQueued,
                mobileRightCastQueued,
                out hand);
        }

        internal static bool ResolveCastHand(
            bool leftMousePressed,
            bool rightMousePressed,
            bool mousePointerOverUi,
            bool controllerLeftCastPressed,
            bool controllerRightCastPressed,
            bool mobileLeftCastQueued,
            bool mobileRightCastQueued,
            out WofHandSide hand)
        {
            if (leftMousePressed && !mousePointerOverUi)
            {
                hand = WofHandSide.Left;
                return true;
            }

            if (rightMousePressed && !mousePointerOverUi)
            {
                hand = WofHandSide.Right;
                return true;
            }

            if (controllerLeftCastPressed || mobileLeftCastQueued)
            {
                hand = WofHandSide.Left;
                return true;
            }

            hand = WofHandSide.Right;
            return controllerRightCastPressed || mobileRightCastQueued;
        }

        internal static Vector2 ResolveLook(Vector2 mouseDelta, Vector2 mobileLook)
        {
            return ResolveLook(mouseDelta, Vector2.zero, mobileLook);
        }

        internal static Vector2 ResolveLook(Vector2 mouseDelta, Vector2 controllerLook, Vector2 mobileLook)
        {
            return mouseDelta * LegacyMouseAxisScale + controllerLook + mobileLook;
        }

        internal static float ApplyControllerDeadzone(float value, float deadzone = ControllerStickDeadzone)
        {
            if (!IsFinite(value) || !IsFinite(deadzone) || deadzone < 0f || deadzone >= 1f)
            {
                return 0f;
            }

            var magnitude = Mathf.Abs(value);
            if (magnitude <= deadzone)
            {
                return 0f;
            }

            return Mathf.Sign(value) * ((magnitude - deadzone) / (1f - deadzone));
        }

        internal static Vector2 ResolveControllerStick(Vector2 rawStick)
        {
            return new Vector2(
                ApplyControllerDeadzone(rawStick.x),
                ApplyControllerDeadzone(rawStick.y));
        }

        internal static bool ResolveControllerSprintLatch(
            bool wasLatched,
            bool sprintPressed,
            bool hasMovementInput)
        {
            if (!hasMovementInput)
            {
                return false;
            }

            return wasLatched || sprintPressed;
        }

        internal static Vector2 ResolveControllerLook(Vector2 deadzonedStick, float deltaSeconds)
        {
            return ResolveControllerLook(deadzonedStick, deltaSeconds, ControllerLookSensitivityRadiansPerSecond);
        }

        internal static Vector2 ResolveControllerLook(Vector2 deadzonedStick, float deltaSeconds, float sensitivityRadiansPerSecond)
        {
            if (!IsFinite(deadzonedStick) || !IsFinite(deltaSeconds) || !IsFinite(sensitivityRadiansPerSecond) ||
                deltaSeconds <= 0f || sensitivityRadiansPerSecond <= 0f)
            {
                return Vector2.zero;
            }

            var degreesPerSecond = sensitivityRadiansPerSecond * Mathf.Rad2Deg;
            var routerScale = degreesPerSecond * deltaSeconds / WofGameConstants.MouseSensitivity;
            return new Vector2(
                deadzonedStick.x * routerScale,
                deadzonedStick.y * routerScale * ControllerLookVerticalMultiplier);
        }

        internal static Vector2 ResolveKeyboardArrowLook(Vector2 arrowInput, float deltaSeconds, float sensitivityScale)
        {
            if (!IsFinite(arrowInput) || !IsFinite(deltaSeconds) || !IsFinite(sensitivityScale) ||
                deltaSeconds <= 0f || sensitivityScale <= 0f)
            {
                return Vector2.zero;
            }

            var routerScale = KeyboardArrowLookRadiansPerSecond * Mathf.Rad2Deg * deltaSeconds * sensitivityScale /
                              WofGameConstants.MouseSensitivity;
            return new Vector2(
                arrowInput.x * routerScale,
                arrowInput.y * routerScale * ControllerLookVerticalMultiplier);
        }

        internal static Vector2 ResolveJoystickValue(Vector2 localPosition, float radius)
        {
            return radius > 0f && IsFinite(localPosition) && IsFinite(radius)
                ? Vector2.ClampMagnitude(localPosition / radius, 1f)
                : Vector2.zero;
        }

        internal static bool ShouldShowTouchControls(
            bool isTouchGameplayDevice,
            bool forced,
            bool hasConnectedController)
        {
            return !hasConnectedController && (isTouchGameplayDevice || forced);
        }

        private static bool IsControllerInputActive(Gamepad gamepad)
        {
            if (!s_ControllerGameplayRequested || gamepad == null)
            {
                s_ControllerGameplayArmed = false;
                return false;
            }

            if (!s_ControllerGameplayArmed && AreControllerGameplayButtonsReleased(gamepad))
            {
                s_ControllerGameplayArmed = true;
                s_ControllerLeftCastHeld = false;
                s_ControllerRightCastHeld = false;
                s_ControllerSprintHeld = false;
                s_ControllerSprintLatched = false;
            }

            return s_ControllerGameplayArmed;
        }

        private static bool AreControllerGameplayButtonsReleased(Gamepad gamepad)
        {
            return !IsControllerButtonPressed(gamepad.buttonSouth, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.buttonEast, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.buttonWest, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.leftShoulder, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.rightShoulder, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.leftTrigger, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.rightTrigger, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.selectButton, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.startButton, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.leftStickButton, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.rightStickButton, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.dpad.up, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.dpad.down, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.dpad.left, ControllerArmButtonThreshold) &&
                   !IsControllerButtonPressed(gamepad.dpad.right, ControllerArmButtonThreshold);
        }

        private static bool IsControllerButtonPressed(UnityEngine.InputSystem.Controls.AxisControl control, float threshold)
        {
            return control != null && control.ReadUnprocessedValue() >= threshold;
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static bool IsMousePointerOverUi(Mouse mouse)
        {
            var eventSystem = EventSystem.current;
            if (mouse == null || eventSystem == null)
            {
                return false;
            }

            var pointer = new PointerEventData(eventSystem)
            {
                position = mouse.position.ReadValue()
            };
            s_UiRaycastResults.Clear();
            eventSystem.RaycastAll(pointer, s_UiRaycastResults);
            var isOverUi = false;
            foreach (var result in s_UiRaycastResults)
            {
                if (result.module is GraphicRaycaster)
                {
                    isOverUi = true;
                    break;
                }
            }
            s_UiRaycastResults.Clear();
            return isOverUi;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
