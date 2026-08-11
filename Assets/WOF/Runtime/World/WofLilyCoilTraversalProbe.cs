using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WOF
{
    internal static class WofLilyCoilTraversalProbe
    {
        private const string ArgumentPrefix = "--wof-lily-coil-controller-probe=";
        private const float CompletionT = 0.995f;

        internal static bool IsRequested
        {
            get
            {
                foreach (var argument in Environment.GetCommandLineArgs())
                {
                    if (argument.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
        }

        internal static IEnumerator Run()
        {
            var captureRoot = ResolveCaptureRoot();
            if (captureRoot == null)
            {
                Fail("capture-root");
                yield break;
            }

            WofPlayerController player = null;
            var playerDeadline = Time.realtimeSinceStartup + 20f;
            while (player == null && Time.realtimeSinceStartup < playerDeadline)
            {
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<WofPlayerController>(
                             FindObjectsInactive.Exclude,
                             FindObjectsSortMode.None))
                {
                    if (!candidate.IsSpawned || !candidate.IsOwner) continue;
                    player = candidate;
                    break;
                }
                if (player == null) yield return null;
            }
            if (player == null)
            {
                Fail("player");
                yield break;
            }

            var authoredSpawnState = WofLilyCoilLayout.GetNearestState(WofLilyCoilLayout.PlayableSpawnPosition);
            var lowerCapFrame = WofLilyCoilLayout.GetFrame(0f);
            var lowerCapPosition = lowerCapFrame.Center +
                                   WofLilyCoilLayout.GetRadial(lowerCapFrame, authoredSpawnState.SurfaceAngle) *
                                   WofLilyCoilLayout.TubePlayerRadius;
            if (!player.PrepareForAutomationVillagerInteractionProbe(
                    lowerCapPosition,
                    WofLilyCoilLayout.GetTunnelViewProbeYaw(0f),
                    0f))
            {
                Fail("position");
                yield break;
            }

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 1600f;

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Lily Coil Traversal QA Controller");
            try
            {
                gamepad.MakeCurrent();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                yield return new WaitForFixedUpdate();

                var activeDeadline = Time.realtimeSinceStartup + 5f;
                while (!player.IsLilyCoilTubeActive && Time.realtimeSinceStartup < activeDeadline)
                    yield return new WaitForFixedUpdate();
                if (!player.IsLilyCoilTubeActive)
                {
                    Fail("tube-entry", $" position={player.transform.position}");
                    yield break;
                }

                var startT = player.LilyCoilTubeProgress;
                if (startT > 0.012f)
                {
                    Fail("start-progress", $" t={startT:F4} position={player.transform.position}");
                    yield break;
                }

                yield return Capture(captureRoot, "lily-coil-traversal-lower-cap.png");
                if (!HasCapture(captureRoot, "lily-coil-traversal-lower-cap.png")) yield break;

                var latchSprint = new GamepadState { leftStick = Vector2.up }
                    .WithButton(GamepadButton.LeftStick);
                var moveForward = new GamepadState { leftStick = Vector2.up };
                InputSystem.QueueStateEvent(gamepad, latchSprint);
                yield return null;
                InputSystem.QueueStateEvent(gamepad, moveForward);

                var startedAt = Time.realtimeSinceStartup;
                var expectedDuration = WofLilyCoilLayout.TubePathLength /
                    (WofGameConstants.WalkSpeed * WofMovementMath.SprintMultiplier *
                     WofLilyCoilLayout.TubeMovementMultiplier);
                var traversalDeadline = startedAt + expectedDuration + 30f;
                var lastProgressAt = startedAt;
                var bestT = startT;
                var previousT = startT;
                var previousPosition = player.transform.position;
                var actualDistance = 0f;
                var maximumBacktrack = 0f;
                var maximumSurfaceError = 0f;
                var movementFrames = 0;
                var groundedFrames = 0;
                var sprintFrames = 0;
                var midpointCaptured = false;
                Debug.Log($"[WOF-AUTOMATION] LILY_COIL_CONTROLLER_PROBE_START nativeGamepad=true startT={startT:F4} pathLength={WofLilyCoilLayout.TubePathLength:F2} expectedDuration={expectedDuration:F2} position={previousPosition}");

                while (player.LilyCoilTubeProgress < CompletionT)
                {
                    InputSystem.QueueStateEvent(gamepad, moveForward);
                    yield return null;

                    var current = player.transform.position;
                    var currentT = player.LilyCoilTubeProgress;
                    var frame = WofLilyCoilLayout.GetFrame(currentT);
                    var surfaceRadius = Vector3.Distance(current, frame.Center);
                    actualDistance += Vector3.Distance(previousPosition, current);
                    maximumSurfaceError = Mathf.Max(
                        maximumSurfaceError,
                        Mathf.Abs(surfaceRadius - WofLilyCoilLayout.TubePlayerRadius));
                    maximumBacktrack = Mathf.Max(maximumBacktrack, previousT - currentT);
                    previousPosition = current;
                    previousT = currentT;
                    movementFrames++;
                    if (player.IsGrounded) groundedFrames++;
                    if (player.IsSprinting) sprintFrames++;

                    if (!WofLilyCoilLayout.IsInsideTubeRealm(current))
                    {
                        Fail("left-realm", $" t={currentT:F4} position={current}");
                        yield break;
                    }
                    if (currentT > bestT + 0.0001f)
                    {
                        bestT = currentT;
                        lastProgressAt = Time.realtimeSinceStartup;
                    }
                    if (Time.realtimeSinceStartup - lastProgressAt > 4f)
                    {
                        Fail("stalled", $" t={currentT:F4} position={current}");
                        yield break;
                    }
                    if (Time.realtimeSinceStartup > traversalDeadline)
                    {
                        Fail("timeout", $" t={currentT:F4} actualDistance={actualDistance:F2}");
                        yield break;
                    }

                    if (!midpointCaptured && currentT >= 0.5f)
                    {
                        midpointCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return null;
                        yield return new WaitForFixedUpdate();
                        yield return Capture(captureRoot, "lily-coil-traversal-midpoint.png");
                        if (!HasCapture(captureRoot, "lily-coil-traversal-midpoint.png")) yield break;
                        InputSystem.QueueStateEvent(gamepad, latchSprint);
                        yield return null;
                        InputSystem.QueueStateEvent(gamepad, moveForward);
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                yield return new WaitForFixedUpdate();

                var endT = player.LilyCoilTubeProgress;
                var endPosition = player.transform.position;
                var expectedEndFrame = WofLilyCoilLayout.GetFrame(endT);
                var endSurfaceError = Mathf.Abs(
                    Vector3.Distance(endPosition, expectedEndFrame.Center) -
                    WofLilyCoilLayout.TubePlayerRadius);
                var groundedRatio = movementFrames == 0 ? 0f : groundedFrames / (float)movementFrames;
                var sprintRatio = movementFrames == 0 ? 0f : sprintFrames / (float)movementFrames;
                var duration = Time.realtimeSinceStartup - startedAt;
                var minimumDistance = WofLilyCoilLayout.TubePathLength * 0.92f;
                if (endT < CompletionT || actualDistance < minimumDistance || maximumBacktrack > 0.002f ||
                    maximumSurfaceError > 0.15f || endSurfaceError > 0.15f || groundedRatio < 0.9f ||
                    sprintRatio < 0.8f || !midpointCaptured)
                {
                    Fail("final", $" startT={startT:F4} endT={endT:F4} duration={duration:F2} actualDistance={actualDistance:F2} minimumDistance={minimumDistance:F2} maxBacktrack={maximumBacktrack:F4} maxSurfaceError={maximumSurfaceError:F3} endSurfaceError={endSurfaceError:F3} groundedRatio={groundedRatio:F3} sprintRatio={sprintRatio:F3} midpoint={midpointCaptured.ToString().ToLowerInvariant()} position={endPosition}");
                    yield break;
                }

                yield return Capture(captureRoot, "lily-coil-traversal-upper-cap.png");
                if (!HasCapture(captureRoot, "lily-coil-traversal-upper-cap.png")) yield break;
                Debug.Log($"[WOF-AUTOMATION] LILY_COIL_CONTROLLER_PROBE_COMPLETE nativeGamepad=true startT={startT:F4} endT={endT:F4} duration={duration:F2} pathLength={WofLilyCoilLayout.TubePathLength:F2} actualDistance={actualDistance:F2} maxBacktrack={maximumBacktrack:F4} maxSurfaceError={maximumSurfaceError:F3} endSurfaceError={endSurfaceError:F3} groundedRatio={groundedRatio:F3} sprintRatio={sprintRatio:F3} midpoint=true position={endPosition}");
            }
            finally
            {
                if (gamepad.added) InputSystem.RemoveDevice(gamepad);
            }
        }

        private static string ResolveCaptureRoot()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                var raw = argument.Substring(ArgumentPrefix.Length).Trim('"');
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var full = Path.GetFullPath(raw);
                if (!full.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase)) return null;
                Directory.CreateDirectory(full);
                return full;
            }
            return null;
        }

        private static IEnumerator Capture(string root, string fileName)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(root, fileName);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 4f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                Fail("screenshot", $" file={fileName}");
        }

        private static bool HasCapture(string root, string fileName)
        {
            var path = Path.Combine(root, fileName);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static void Fail(string stage, string detail = "")
        {
            Debug.LogError($"[WOF-AUTOMATION] LILY_COIL_CONTROLLER_PROBE_FAILED stage={stage}{detail}");
        }
    }
}
