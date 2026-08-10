using System;
using System.Collections;
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
                var operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_FAILED stage=load-operation");
                    yield break;
                }
                while (!operation.isDone) yield return null;
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_FAILED stage=scene-state");
                yield break;
            }

            WofPlayerController player = null;
            if (IsViewProbeRequested())
            {
                var controllerProbe = IsControllerProbeRequested();
                var ladderProbe = IsLadderControllerProbeRequested();
                var viewVariant = ResolveViewProbeVariant();
                var deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && player == null)
                {
                    foreach (var candidate in FindObjectsByType<WofPlayerController>(
                                 FindObjectsInactive.Exclude,
                                 FindObjectsSortMode.None))
                    {
                        if (!candidate.IsSpawned || !candidate.IsOwner) continue;
                        player = candidate;
                        break;
                    }
                    if (player == null) yield return null;
                }

                var probePosition = controllerProbe
                    ? WofMountainVillageLayout.FirstVillagerControllerProbeSpawn
                    : ladderProbe
                        ? WofMountainVillageLayout.FirstLadderControllerProbeSpawn
                        : ResolveStaticViewPosition(viewVariant);
                var probeYaw = controllerProbe ? 0f : ladderProbe ? 192.561f : ResolveStaticViewYaw(viewVariant);
                var probePitch = controllerProbe ? 26f : ladderProbe ? 0f : ResolveStaticViewPitch(viewVariant);
                var positioned = player != null && (controllerProbe || ladderProbe
                    ? player.PrepareForAutomationVillagerInteractionProbe(probePosition, probeYaw, probePitch)
                    : player.PrepareForAutomationStaticViewProbe(probePosition, probeYaw, probePitch));
                if (!positioned)
                {
                    Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_VILLAGE_SCENE_FAILED stage=probe-position");
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_VILLAGE_PROBE_POSITIONED variant={viewVariant} position={probePosition} yaw={probeYaw:F3} pitch={probePitch:F3} controller={controllerProbe} ladder={ladderProbe}");
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
        }

        private static bool IsViewProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-mountain-village-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-mountain-village-view-probe=", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--wof-mountain-villager-controller-probe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--wof-mountain-ladder-controller-probe", StringComparison.OrdinalIgnoreCase))
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
                if (value == "summit" || value == "aerial" || value == "banquet" || value == "catwalk") return value;
            }
            return "exterior";
        }

        private static Vector3 ResolveStaticViewPosition(string variant)
        {
            return variant switch
            {
                "summit" => WofMountainVillageLayout.SummitViewProbeSpawn,
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
