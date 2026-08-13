using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WOF
{
    internal static class WofChicagoTraversalProbe
    {
        private const string ArgumentPrefix = "--wof-chicago-controller-probe=";

        internal static bool IsRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase)) return true;
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

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Chicago Traversal QA Controller");
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

                yield return Capture(captureRoot, "chicago-route-south-entry.png");
                if (!IsCompleteCapture(captureRoot, "chicago-route-south-entry.png")) yield break;

                var routeLength = 0f;
                for (var index = 1; index < route.Length; index++)
                    routeLength += WofChicagoTraversalRules.HorizontalDistance(route[index - 1], route[index]);

                var startedAt = Time.realtimeSinceStartup;
                var previous = player.transform.position;
                var actualDistance = 0f;
                var maximumCrossTrack = 0f;
                var movementFrames = 0;
                var groundedFrames = 0;
                var intersectionCaptured = false;
                var parkCaptured = false;
                Debug.Log($"[WOF-AUTOMATION] CHICAGO_CONTROLLER_PROBE_START points={route.Length} routeLength={routeLength:F2} position={previous}");

                for (var index = 1; index < route.Length; index++)
                {
                    var target = route[index];
                    var bestDistance = WofChicagoTraversalRules.HorizontalDistance(player.transform.position, target);
                    var lastProgressAt = Time.realtimeSinceStartup;
                    var deadline = Time.realtimeSinceStartup +
                                   Mathf.Max(6f, bestDistance / WofGameConstants.WalkSpeed + 5f);
                    while (WofChicagoTraversalRules.HorizontalDistance(player.transform.position, target) >
                           WofChicagoTraversalRules.ArrivalRadius)
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

                        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                        yield return null;
                        current = player.transform.position;
                        actualDistance += WofChicagoTraversalRules.HorizontalDistance(previous, current);
                        previous = current;
                        maximumCrossTrack = Mathf.Max(
                            maximumCrossTrack,
                            WofChicagoTraversalRules.HorizontalDistanceToSegment(
                                current,
                                route[index - 1],
                                target));
                        movementFrames++;
                        if (player.IsGrounded) groundedFrames++;

                        var distance = WofChicagoTraversalRules.HorizontalDistance(current, target);
                        if (distance < bestDistance - 0.15f)
                        {
                            bestDistance = distance;
                            lastProgressAt = Time.realtimeSinceStartup;
                        }
                        if (current.y < WofChicagoCityLayout.ReactBaseHeight - 6f)
                        {
                            Fail("fall", $" point={index} position={current}");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup - lastProgressAt > 3f)
                        {
                            Fail("stalled", $" point={index} distance={distance:F2} position={current}");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup > deadline)
                        {
                            Fail("segment-timeout", $" point={index} distance={distance:F2} position={current}");
                            yield break;
                        }
                    }

                    if (!intersectionCaptured && index >= 4)
                    {
                        intersectionCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return Capture(captureRoot, "chicago-route-intersection.png");
                        if (!IsCompleteCapture(captureRoot, "chicago-route-intersection.png")) yield break;
                    }
                    if (!parkCaptured && index >= 7)
                    {
                        parkCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        player.ApplyAutomationMovementHeading(180f);
                        yield return null;
                        yield return Capture(captureRoot, "chicago-route-bean-park.png");
                        if (!IsCompleteCapture(captureRoot, "chicago-route-bean-park.png")) yield break;
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForFixedUpdate();
                var endError = WofChicagoTraversalRules.HorizontalDistance(
                    player.transform.position,
                    route[route.Length - 1]);
                var groundedRatio = movementFrames == 0 ? 0f : groundedFrames / (float)movementFrames;
                var duration = Time.realtimeSinceStartup - startedAt;
                var reachedPark = WofChicagoTraversalRules.IsBeanParkApproach(route[7]);
                var reachedNorth = WofChicagoTraversalRules.IsNorthBoundary(player.transform.position);
                if (endError > 1.5f || actualDistance < routeLength * 0.85f ||
                    maximumCrossTrack > WofChicagoTraversalRules.MaximumCrossTrackError ||
                    groundedRatio < WofChicagoTraversalRules.MinimumGroundedRatio ||
                    !reachedPark || !reachedNorth)
                {
                    Fail("final", $" endError={endError:F2} actualDistance={actualDistance:F2} routeLength={routeLength:F2} maxCrossTrack={maximumCrossTrack:F2} groundedRatio={groundedRatio:F3} park={reachedPark} north={reachedNorth}");
                    yield break;
                }

                yield return Capture(captureRoot, "chicago-route-north-boundary.png");
                if (!IsCompleteCapture(captureRoot, "chicago-route-north-boundary.png")) yield break;
                Debug.Log($"[WOF-AUTOMATION] CHICAGO_CONTROLLER_PROBE_COMPLETE nativeGamepad=true points={route.Length} duration={duration:F2} routeLength={routeLength:F2} actualDistance={actualDistance:F2} maxCrossTrack={maximumCrossTrack:F2} groundedRatio={groundedRatio:F3} endError={endError:F2} beanPark={reachedPark} northBoundary={reachedNorth} position={player.transform.position}");
            }
            finally
            {
                if (gamepad.added) InputSystem.RemoveDevice(gamepad);
            }
        }

        private static bool TryResolveCaptureRoot(out string captureRoot)
        {
            captureRoot = null;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                var raw = argument.Substring(ArgumentPrefix.Length).Trim('"');
                if (string.IsNullOrWhiteSpace(raw)) return false;
                var full = Path.GetFullPath(raw);
                if (!full.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase)) return false;
                Directory.CreateDirectory(full);
                captureRoot = full;
                return true;
            }
            return false;
        }

        private static IEnumerator WaitUntilGrounded(WofPlayerController player, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!player.IsGrounded && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
        }

        private static IEnumerator Capture(string root, string fileName)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(root, fileName);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 4f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                Fail("screenshot", $" file={fileName}");
        }

        private static bool IsCompleteCapture(string root, string fileName)
        {
            var path = Path.Combine(root, fileName);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static void Fail(string stage, string detail = "")
        {
            Debug.LogError($"[WOF-AUTOMATION] CHICAGO_CONTROLLER_PROBE_FAILED stage={stage}{detail}");
        }
    }
}
