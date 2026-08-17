using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WOF
{
    [DisallowMultipleComponent]
    internal sealed class WofDesertTraversalRuntime : MonoBehaviour
    {
        private const string ArgumentPrefix = "--wof-desert-controller-probe=";
        private WofPlayerController _player;
        private bool _started;

        internal static bool IsProbeRequested()
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

        internal void Configure(WofPlayerController player)
        {
            _player = player;
            if (_started || player == null || !player.IsOwner || !IsProbeRequested()) return;
            _started = true;
            StartCoroutine(RunProbe());
        }

        private IEnumerator RunProbe()
        {
            if (!TryResolveCaptureRoot(out var captureRoot))
            {
                Fail("capture-root");
                yield break;
            }

            var readyDeadline = Time.realtimeSinceStartup + 20f;
            while ((_player == null || !_player.IsSpawned || !_player.IsOwner) &&
                   Time.realtimeSinceStartup < readyDeadline)
            {
                yield return null;
            }
            if (_player == null || !_player.IsSpawned || !_player.IsOwner)
            {
                Fail("player");
                yield break;
            }

            var route = WofDesertTraversalRules.BuildNorthGateRoute();
            var startYaw = Mathf.Atan2(
                route[1].x - route[0].x,
                route[1].z - route[0].z) * Mathf.Rad2Deg;
            if (!_player.PrepareForAutomationVillagerInteractionProbe(
                    route[0] + Vector3.up * 1.2f,
                    startYaw,
                    -4f))
            {
                Fail("position");
                yield break;
            }

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Desert Traversal QA Controller");
            try
            {
                gamepad.MakeCurrent();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return WaitUntilGrounded(5f);
                if (!_player.IsGrounded)
                {
                    Fail("start-grounded", $" position={_player.transform.position}");
                    yield break;
                }

                yield return Capture(captureRoot, "desert-route-south-road.png");
                if (!IsCompleteCapture(captureRoot, "desert-route-south-road.png")) yield break;

                var routeLength = 0f;
                for (var index = 1; index < route.Length; index++)
                {
                    routeLength += WofDesertTraversalRules.HorizontalDistance(route[index - 1], route[index]);
                }
                var startedAt = Time.realtimeSinceStartup;
                var previous = _player.transform.position;
                var actualDistance = 0f;
                var maximumCrossTrack = 0f;
                var movementFrames = 0;
                var groundedFrames = 0;
                var plazaCaptured = false;
                var gateCaptured = false;
                Debug.Log($"[WOF-AUTOMATION] DESERT_CONTROLLER_PROBE_START points={route.Length} routeLength={routeLength:F2} position={previous}");

                for (var index = 1; index < route.Length; index++)
                {
                    var target = route[index];
                    var bestDistance = WofDesertTraversalRules.HorizontalDistance(
                        _player.transform.position,
                        target);
                    var lastProgressAt = Time.realtimeSinceStartup;
                    var deadline = Time.realtimeSinceStartup +
                                   Mathf.Max(8f, bestDistance / WofGameConstants.WalkSpeed + 6f);
                    while (WofDesertTraversalRules.HorizontalDistance(
                               _player.transform.position,
                               target) > WofDesertTraversalRules.ArrivalRadius)
                    {
                        var current = _player.transform.position;
                        var direction = target - current;
                        direction.y = 0f;
                        if (direction.sqrMagnitude <= 0.0001f ||
                            !_player.ApplyAutomationMovementHeading(
                                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg))
                        {
                            Fail("heading", $" point={index} position={current}");
                            yield break;
                        }

                        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                        yield return null;
                        current = _player.transform.position;
                        actualDistance += WofDesertTraversalRules.HorizontalDistance(previous, current);
                        previous = current;
                        maximumCrossTrack = Mathf.Max(
                            maximumCrossTrack,
                            WofDesertTraversalRules.HorizontalDistanceToSegment(
                                current,
                                route[index - 1],
                                target));
                        movementFrames++;
                        if (_player.IsGrounded) groundedFrames++;

                        var distance = WofDesertTraversalRules.HorizontalDistance(current, target);
                        if (distance < bestDistance - 0.15f)
                        {
                            bestDistance = distance;
                            lastProgressAt = Time.realtimeSinceStartup;
                        }
                        if (current.y < WofDesertVillageLayout.ReactBaseHeight - 8f)
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

                    if (!plazaCaptured && index >= 3)
                    {
                        plazaCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return Capture(captureRoot, "desert-route-well-detour.png");
                        if (!IsCompleteCapture(captureRoot, "desert-route-well-detour.png")) yield break;
                    }
                    if (!gateCaptured && index >= 7)
                    {
                        gateCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return Capture(captureRoot, "desert-route-north-gate.png");
                        if (!IsCompleteCapture(captureRoot, "desert-route-north-gate.png")) yield break;
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForFixedUpdate();
                var endError = WofDesertTraversalRules.HorizontalDistance(
                    _player.transform.position,
                    route[route.Length - 1]);
                var groundedRatio = movementFrames == 0 ? 0f : groundedFrames / (float)movementFrames;
                var duration = Time.realtimeSinceStartup - startedAt;
                var endedInExpansion = WofDesertTraversalRules.IsNorthExpansionPoint(_player.transform.position);
                if (endError > 1.5f || actualDistance < routeLength * 0.85f ||
                    maximumCrossTrack > WofDesertTraversalRules.MaximumCrossTrackError ||
                    groundedRatio < WofDesertTraversalRules.MinimumGroundedRatio || !endedInExpansion)
                {
                    Fail("final", $" endError={endError:F2} actualDistance={actualDistance:F2} routeLength={routeLength:F2} maxCrossTrack={maximumCrossTrack:F2} groundedRatio={groundedRatio:F3} expansion={endedInExpansion}");
                    yield break;
                }

                yield return Capture(captureRoot, "desert-route-expansion-chunk.png");
                if (!IsCompleteCapture(captureRoot, "desert-route-expansion-chunk.png")) yield break;
                Debug.Log($"[WOF-AUTOMATION] DESERT_CONTROLLER_PROBE_COMPLETE nativeGamepad=true points={route.Length} duration={duration:F2} routeLength={routeLength:F2} actualDistance={actualDistance:F2} maxCrossTrack={maximumCrossTrack:F2} groundedRatio={groundedRatio:F3} endError={endError:F2} expansionChunks={WofDesertTraversalRules.CountExpansionChunks()} position={_player.transform.position}");
            }
            finally
            {
                if (gamepad.added) InputSystem.RemoveDevice(gamepad);
            }
        }

        private static bool TryResolveCaptureRoot(out string captureRoot)
        {
            captureRoot = null;
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                var argument = arguments[index];
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

        private IEnumerator WaitUntilGrounded(float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!_player.IsGrounded && Time.realtimeSinceStartup < deadline)
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
            Debug.LogError($"[WOF-AUTOMATION] DESERT_CONTROLLER_PROBE_FAILED stage={stage}{detail}");
        }
    }
}
