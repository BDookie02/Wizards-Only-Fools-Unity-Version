using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofMountainVillageSceneLoader : MonoBehaviour
    {
        public const string SceneName = "WofMountainVillage";

        private IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(SceneName).isLoaded)
            {
                yield return WofAdditiveSceneLoadScheduler.LoadSceneAdditively(
                    SceneName,
                    "MOUNTAIN_VILLAGE_SCENE_FAILED",
                    IsViewProbeRequested()
                        ? WofAdditiveSceneLoadScheduler.ProbePriority
                        : WofAdditiveSceneLoadScheduler.MountainPriority);
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_FAILED stage=scene-state");
                yield break;
            }

            WofPlayerController player = null;
            WofMountainAccessPathRuntime accessPath = null;
            if (IsViewProbeRequested())
            {
                var controllerProbe = IsControllerProbeRequested();
                var ladderProbe = IsLadderControllerProbeRequested();
                var accessControllerProbe = IsAccessControllerProbeRequested();
                var accessWorldPoints = Array.Empty<Vector3>();
                var viewVariant = ResolveViewProbeVariant();
                var deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline &&
                       (player == null || accessControllerProbe && accessPath == null))
                {
                    foreach (var candidate in FindObjectsByType<WofPlayerController>(
                                 FindObjectsInactive.Exclude,
                                 FindObjectsSortMode.None))
                    {
                        if (!candidate.IsSpawned || !candidate.IsOwner) continue;
                        player = candidate;
                        break;
                    }
                    if (accessControllerProbe)
                    {
                        foreach (var candidate in FindObjectsByType<WofMountainAccessPathRuntime>(
                                     FindObjectsInactive.Exclude,
                                     FindObjectsSortMode.None))
                        {
                            if (candidate.gameObject.scene != scene || candidate.PointCount < 2) continue;
                            accessPath = candidate;
                            break;
                        }
                    }
                    if (player == null || accessControllerProbe && accessPath == null) yield return null;
                }

                if (accessControllerProbe &&
                    (accessPath == null || !accessPath.TryCopyWorldPoints(out accessWorldPoints)))
                {
                    Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_FAILED stage=access-path");
                    yield break;
                }

                var probePosition = controllerProbe
                    ? WofMountainVillageLayout.FirstVillagerControllerProbeSpawn
                    : ladderProbe
                        ? WofMountainVillageLayout.FirstLadderControllerProbeSpawn
                        : accessControllerProbe
                            ? accessWorldPoints[0] + Vector3.up * 2f
                        : ResolveStaticViewPosition(viewVariant);
                var probeYaw = controllerProbe ? 0f
                    : ladderProbe ? 192.561f
                    : accessControllerProbe ? ResolveHeading(accessWorldPoints[0], accessWorldPoints[1])
                    : ResolveStaticViewYaw(viewVariant);
                var probePitch = controllerProbe ? 26f
                    : ladderProbe || accessControllerProbe ? 0f
                    : ResolveStaticViewPitch(viewVariant);
                var positioned = player != null && (controllerProbe || ladderProbe || accessControllerProbe
                    ? player.PrepareForAutomationVillagerInteractionProbe(probePosition, probeYaw, probePitch)
                    : player.PrepareForAutomationStaticViewProbe(probePosition, probeYaw, probePitch));
                if (!positioned)
                {
                    Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_FAILED stage=probe-position");
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_VILLAGE_PROBE_POSITIONED variant={viewVariant} position={probePosition} yaw={probeYaw:F3} pitch={probePitch:F3} controller={controllerProbe} ladder={ladderProbe} access={accessControllerProbe}");
            }

            Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_READY scene={scene.name} roots={scene.rootCount} origin={WofMountainVillageLayout.WorldOrigin}");
            if (IsLadderControllerProbeRequested())
            {
                if (player == null)
                {
                    Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_LADDER_CONTROLLER_PROBE_FAILED stage=player");
                    yield break;
                }
                yield return RunLadderControllerProbe(player);
            }
            if (IsAccessControllerProbeRequested())
            {
                if (player == null || accessPath == null)
                {
                    Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=setup");
                    yield break;
                }
                yield return RunAccessControllerProbe(player, accessPath);
            }
        }

        private static bool IsViewProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-mountain-village-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-mountain-village-view-probe=", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--wof-mountain-villager-controller-probe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--wof-mountain-ladder-controller-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-mountain-access-controller-probe=", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ResolveViewProbeVariant()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                const string prefix = "--wof-mountain-village-view-probe=";
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var value = argument.Substring(prefix.Length).Trim().ToLowerInvariant();
                if (value == "profile" || value == "summit" || value == "aerial" || value == "banquet" || value == "catwalk") return value;
            }
            return "exterior";
        }

        private static Vector3 ResolveStaticViewPosition(string variant)
        {
            return variant switch
            {
                "summit" => WofMountainVillageLayout.SummitViewProbeSpawn,
                "profile" => WofMountainVillageLayout.ProfileViewProbeSpawn,
                "aerial" => WofMountainVillageLayout.AerialViewProbeSpawn,
                "banquet" => WofMountainVillageLayout.BanquetViewProbeSpawn,
                "catwalk" => WofMountainVillageLayout.CatwalkViewProbeSpawn,
                _ => WofMountainVillageLayout.ViewProbeSpawn
            };
        }

        private static float ResolveStaticViewYaw(string variant)
        {
            return variant switch
            {
                "summit" => 180f,
                "profile" => 180f,
                "aerial" => 0f,
                "banquet" => 153.4f,
                "catwalk" => 121f,
                _ => 180f
            };
        }

        private static float ResolveStaticViewPitch(string variant)
        {
            return variant switch
            {
                "summit" => 6f,
                "profile" => 18f,
                "aerial" => 82f,
                "banquet" => 5f,
                "catwalk" => 1f,
                _ => -11f
            };
        }

        private static bool IsControllerProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-mountain-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsLadderControllerProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-mountain-ladder-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAccessControllerProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith("--wof-mountain-access-controller-probe=", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string ResolveAccessControllerProbeRoot()
        {
            const string prefix = "--wof-mountain-access-controller-probe=";
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var raw = argument.Substring(prefix.Length).Trim('"');
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var full = Path.GetFullPath(raw);
                if (!full.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase)) return null;
                Directory.CreateDirectory(full);
                return full;
            }
            return null;
        }

        private static IEnumerator RunAccessControllerProbe(
            WofPlayerController player,
            WofMountainAccessPathRuntime accessPath)
        {
            var screenshotRoot = ResolveAccessControllerProbeRoot();
            if (screenshotRoot == null || !accessPath.TryCopyWorldPoints(out var points) || points.Length < 8)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=path-data");
                yield break;
            }

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Mountain Access QA Controller");
            try
            {
                gamepad.MakeCurrent();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                yield return null;

                var groundedDeadline = Time.realtimeSinceStartup + 5f;
                while (!player.IsGrounded && Time.realtimeSinceStartup < groundedDeadline)
                    yield return new WaitForFixedUpdate();
                if (!player.IsGrounded)
                {
                    Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=start-grounded position={player.transform.position}");
                    yield break;
                }

                yield return CaptureAccessProbeScreenshot(screenshotRoot, "mountain-trail-start.png");
                if (!IsCompleteAccessProbeScreenshot(screenshotRoot, "mountain-trail-start.png"))
                    yield break;
                var routeLength = 0f;
                for (var index = 1; index < points.Length; index++)
                    routeLength += HorizontalDistance(points[index - 1], points[index]);

                var movementState = new GamepadState { leftStick = Vector2.up }
                    .WithButton(GamepadButton.LeftStick);
                InputSystem.QueueStateEvent(gamepad, movementState);
                var startedAt = Time.realtimeSinceStartup;
                var previousPosition = player.transform.position;
                var actualDistance = 0f;
                var maximumCrossTrackError = 0f;
                var groundedFrames = 0;
                var movementFrames = 0;
                var midpointCaptured = false;
                Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_START points={points.Length} routeLength={routeLength:F2} position={previousPosition}");

                for (var index = 1; index < points.Length; index++)
                {
                    var target = points[index];
                    var bestDistance = HorizontalDistance(player.transform.position, target);
                    var lastProgressAt = Time.realtimeSinceStartup;
                    var segmentDeadline = Time.realtimeSinceStartup +
                                          Mathf.Max(4f, bestDistance /
                                              (WofGameConstants.WalkSpeed * WofMovementMath.SprintMultiplier) + 3f);
                    while (HorizontalDistance(player.transform.position, target) > 0.9f)
                    {
                        var current = player.transform.position;
                        var distance = HorizontalDistance(current, target);
                        var direction = target - current;
                        direction.y = 0f;
                        if (direction.sqrMagnitude <= 0.0001f ||
                            !player.ApplyAutomationMovementHeading(
                                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg))
                        {
                            Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=heading point={index} position={current}");
                            yield break;
                        }

                        InputSystem.QueueStateEvent(gamepad, movementState);
                        actualDistance += HorizontalDistance(previousPosition, current);
                        previousPosition = current;
                        maximumCrossTrackError = Mathf.Max(
                            maximumCrossTrackError,
                            ResolveHorizontalSegmentDistance(current, points[index - 1], target));
                        movementFrames++;
                        if (player.IsGrounded) groundedFrames++;

                        var minimumExpectedY = Mathf.Min(points[index - 1].y, target.y) - 6f;
                        if (current.y < minimumExpectedY)
                        {
                            Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=fall point={index} position={current} minimumY={minimumExpectedY:F2}");
                            yield break;
                        }
                        if (distance < bestDistance - 0.2f)
                        {
                            bestDistance = distance;
                            lastProgressAt = Time.realtimeSinceStartup;
                        }
                        if (Time.realtimeSinceStartup - lastProgressAt > 3f)
                        {
                            Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=stalled point={index} distance={distance:F2} position={current}");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup > segmentDeadline)
                        {
                            Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=segment-timeout point={index} distance={distance:F2} position={current}");
                            yield break;
                        }
                        yield return null;
                    }

                    if (!midpointCaptured && index >= points.Length / 2)
                    {
                        midpointCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return new WaitForFixedUpdate();
                        yield return CaptureAccessProbeScreenshot(screenshotRoot, "mountain-trail-midpoint.png");
                        if (!IsCompleteAccessProbeScreenshot(screenshotRoot, "mountain-trail-midpoint.png"))
                            yield break;
                        InputSystem.QueueStateEvent(gamepad, movementState);
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForFixedUpdate();
                var endError = HorizontalDistance(player.transform.position, points[points.Length - 1]);
                var verticalError = Mathf.Abs(player.transform.position.y - points[points.Length - 1].y);
                var groundedRatio = movementFrames == 0 ? 0f : groundedFrames / (float)movementFrames;
                var duration = Time.realtimeSinceStartup - startedAt;
                if (endError > 1.5f || verticalError > 6f || actualDistance < routeLength * 0.85f ||
                    maximumCrossTrackError > WofMountainAccessPathLayout.Width * 0.5f || groundedRatio < 0.45f)
                {
                    Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=final endError={endError:F2} verticalError={verticalError:F2} actualDistance={actualDistance:F2} routeLength={routeLength:F2} maxCrossTrack={maximumCrossTrackError:F2} groundedRatio={groundedRatio:F3}");
                    yield break;
                }

                yield return CaptureAccessProbeScreenshot(screenshotRoot, "mountain-trail-summit.png");
                if (!IsCompleteAccessProbeScreenshot(screenshotRoot, "mountain-trail-summit.png"))
                    yield break;
                Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_COMPLETE nativeGamepad=true points={points.Length} duration={duration:F2} routeLength={routeLength:F2} actualDistance={actualDistance:F2} maxCrossTrack={maximumCrossTrackError:F2} groundedRatio={groundedRatio:F3} endError={endError:F2} verticalError={verticalError:F2} position={player.transform.position}");
            }
            finally
            {
                if (gamepad.added) InputSystem.RemoveDevice(gamepad);
            }
        }

        private static IEnumerator CaptureAccessProbeScreenshot(string root, string fileName)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(root, fileName);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 4f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_CONTROLLER_PROBE_FAILED stage=screenshot file={fileName}");
        }

        private static bool IsCompleteAccessProbeScreenshot(string root, string fileName)
        {
            var path = Path.Combine(root, fileName);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static float ResolveHeading(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private static float ResolveHorizontalSegmentDistance(Vector3 point, Vector3 from, Vector3 to)
        {
            var start = new Vector2(from.x, from.z);
            var end = new Vector2(to.x, to.z);
            var candidate = new Vector2(point.x, point.z);
            var segment = end - start;
            var denominator = segment.sqrMagnitude;
            if (denominator <= 0.0001f) return Vector2.Distance(candidate, start);
            var t = Mathf.Clamp01(Vector2.Dot(candidate - start, segment) / denominator);
            return Vector2.Distance(candidate, start + segment * t);
        }

        private static IEnumerator RunLadderControllerProbe(WofPlayerController player)
        {
            var zoneDeadline = Time.realtimeSinceStartup + 8f;
            while (player.ActiveMountainLadderZoneCount == 0 && Time.realtimeSinceStartup < zoneDeadline)
            {
                yield return new WaitForFixedUpdate();
            }
            if (player.ActiveMountainLadderZoneCount == 0)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_LADDER_CONTROLLER_PROBE_FAILED stage=trigger");
                yield break;
            }

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Mountain Ladder QA Controller");
            gamepad.MakeCurrent();
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            yield return null;

            var startY = player.transform.position.y;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            var climbUntil = Time.realtimeSinceStartup + 0.8f;
            while (Time.realtimeSinceStartup < climbUntil)
            {
                yield return null;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return new WaitForFixedUpdate();
            var deltaY = player.transform.position.y - startY;
            InputSystem.RemoveDevice(gamepad);

            if (deltaY <= 1f)
            {
                Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_LADDER_CONTROLLER_PROBE_FAILED stage=climb deltaY={deltaY:F3}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_LADDER_CONTROLLER_PROBE_COMPLETE leftStick=true ladder=3:0-mineshaft-ladder-0 deltaY={deltaY:F3} climbSpeed={WofMountainLadderZone.ClimbSpeed:F1} damping={WofMountainLadderZone.PlanarDamping:F2}");
        }
    }
}
