using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WOF
{
    [DisallowMultipleComponent]
    internal sealed class WofTreeHouseTraversalRuntime : MonoBehaviour
    {
        private const string ProbeArgumentPrefix = "--wof-treehouse-controller-probe=";
        private WofPlayerController _player;
        private CharacterController _controller;
        private float _normalStepOffset;
        private float _normalSlopeLimit;
        private bool _assistApplied;
        private bool _probeStarted;
        private readonly List<Collider> _ignoredTreeColliders = new List<Collider>();
        private int _ignoredStructuralTreeMask;
        private int _ignoredBridgeTreeMask;
        private bool _spiralSupportsReady;

        internal void Configure(WofPlayerController player)
        {
            _player = player;
            _controller = player == null ? null : player.GetComponent<CharacterController>();
            if (_controller != null)
            {
                _normalStepOffset = _controller.stepOffset;
                _normalSlopeLimit = _controller.slopeLimit;
            }
            EnsureContinuousSpiralSupports();

            if (!_probeStarted && player != null && player.IsOwner &&
                TryResolveProbeRoot(out var captureRoot))
            {
                _probeStarted = true;
                StartCoroutine(RunControllerProbe(captureRoot));
            }
        }

        private void LateUpdate()
        {
            EnsureContinuousSpiralSupports();
            if (_player == null || _controller == null || !_player.IsSpawned ||
                !WofTreeHouseTraversalRules.RunsControllerSimulation(_player.IsServer, _player.IsOwner))
            {
                RestoreController();
                return;
            }

            var mobilePerformanceMode = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            var shouldAssist = WofTreeHouseTraversalRules.RequiresTraversalAssist(
                transform.position,
                mobilePerformanceMode);
            var structuralTreeMask = WofTreeHouseTraversalRules.ResolveStructuralCollisionAssistTreeMask(
                transform.position,
                mobilePerformanceMode);
            var bridgeTreeMask = WofTreeHouseTraversalRules.ResolveBridgeEndpointTreeMask(transform.position);
            if (WofTreeHouseTraversalRules.TryResolveSpiralSurfaceTreeIndex(
                    transform.position,
                    mobilePerformanceMode,
                    out var spiralSurfaceTreeIndex))
            {
                bridgeTreeMask &= ~(1 << spiralSurfaceTreeIndex);
            }
            SetStructuralCollisionAssist(structuralTreeMask, bridgeTreeMask);
            if (shouldAssist == _assistApplied)
            {
                return;
            }

            _assistApplied = shouldAssist;
            var assistedStepOffset = WofTreeHouseTraversalRules.ResolveAssistedStepOffset(
                mobilePerformanceMode);
            _controller.stepOffset = shouldAssist
                ? Mathf.Max(_normalStepOffset, assistedStepOffset)
                : _normalStepOffset;
            _controller.slopeLimit = shouldAssist
                ? Mathf.Max(_normalSlopeLimit, WofTreeHouseTraversalRules.AssistedSlopeLimit)
                : _normalSlopeLimit;
        }

        private void OnDestroy()
        {
            RestoreController();
        }

        private void RestoreController()
        {
            RestoreStructuralCollisions();
            if (!_assistApplied || _controller == null)
            {
                return;
            }

            _assistApplied = false;
            _controller.stepOffset = _normalStepOffset;
            _controller.slopeLimit = _normalSlopeLimit;
        }

        private void SetStructuralCollisionAssist(int structuralTreeMask, int bridgeTreeMask)
        {
            if (_controller == null ||
                (structuralTreeMask == _ignoredStructuralTreeMask &&
                 bridgeTreeMask == _ignoredBridgeTreeMask))
            {
                return;
            }

            RestoreStructuralCollisions();
            var combinedTreeMask = structuralTreeMask | bridgeTreeMask;
            if (combinedTreeMask == 0)
            {
                return;
            }

            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var treeBit = 1 << treeIndex;
                if ((combinedTreeMask & treeBit) == 0)
                {
                    continue;
                }

                var treeObject = GameObject.Find($"GiantTree_{treeIndex}");
                if (treeObject == null)
                {
                    continue;
                }

                foreach (var collider in treeObject.GetComponentsInChildren<Collider>(includeInactive: false))
                {
                    if (collider == null ||
                        (!IsTrunkOrRootCollider(collider) &&
                         ((bridgeTreeMask & treeBit) == 0 || !IsSpiralCollider(collider))))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(_controller, collider, ignore: true);
                    _ignoredTreeColliders.Add(collider);
                }
            }

            if (_ignoredTreeColliders.Count > 0)
            {
                _ignoredStructuralTreeMask = structuralTreeMask;
                _ignoredBridgeTreeMask = bridgeTreeMask;
            }
        }

        private void RestoreStructuralCollisions()
        {
            if (_controller != null)
            {
                foreach (var collider in _ignoredTreeColliders)
                {
                    if (collider != null)
                    {
                        Physics.IgnoreCollision(_controller, collider, ignore: false);
                    }
                }
            }

            _ignoredTreeColliders.Clear();
            _ignoredStructuralTreeMask = 0;
            _ignoredBridgeTreeMask = 0;
        }

        private static bool IsTrunkOrRootCollider(Collider collider)
        {
            if (collider.name == "TrunkMain" || collider.name == "TrunkTwisted")
            {
                return true;
            }

            var parent = collider.transform.parent;
            return collider.name == "Block" && parent != null &&
                   parent.name.StartsWith("Root_", StringComparison.Ordinal);
        }

        private static bool IsSpiralCollider(Collider collider)
        {
            return collider.name.StartsWith("SpiralStep_", StringComparison.Ordinal) ||
                   collider.name.StartsWith("WofTraversalSpiralSupport", StringComparison.Ordinal);
        }

        private void EnsureContinuousSpiralSupports()
        {
            if (_spiralSupportsReady)
            {
                return;
            }

            var mobilePerformanceMode = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps(
                steps: mobilePerformanceMode
                    ? WofTreeHouseVillageLayout.MobileSpiralStepCount
                    : WofTreeHouseVillageLayout.DesktopSpiralStepCount);
            WofTreeHouseTraversalRules.BuildContinuousSpiralSupport(
                steps,
                out var vertices,
                out var triangles);
            if (vertices.Length == 0 || triangles.Length == 0)
            {
                return;
            }

            var supportName = mobilePerformanceMode
                ? "WofTraversalSpiralSupportMobile"
                : "WofTraversalSpiralSupportDesktop";
            var readyCount = 0;
            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var treeObject = GameObject.Find($"GiantTree_{treeIndex}");
                if (treeObject == null)
                {
                    continue;
                }

                var existing = treeObject.transform.Find(supportName);
                if (existing == null)
                {
                    var support = new GameObject(supportName)
                    {
                        hideFlags = HideFlags.DontSave,
                        layer = treeObject.layer
                    };
                    support.transform.SetParent(treeObject.transform, worldPositionStays: false);
                    var mesh = new Mesh
                    {
                        name = $"{supportName}_{treeIndex}",
                        hideFlags = HideFlags.DontSave
                    };
                    mesh.vertices = vertices;
                    mesh.triangles = triangles;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    var meshCollider = support.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = mesh;
                }

                readyCount++;
            }

            _spiralSupportsReady = readyCount == WofTreeHouseVillageLayout.Trees.Count;
            if (_spiralSupportsReady)
            {
                Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_CONTINUOUS_SUPPORT_READY trees={readyCount} steps={steps.Count} mobile={mobilePerformanceMode}");
            }
        }

        private IEnumerator RunControllerProbe(string captureRoot)
        {
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

            var tree = WofTreeHouseVillageLayout.Trees[0];
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps();
            if (steps.Count < 8)
            {
                Fail("spiral-data");
                yield break;
            }

            var firstStep = ResolveWorldStep(tree, steps[1].Position);
            // The automation coroutine begins during Configure, before this
            // component's first LateUpdate. Arm the same collision assist a
            // walking player receives before placing the probe on the tread.
            SetStructuralCollisionAssist(1 << 0, 0);
            if (!_player.PrepareForAutomationVillagerInteractionProbe(firstStep + Vector3.up * 1.4f, 0f, -6f))
            {
                Fail("spiral-position");
                yield break;
            }

            var gamepad = InputSystem.AddDevice<Gamepad>("WOF Tree House Traversal QA Controller");
            try
            {
                gamepad.MakeCurrent();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return WaitUntilGrounded(5f);
                if (!_player.IsGrounded)
                {
                    Fail("spiral-start-grounded", $" position={_player.transform.position}");
                    yield break;
                }

                yield return Capture(captureRoot, "treehouse-spiral-start.png");
                var startY = _player.transform.position.y;
                var highestY = startY;
                var movementFrames = 0;
                var groundedFrames = 0;
                var midpointCaptured = false;
                Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_SPIRAL_PROBE_START steps={steps.Count} position={_player.transform.position}");

                for (var index = 2; index < steps.Count; index++)
                {
                    var target = ResolveWorldStep(tree, steps[index].Position);
                    var bestDistance = HorizontalDistance(_player.transform.position, target);
                    var lastProgressAt = Time.realtimeSinceStartup;
                    var deadline = Time.realtimeSinceStartup + 5f;
                    while (HorizontalDistance(_player.transform.position, target) > 0.72f)
                    {
                        var current = _player.transform.position;
                        var direction = target - current;
                        direction.y = 0f;
                        if (direction.sqrMagnitude <= 0.0001f ||
                            !_player.ApplyAutomationMovementHeading(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg))
                        {
                            Fail("spiral-heading", $" step={index} position={current}");
                            yield break;
                        }

                        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                        yield return null;
                        movementFrames++;
                        if (_player.IsGrounded) groundedFrames++;
                        highestY = Mathf.Max(highestY, _player.transform.position.y);
                        var distance = HorizontalDistance(_player.transform.position, target);
                        if (distance < bestDistance - 0.12f)
                        {
                            bestDistance = distance;
                            lastProgressAt = Time.realtimeSinceStartup;
                        }
                        if (_player.transform.position.y < tree.Position.y - 3f)
                        {
                            Fail("spiral-fall", $" step={index} position={_player.transform.position}");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup - lastProgressAt > 2.2f ||
                            Time.realtimeSinceStartup > deadline)
                        {
                            Fail("spiral-stalled", $" step={index} distance={distance:F2} position={_player.transform.position}");
                            yield break;
                        }
                    }

                    if (!midpointCaptured && index >= steps.Count / 2)
                    {
                        midpointCaptured = true;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return Capture(captureRoot, "treehouse-spiral-midpoint.png");
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForFixedUpdate();
                var groundedRatio = movementFrames == 0 ? 0f : groundedFrames / (float)movementFrames;
                var minimumTopY = tree.Position.y + 13.5f;
                if (highestY < minimumTopY || groundedRatio < 0.45f)
                {
                    Fail("spiral-final", $" highestY={highestY:F2} minimumY={minimumTopY:F2} groundedRatio={groundedRatio:F3}");
                    yield break;
                }
                yield return Capture(captureRoot, "treehouse-spiral-top.png");
                Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_SPIRAL_TRAVERSAL_PASS highestY={highestY:F2} groundedRatio={groundedRatio:F3} position={_player.transform.position}");

                var bridge = WofTreeHouseVillageLayout.Bridges[0];
                var bridgeStart = WofTreeHouseVillageLayout.GetHouseBalconyPosition(bridge.StartTree, bridge.StartHouse);
                var bridgeEnd = WofTreeHouseVillageLayout.GetHouseBalconyPosition(bridge.EndTree, bridge.EndHouse);
                var bridgeDirection = bridgeEnd - bridgeStart;
                var bridgeLength = bridgeDirection.magnitude;
                bridgeDirection = bridgeLength > 0.0001f ? bridgeDirection / bridgeLength : Vector3.forward;
                var accessibleInset = Mathf.Min(5.2f, bridgeLength * 0.2f);
                var traversalStart = bridgeStart + bridgeDirection * accessibleInset + Vector3.up * 0.45f;
                var traversalEnd = bridgeEnd - bridgeDirection * accessibleInset;
                if (!_player.PrepareForAutomationVillagerInteractionProbe(
                        traversalStart,
                        Mathf.Atan2(bridgeDirection.x, bridgeDirection.z) * Mathf.Rad2Deg,
                        -4f))
                {
                    Fail("bridge-position");
                    yield break;
                }

                yield return WaitUntilGrounded(5f);
                yield return Capture(captureRoot, "treehouse-bridge-start.png");
                if (!_player.IsGrounded)
                {
                    Fail("bridge-start-grounded", $" position={_player.transform.position}");
                    yield break;
                }

                var bridgeBestDistance = Vector3.Distance(_player.transform.position, traversalEnd);
                var bridgeLastProgressAt = Time.realtimeSinceStartup;
                var bridgeDeadline = Time.realtimeSinceStartup + Mathf.Max(8f, bridgeLength / WofGameConstants.WalkSpeed + 5f);
                var bridgeStartPosition = _player.transform.position;
                var bridgeMaximumCrossTrack = 0f;
                while (Vector3.Distance(_player.transform.position, traversalEnd) > 1.1f)
                {
                    var current = _player.transform.position;
                    var direction = traversalEnd - current;
                    direction.y = 0f;
                    if (direction.sqrMagnitude <= 0.0001f ||
                        !_player.ApplyAutomationMovementHeading(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg))
                    {
                        Fail("bridge-heading", $" position={current}");
                        yield break;
                    }
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                    yield return null;
                    bridgeMaximumCrossTrack = Mathf.Max(
                        bridgeMaximumCrossTrack,
                        WofTreeHouseTraversalRules.DistanceToSegment(_player.transform.position, bridgeStart, bridgeEnd));
                    var distance = Vector3.Distance(_player.transform.position, traversalEnd);
                    if (distance < bridgeBestDistance - 0.12f)
                    {
                        bridgeBestDistance = distance;
                        bridgeLastProgressAt = Time.realtimeSinceStartup;
                    }
                    if (_player.transform.position.y < Mathf.Min(bridgeStart.y, bridgeEnd.y) - 5f)
                    {
                        Fail("bridge-fall", $" position={_player.transform.position}");
                        yield break;
                    }
                    if (Time.realtimeSinceStartup - bridgeLastProgressAt > 2.2f ||
                        Time.realtimeSinceStartup > bridgeDeadline)
                    {
                        InputSystem.QueueStateEvent(gamepad, new GamepadState());
                        yield return Capture(captureRoot, "treehouse-bridge-stall.png");
                        Fail(
                            "bridge-stalled",
                            $" distance={distance:F2} position={_player.transform.position} nearby={DescribeNearbyColliders()}");
                        yield break;
                    }
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForFixedUpdate();
                var bridgeTravel = Vector3.Distance(bridgeStartPosition, _player.transform.position);
                if (bridgeTravel < Mathf.Max(4f, bridgeLength - accessibleInset * 2f - 2f) ||
                    bridgeMaximumCrossTrack > 2.8f)
                {
                    Fail("bridge-final", $" travel={bridgeTravel:F2} length={bridgeLength:F2} maxCrossTrack={bridgeMaximumCrossTrack:F2}");
                    yield break;
                }
                yield return Capture(captureRoot, "treehouse-bridge-end.png");
                Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_BRIDGE_TRAVERSAL_PASS nativeGamepad=true travel={bridgeTravel:F2} length={bridgeLength:F2} maxCrossTrack={bridgeMaximumCrossTrack:F2} position={_player.transform.position}");
                Debug.Log("[WOF-AUTOMATION] TREEHOUSE_CONTROLLER_PROBE_COMPLETE");
            }
            finally
            {
                if (gamepad.added)
                {
                    InputSystem.RemoveDevice(gamepad);
                }
            }
        }

        private IEnumerator WaitUntilGrounded(float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!_player.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static Vector3 ResolveWorldStep(WofTreeHouseTreePlacement tree, Vector3 localStep)
        {
            return tree.Position + Quaternion.Euler(0f, tree.YawRadians * Mathf.Rad2Deg, 0f) * localStep;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private string DescribeNearbyColliders()
        {
            var center = _controller == null ? transform.position : _controller.bounds.center;
            var nearby = Physics.OverlapSphere(center, 2.5f, ~0, QueryTriggerInteraction.Ignore);
            var descriptions = new List<string>(nearby.Length);
            foreach (var collider in nearby)
            {
                if (collider == null || collider == _controller)
                {
                    continue;
                }

                descriptions.Add(BuildHierarchyPath(collider.transform));
            }
            descriptions.Sort(StringComparer.Ordinal);
            return descriptions.Count == 0 ? "none" : string.Join("|", descriptions);
        }

        private static string BuildHierarchyPath(Transform target)
        {
            var parts = new List<string>();
            for (var current = target; current != null && parts.Count < 6; current = current.parent)
            {
                parts.Add(current.name);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static IEnumerator Capture(string root, string fileName)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(root, fileName);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 4f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                Fail("screenshot", $" file={fileName}");
            }
        }

        private static bool TryResolveProbeRoot(out string root)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(ProbeArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var raw = argument.Substring(ProbeArgumentPrefix.Length).Trim('"');
                if (string.IsNullOrWhiteSpace(raw))
                {
                    break;
                }
                var full = Path.GetFullPath(raw);
                if (!full.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                Directory.CreateDirectory(full);
                root = full;
                return true;
            }

            root = null;
            return false;
        }

        private static void Fail(string stage, string detail = "")
        {
            Debug.LogError($"[WOF-AUTOMATION] TREEHOUSE_CONTROLLER_PROBE_FAILED stage={stage}{detail}");
        }
    }
}
