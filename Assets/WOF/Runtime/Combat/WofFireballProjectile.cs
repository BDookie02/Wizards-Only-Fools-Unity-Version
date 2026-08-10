using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace WOF
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WofFireballProjectile : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private Sprite[] spellThumbnails;
        [SerializeField] private MeshRenderer glowRenderer;
        [SerializeField] private Light spellLight;

        private readonly NetworkVariable<Vector3> _direction = new(
            Vector3.forward,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _sourceClientId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _spellValue = new(
            (int)WofSpellId.Fireball,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _anchored = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private static readonly Dictionary<ulong, List<WofFireballProjectile>> PortalEndpoints = new();
        private static readonly Dictionary<ulong, double> PortalTravelerCooldowns = new();
        private MaterialPropertyBlock _glowProperties;

        private double _despawnAt;
        private double _impactAt;
        private float _animationClock;
        private float _areaTickClock;
        private int _frameIndex;
        private bool _areaImpactApplied;
        private WofSpellId _spell;

        public WofSpellId Spell => _spell;
        public ulong SourceClientId => _sourceClientId.Value;

        public void InitializeServer(ulong sourceClientId, Vector3 direction, WofSpellId spell)
        {
            _sourceClientId.Value = sourceClientId;
            _direction.Value = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
            _spellValue.Value = (int)spell;
            _spell = spell;
        }

        public override void OnNetworkSpawn()
        {
            _spellValue.OnValueChanged += HandleSpellChanged;
            _anchored.OnValueChanged += HandleAnchoredChanged;
            _spell = WofSpellLoadout.IsValid(_spellValue.Value)
                ? (WofSpellId)_spellValue.Value
                : WofSpellId.Fireball;
            ConfigureVisual();

            if (IsServer)
            {
                var now = NetworkManager.ServerTime.Time;
                _despawnAt = now + WofSpellRuntimeTuning.GetLifetimeSeconds(_spell);
                _impactAt = now + (_spell == WofSpellId.MeteorShower ? 1.25d : 0d);
                if (_spell == WofSpellId.Portal)
                {
                    // Portals become endpoints on collision. A missed shot still expires.
                    _despawnAt = now + 12d;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            _spellValue.OnValueChanged -= HandleSpellChanged;
            _anchored.OnValueChanged -= HandleAnchoredChanged;
            RemovePortalEndpoint();
        }

        private void Update()
        {
            AnimateSprite();
            FaceCamera();
            if (!IsServer) return;

            var now = NetworkManager.ServerTime.Time;
            if (now >= _despawnAt)
            {
                NetworkObject.Despawn(true);
                return;
            }

            var mode = WofSpellRuntimeTuning.GetMode(_spell);
            if (mode == WofSpellRuntimeMode.GroundArea ||
                _spell is WofSpellId.DiscShield or WofSpellId.OrbShield)
            {
                UpdateAreaEffect(now);
                return;
            }
            if (_spell == WofSpellId.Portal && _anchored.Value)
            {
                UpdatePortalTraversal(now);
                return;
            }
            if (_spell == WofSpellId.SmokeBomb && _anchored.Value) return;
            if (mode == WofSpellRuntimeMode.Hitscan || mode == WofSpellRuntimeMode.Self)
            {
                return;
            }

            MoveProjectile();
        }

        private void MoveProjectile()
        {
            var direction = _direction.Value.normalized;
            var distance = WofSpellRuntimeTuning.GetSpeed(_spell) * Time.deltaTime;
            if (distance <= 0f) return;

            if (Physics.SphereCast(
                    transform.position,
                    _spell == WofSpellId.RingsOfPower ? 0.5f : 0.32f,
                    direction,
                    out var hit,
                    distance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                var player = hit.collider.GetComponentInParent<WofPlayerController>();
                if (player != null && player.OwnerClientId == _sourceClientId.Value)
                {
                    transform.position += direction * distance;
                    return;
                }

                if (_spell == WofSpellId.Portal)
                {
                    AnchorPortal(hit.point - direction * 1.5f + Vector3.up);
                    return;
                }
                if (_spell == WofSpellId.SmokeBomb)
                {
                    _anchored.Value = true;
                    transform.position = hit.point + Vector3.up * 2f;
                    _despawnAt = NetworkManager.ServerTime.Time + 6.5d;
                    ConfigureVisual();
                    return;
                }

                if (player != null)
                {
                    if (!player.HasActiveSpellShield)
                    {
                        player.ApplyServerSpellImpact(_spell, _sourceClientId.Value);
                    }
                    NetworkObject.Despawn(true);
                    return;
                }

                if (_spell == WofSpellId.Kunai && TryGetSourcePlayer(out var source))
                {
                    source.ApplyServerKunaiPull(hit.point);
                }
                NetworkObject.Despawn(true);
                return;
            }

            transform.position += direction * distance;
        }

        private void UpdateAreaEffect(double now)
        {
            if (_spell is WofSpellId.DiscShield or WofSpellId.OrbShield)
            {
                if (TryGetSourcePlayer(out var owner))
                {
                    var offset = _spell == WofSpellId.DiscShield
                        ? owner.transform.forward * 2.2f + Vector3.up * 1.1f
                        : Vector3.up * 1.1f;
                    transform.position = owner.transform.position + offset;
                }
                return;
            }

            if (_spell == WofSpellId.Lightning && !_areaImpactApplied)
            {
                _areaImpactApplied = true;
                return;
            }
            if (_spell == WofSpellId.MeteorShower && !_areaImpactApplied && now >= _impactAt)
            {
                _areaImpactApplied = true;
                ApplyAreaDamage(WofSpellRuntimeTuning.MeteorRadius, 18f, false);
                return;
            }
            if (_spell == WofSpellId.Tornado)
            {
                _areaTickClock += Time.deltaTime;
                if (_areaTickClock < 0.2f) return;
                _areaTickClock = 0f;
                ApplyTornadoPull();
                return;
            }
            if (_spell == WofSpellId.HealingCrystals)
            {
                _areaTickClock += Time.deltaTime;
                if (_areaTickClock < 0.1f) return;
                var delta = _areaTickClock;
                _areaTickClock = 0f;
                foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
                {
                    if (!player.IsSpawned || player.IsDead ||
                        Vector3.SqrMagnitude(player.transform.position - transform.position) >=
                        WofSpellRuntimeTuning.HealingCrystalRadius * WofSpellRuntimeTuning.HealingCrystalRadius)
                        continue;
                    player.ApplyServerHealing(
                        WofSpellRuntimeTuning.HealingCrystalHealPerSecond * delta,
                        clearToxicEffects: true);
                }
            }
        }

        private void ApplyAreaDamage(float radius, float damage, bool pull)
        {
            var radiusSquared = radius * radius;
            foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned || player.IsDead || player.OwnerClientId == _sourceClientId.Value ||
                    Vector3.SqrMagnitude(player.transform.position - transform.position) > radiusSquared)
                    continue;
                if (!player.HasActiveSpellShield) player.ApplyServerDamage(damage, _sourceClientId.Value);
                if (pull) player.ApplyServerTornadoPull(transform.position);
            }
        }

        private void ApplyTornadoPull()
        {
            var radiusSquared = WofSpellRuntimeTuning.TornadoRadius * WofSpellRuntimeTuning.TornadoRadius;
            foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned || player.IsDead || player.OwnerClientId == _sourceClientId.Value ||
                    Vector3.SqrMagnitude(player.transform.position - transform.position) > radiusSquared)
                    continue;
                player.ApplyServerTornadoPull(transform.position);
            }
        }

        private void AnchorPortal(Vector3 position)
        {
            _anchored.Value = true;
            transform.position = position;
            _despawnAt = NetworkManager.ServerTime.Time + 12d;
            if (!PortalEndpoints.TryGetValue(_sourceClientId.Value, out var endpoints))
            {
                endpoints = new List<WofFireballProjectile>(2);
                PortalEndpoints.Add(_sourceClientId.Value, endpoints);
            }
            endpoints.RemoveAll(endpoint => endpoint == null || !endpoint.IsSpawned);
            while (endpoints.Count >= 2)
            {
                var oldest = endpoints[0];
                endpoints.RemoveAt(0);
                if (oldest != null && oldest.IsSpawned) oldest.NetworkObject.Despawn(true);
            }
            endpoints.Add(this);
            ConfigureVisual();
        }

        private void UpdatePortalTraversal(double now)
        {
            if (!PortalEndpoints.TryGetValue(_sourceClientId.Value, out var endpoints)) return;
            endpoints.RemoveAll(endpoint => endpoint == null || !endpoint.IsSpawned || !endpoint._anchored.Value);
            if (endpoints.Count != 2) return;
            var other = endpoints[0] == this ? endpoints[1] : endpoints[0];
            foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned || player.IsDead ||
                    Vector3.SqrMagnitude(player.transform.position - transform.position) > 2.4f * 2.4f)
                    continue;
                if (PortalTravelerCooldowns.TryGetValue(player.OwnerClientId, out var until) && now < until) continue;
                PortalTravelerCooldowns[player.OwnerClientId] = now + 0.65d;
                player.ApplyServerPortalTeleport(other.transform.position + Vector3.up * 0.25f);
            }
        }

        private void RemovePortalEndpoint()
        {
            if (!PortalEndpoints.TryGetValue(_sourceClientId.Value, out var endpoints)) return;
            endpoints.Remove(this);
            endpoints.RemoveAll(endpoint => endpoint == null || !endpoint.IsSpawned);
            if (endpoints.Count == 0) PortalEndpoints.Remove(_sourceClientId.Value);
        }

        private bool TryGetSourcePlayer(out WofPlayerController player)
        {
            foreach (var candidate in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
            {
                if (candidate.IsSpawned && candidate.OwnerClientId == _sourceClientId.Value)
                {
                    player = candidate;
                    return true;
                }
            }
            player = null;
            return false;
        }

        private void HandleSpellChanged(int previous, int current)
        {
            _spell = WofSpellLoadout.IsValid(current) ? (WofSpellId)current : WofSpellId.Fireball;
            ConfigureVisual();
        }

        private void HandleAnchoredChanged(bool previous, bool current)
        {
            ConfigureVisual();
        }

        private void ConfigureVisual()
        {
            if (spriteRenderer != null)
            {
                var sprite = _spell == WofSpellId.Fireball && frames != null && frames.Length > 0
                    ? frames[0]
                    : spellThumbnails != null && (int)_spell >= 0 && (int)_spell < spellThumbnails.Length
                        ? spellThumbnails[(int)_spell]
                        : null;
                if (sprite != null) spriteRenderer.sprite = sprite;
                spriteRenderer.color = Color.white;
                var scale = WofSpellRuntimeTuning.GetVisualScale(_spell);
                if (_anchored.Value && _spell == WofSpellId.SmokeBomb) scale *= 2.2f;
                spriteRenderer.transform.localScale = Vector3.one * scale;
            }

            var color = WofSpellRuntimeTuning.GetColor(_spell);
            if (spellLight != null)
            {
                spellLight.color = color;
                spellLight.intensity = _spell is WofSpellId.Lightning or WofSpellId.Fireball ? 4f : 2.2f;
                spellLight.range = Mathf.Max(5f, WofSpellRuntimeTuning.GetVisualScale(_spell));
            }
            if (glowRenderer != null)
            {
                _glowProperties ??= new MaterialPropertyBlock();
                glowRenderer.GetPropertyBlock(_glowProperties);
                _glowProperties.SetColor("_BaseColor", color);
                _glowProperties.SetColor("_Color", color);
                glowRenderer.SetPropertyBlock(_glowProperties);
                glowRenderer.transform.localScale = Vector3.one * Mathf.Clamp(
                    WofSpellRuntimeTuning.GetVisualScale(_spell) * 0.18f,
                    0.24f,
                    2.6f);
            }
        }

        private void AnimateSprite()
        {
            if (_spell != WofSpellId.Fireball || spriteRenderer == null || frames == null || frames.Length == 0)
                return;
            _animationClock += Time.deltaTime;
            if (_animationClock < 0.1f) return;
            _animationClock -= 0.1f;
            _frameIndex = (_frameIndex + 1) % frames.Length;
            spriteRenderer.sprite = frames[_frameIndex];
        }

        private void FaceCamera()
        {
            var camera = Camera.main;
            if (camera != null && spriteRenderer != null)
            {
                spriteRenderer.transform.rotation = Quaternion.LookRotation(
                    spriteRenderer.transform.position - camera.transform.position);
            }
        }
    }
}
