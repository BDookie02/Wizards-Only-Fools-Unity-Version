using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofVillagerManager : MonoBehaviour
    {
        [SerializeField] private WofVillagerBillboard[] villagers;

        private Camera _camera;
        private readonly List<Vector3> _facingTargets = new(WofGameConstants.MaxPlayers);
        private WofPlayerController[] _playerControllers = Array.Empty<WofPlayerController>();
        private WofVillagerBillboard _insideVillager;
        private float _nextRuntimeTickAt;
        private float _nextVisibilityUpdateAt;
        private float _nextPlayerRefreshAt;
        private int _lastVisibleCount = -1;
        private int _lastFacingTargetCount = -1;

        public int VillagerCount => villagers?.Length ?? 0;
        public WofVillagerBillboard InsideVillager => _insideVillager;

        public string GetReactDisplayName(WofVillagerBillboard villager)
        {
            if (villager == null)
            {
                return string.Empty;
            }
            if (villager.IsDarrel)
            {
                return "Darrel";
            }
            if (!string.IsNullOrWhiteSpace(villager.ReactDisplayName))
            {
                return villager.ReactDisplayName;
            }
            if (villagers != null)
            {
                for (var index = 0; index < villagers.Length; index++)
                {
                    if (villagers[index] == villager)
                    {
                        return $"Town Villager {index + 1}";
                    }
                }
            }
            return "Town Villager";
        }

        public string GetReactTownId(WofVillagerBillboard villager)
        {
            return villager == null || string.IsNullOrWhiteSpace(villager.ReactTownId)
                ? "base-village"
                : villager.ReactTownId;
        }

        public void Configure(WofVillagerBillboard[] values)
        {
            villagers = values;
        }

        public bool TryGetTargetedVillager(Camera camera, out WofVillagerBillboard targetedVillager)
        {
            targetedVillager = null;
            if (camera == null || villagers == null)
            {
                return false;
            }

            var origin = camera.transform.position;
            var direction = camera.transform.forward;
            var bestScore = float.PositiveInfinity;
            for (var index = 0; index < villagers.Length; index++)
            {
                var candidate = villagers[index];
                if (candidate == null ||
                    !WofQuestTargetMath.TryScoreTarget(origin, direction, candidate.InteractionCenter, out var score) ||
                    score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                targetedVillager = candidate;
            }

            return targetedVillager != null;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now < _nextRuntimeTickAt)
            {
                return;
            }
            _nextRuntimeTickAt = now + WofVillagerMath.RuntimeTickSeconds;

            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }
            if (_camera == null || villagers == null)
            {
                return;
            }

            var playerPosition = _camera.transform.position;
            UpdateFacingTargets(now);
            UpdateInsideVillager(playerPosition, now);
            var mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            if (now >= _nextVisibilityUpdateAt)
            {
                _nextVisibilityUpdateAt = now + WofVillagerMath.VisibilityUpdateSeconds;
                UpdateVisibility(playerPosition, now, mobile);
            }

            for (var index = 0; index < villagers.Length; index++)
            {
                var villager = villagers[index];
                if (villager != null)
                {
                    villager.TickVisual(_camera, playerPosition, _facingTargets, now, mobile);
                }
            }
        }

        private void UpdateFacingTargets(float now)
        {
            if (now >= _nextPlayerRefreshAt)
            {
                _nextPlayerRefreshAt = now + WofVillagerMath.VisibilityUpdateSeconds;
                _playerControllers = FindObjectsByType<WofPlayerController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            }

            _facingTargets.Clear();
            var localCount = 0;
            for (var index = 0; index < _playerControllers.Length; index++)
            {
                var player = _playerControllers[index];
                if (player != null && player.IsOwner && !player.IsDead)
                {
                    _facingTargets.Add(player.transform.position);
                    localCount = 1;
                    break;
                }
            }
            for (var index = 0; index < _playerControllers.Length; index++)
            {
                var player = _playerControllers[index];
                if (player == null || player.IsOwner || player.IsDead)
                {
                    continue;
                }
                _facingTargets.Add(player.transform.position);
            }
            if (_facingTargets.Count != _lastFacingTargetCount)
            {
                _lastFacingTargetCount = _facingTargets.Count;
                Debug.Log($"[WOF-AUTOMATION] VILLAGER_FACING_TARGETS active={_facingTargets.Count} local={localCount} remotes={_facingTargets.Count - localCount}");
            }
        }

        private void UpdateInsideVillager(Vector3 playerPosition, float now)
        {
            WofVillagerBillboard nextInside = null;
            var nearestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < villagers.Length; index++)
            {
                var villager = villagers[index];
                if (villager == null || !WofVillagerMath.IsPlayerInsideHut(playerPosition, villager.Hut))
                {
                    continue;
                }

                var dx = playerPosition.x - villager.Hut.x;
                var dz = playerPosition.z - villager.Hut.z;
                var distanceSquared = dx * dx + dz * dz;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nextInside = villager;
                }
            }

            if (_insideVillager == nextInside)
            {
                return;
            }
            if (_insideVillager != null)
            {
                _insideVillager.SetPlayerInside(false, now);
            }
            _insideVillager = nextInside;
            if (_insideVillager != null)
            {
                var distance = Mathf.Sqrt(nearestDistanceSquared);
                _insideVillager.SetPlayerInside(true, now, 1f - distance / 9f);
            }
        }

        private void UpdateVisibility(Vector3 playerPosition, float now, bool mobile)
        {
            var visibleCount = 0;
            for (var index = 0; index < villagers.Length; index++)
            {
                var villager = villagers[index];
                if (villager == null)
                {
                    continue;
                }

                var visible = WofVillagerMath.ShouldRender(
                    playerPosition,
                    villager.transform.position,
                    villager.IsReacting(now),
                    mobile);
                villager.SetWorldVisible(visible);
                if (visible)
                {
                    visibleCount++;
                }
            }

            if (visibleCount != _lastVisibleCount)
            {
                _lastVisibleCount = visibleCount;
                Debug.Log($"[WOF-AUTOMATION] VILLAGER_VISIBILITY visible={visibleCount} total={villagers.Length}");
            }
        }
    }
}
