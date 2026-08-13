using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WOF
{
    internal static class WofGraveyardTraversalProbe
    {
        private const string ArgumentPrefix = "--wof-graveyard-controller-probe=";

        internal static bool IsRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index].StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        internal static IEnumerator Run(WofPlayerController player, Vector3[] route)
        {
            if (!TryResolveCaptureRoot(out var captureRoot) || player == null ||
                !player.IsSpawned || !player.IsOwner || route == null || route.Length < 3)
            {
                Fail("setup");
                yield break;
            }

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Graveyard Traversal QA Controller");
            try
            {
                gamepad.MakeCurrent();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return WaitUntilGrounded(player, 5f);
                if (!player.IsGrounded)
                {
                    Fail("start-grounded", $" position={player.transform.position}");
                    yield break;
                }

                yield return Capture(captureRoot, "graveyard-route-south-ramp.png");
                if (!IsCompleteCapture(captureRoot, "graveyard-route-south-ramp.png"))
                {
                    yield break;
                }

                var routeLength = 0f;
                for (var index = 1; index < route.Length; index++)
                {
                    routeLength += WofGraveyardTraversalRules.HorizontalDistance(
                        route[index - 1],
                        route[index]);
                }

                var startedAt = Time.realtimeSinceStartup;
                var previousPosition = player.transform.position;
                var actualDistance = 0f;
                var maximumCrossTrack = 0f;
                var movementFrames = 0;
                var groundedFrames = 0;
                var interiorCaptured = false;
                Debug.Log($"[WOF-AUTOMATION] GRAVEYARD_CONTROLLER_PROBE_START points={route.Length} routeLength={routeLength:F2} position={previousPosition}");

                for (var index = 1; index < route.Length; index++)
                {
                    var target = route[index];
                    var bestDistance = WofGraveyardTraversalRules.HorizontalDistance(
                        player.transform.position,
                        target);
                    var lastProgressAt = Time.realtimeSinceStartup;
                    var segmentDeadline = Time.realtimeSinceStartup +
                                          Mathf.Max(5f, bestDistance / WofGameConstants.WalkSpeed + 4f);
                    while (WofGraveyardTraversalRules.HorizontalDistance(
                               player.transform.position,
                               target) > WofGraveyardTraversalRules.ArrivalRadius)
                    {
                        var current = player.transform.position;
                        var direction = target - current;
                        direction.y = 0f;
                        if (direction.sqrMagnitude <= 0.0001f ||
                            !player.ApplyAutomationMovementHeading(
                                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg))
                        {
                            Fail("heading", $" point={index} position={current}");
                            yield break;
                        }

                        InputSystem.QueueStateEvent(
                            gamepad,
                            new GamepadState { leftStick = Vector2.up });
                        yield return null;

                        current = player.transform.position;
                        actualDistance += WofGraveyardTraversalRules.HorizontalDistance(
                            previousPosition,
                            current);
                        previousPosition = current;
                        maximumCrossTrack = Mathf.Max(
                            maximumCrossTrack,
                            WofGraveyardTraversalRules.HorizontalDistanceToSegment(
                                current,
                                route[index - 1],
                                target));
                        movementFrames++;
                        if (player.IsGrounded) groundedFrames++;

                        var distance = WofGraveyardTraversalRules.HorizontalDistance(current, target);
                        if (distance < bestDistance - 0.15f)
                        {
                            bestDistance = distance;
                            lastProgressAt = Time.realtimeSinceStartup;
                        }
                        if (current.y < WofGraveyardVillageLayout.ReactBaseHeight - 6f)
                        {
                            Fail("fall", $" point={index} position={current}");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup - lastProgressAt > 2.75f)
                        {
                            Fail("stalled", $" point={index} distance={distance:F2} position={current}");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup > segmentDeadline)
                        {
                            Fail("segment-timeout", $" point={index} distance={distance:F2} position={current}");
                            yield break;
                        }
                    }

                    if (!interiorCaptured && index >= 4)
                    {
                        interiorCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return new WaitForFixedUpdate();
                        yield return Capture(captureRoot, "graveyard-route-center-aisle.png");
                        if (!IsCompleteCapture(captureRoot, "graveyard-route-center-aisle.png"))
                        {
                            yield break;
                        }
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForFixedUpdate();
                var endError = WofGraveyardTraversalRules.HorizontalDistance(
                    player.transform.position,
                    route[route.Length - 1]);
                var groundedRatio = movementFrames == 0
                    ? 0f
                    : groundedFrames / (float)movementFrames;
                var duration = Time.realtimeSinceStartup - startedAt;
                if (endError > 1.5f || actualDistance < routeLength * 0.85f ||
                    maximumCrossTrack > WofGraveyardTraversalRules.MaximumCrossTrackError ||
                    groundedRatio < WofGraveyardTraversalRules.MinimumGroundedRatio)
                {
                    Fail(
                        "final",
                        $" endError={endError:F2} actualDistance={actualDistance:F2} routeLength={routeLength:F2} maxCrossTrack={maximumCrossTrack:F2} groundedRatio={groundedRatio:F3}");
                    yield break;
                }

                yield return Capture(captureRoot, "graveyard-route-northwest-exit.png");
                if (!IsCompleteCapture(captureRoot, "graveyard-route-northwest-exit.png"))
                {
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] GRAVEYARD_CONTROLLER_PROBE_COMPLETE nativeGamepad=true points={route.Length} duration={duration:F2} routeLength={routeLength:F2} actualDistance={actualDistance:F2} maxCrossTrack={maximumCrossTrack:F2} groundedRatio={groundedRatio:F3} endError={endError:F2} position={player.transform.position}");
            }
            finally
            {
                if (gamepad.added)
                {
                    InputSystem.RemoveDevice(gamepad);
                }
            }
        }

        private static bool TryResolveCaptureRoot(out string captureRoot)
        {
            captureRoot = null;
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                var argument = arguments[index];
                if (!argument.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var raw = argument.Substring(ArgumentPrefix.Length).Trim('"');
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }
                var full = Path.GetFullPath(raw);
                if (!full.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                Directory.CreateDirectory(full);
                captureRoot = full;
                return true;
            }
            return false;
        }

        private static IEnumerator WaitUntilGrounded(WofPlayerController player, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!player.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator Capture(string root, string fileName)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(root, fileName);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 4f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                Fail("screenshot", $" file={fileName}");
            }
        }

        private static bool IsCompleteCapture(string root, string fileName)
        {
            var path = Path.Combine(root, fileName);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static void Fail(string stage, string detail = "")
        {
            Debug.LogError($"[WOF-AUTOMATION] GRAVEYARD_CONTROLLER_PROBE_FAILED stage={stage}{detail}");
        }
    }
}
