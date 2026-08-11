using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    public readonly struct WofTrainingDummyDamageResult
    {
        public WofTrainingDummyDamageResult(bool applied, float health, double respawnAt, int hitSequence)
        {
            Applied = applied;
            Health = health;
            RespawnAt = respawnAt;
            HitSequence = hitSequence;
        }

        public bool Applied { get; }
        public float Health { get; }
        public double RespawnAt { get; }
        public int HitSequence { get; }
        public bool IsDown => Health <= 0f;
    }

    /// <summary>
    /// Exact combat values used by the React spell-dummy QA runtime.
    /// Dummy damage intentionally differs from player-versus-player damage.
    /// </summary>
    public static class WofTrainingDummyCombatRules
    {
        public const float MaxHealth = 120f;
        public const double RespawnSeconds = 1.8d;
        public const float HitRadius = 1.7f;

        public static float GetDamage(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball => 24f,
                WofSpellId.IceShard => 34f,
                WofSpellId.ArcaneBeam => 34f,
                WofSpellId.RingsOfPower => 22f,
                WofSpellId.Lightning => 32f,
                WofSpellId.Flamethrower => 6f,
                WofSpellId.Kunai => 18f,
                WofSpellId.Tornado => 12f,
                WofSpellId.MeteorShower => 30f,
                WofSpellId.Sleep => 5f,
                WofSpellId.Poison => 7f,
                WofSpellId.Acid => 9f,
                _ => 0f
            };
        }

        public static WofTrainingDummyDamageResult Apply(
            float currentHealth,
            int currentHitSequence,
            WofSpellId spell,
            double now)
        {
            var health = Mathf.Clamp(currentHealth, 0f, MaxHealth);
            var damage = GetDamage(spell);
            if (health <= 0f || damage <= 0f || !double.IsFinite(now))
                return new WofTrainingDummyDamageResult(false, health, 0d, currentHitSequence);

            var nextHealth = Mathf.Max(0f, health - damage);
            return new WofTrainingDummyDamageResult(
                true,
                nextHealth,
                nextHealth <= 0f ? now + RespawnSeconds : 0d,
                currentHitSequence == int.MaxValue ? 1 : currentHitSequence + 1);
        }

        public static bool IsRespawnDue(float health, double respawnAt, double now)
        {
            return health <= 0f && respawnAt > 0d && double.IsFinite(respawnAt) &&
                   double.IsFinite(now) && now >= respawnAt;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WofTrainingDummyRuntime : MonoBehaviour
    {
        private const float HitReactionSeconds = 0.14f;

        private Transform _model;
        private Collider _collider;
        private Canvas _healthCanvas;
        private Image _healthFill;
        private Text _healthLabel;
        private int _hitSequence = -1;
        private float _health = WofTrainingDummyCombatRules.MaxHealth;
        private float _hitReactionUntil;
        private string _instanceId = string.Empty;

        public string InstanceId => _instanceId;
        public float Health => _health;
        public bool IsDown => _health <= 0f;
        public Vector3 DamageProbePosition => transform.position;

        public void Initialize(string instanceId, Transform model, Collider dummyCollider)
        {
            _instanceId = instanceId ?? string.Empty;
            _model = model;
            _collider = dummyCollider;
            BuildHealthDisplay();
        }

        public void ApplyState(WofEnginePlaceableRecord record)
        {
            var nextHealth = Mathf.Clamp(record.trainingDummyHealth, 0f, WofTrainingDummyCombatRules.MaxHealth);
            if (_hitSequence >= 0 && record.trainingDummyHitSequence != _hitSequence)
                _hitReactionUntil = Time.unscaledTime + HitReactionSeconds;
            _hitSequence = record.trainingDummyHitSequence;
            _health = nextHealth;

            if (_collider != null) _collider.enabled = !IsDown;
            RefreshHealthDisplay();
            RefreshModelScale();
        }

        public bool ApplyServerSpellImpact(WofSpellId spell, ulong sourceClientId)
        {
            if (IsDown || string.IsNullOrEmpty(_instanceId)) return false;
            var players = FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None);
            for (var index = 0; index < players.Length; index++)
            {
                if (players[index].ApplyServerTrainingDummySpellImpact(_instanceId, spell, sourceClientId))
                    return true;
            }
            return false;
        }

        public static int ApplyServerAreaSpellImpact(
            Vector3 center,
            float radius,
            WofSpellId spell,
            ulong sourceClientId)
        {
            var hitCount = 0;
            var hitRadius = Mathf.Max(0f, radius) + WofTrainingDummyCombatRules.HitRadius;
            var hitRadiusSquared = hitRadius * hitRadius;
            var dummies = FindObjectsByType<WofTrainingDummyRuntime>(FindObjectsSortMode.None);
            for (var index = 0; index < dummies.Length; index++)
            {
                var dummy = dummies[index];
                if (dummy == null || dummy.IsDown ||
                    Vector3.SqrMagnitude(dummy.DamageProbePosition - center) > hitRadiusSquared) continue;
                if (dummy.ApplyServerSpellImpact(spell, sourceClientId)) hitCount++;
            }
            return hitCount;
        }

        public static int ApplyServerHitscanSpellImpact(
            Vector3 origin,
            Vector3 direction,
            float range,
            float radius,
            WofSpellId spell,
            ulong sourceClientId)
        {
            if (range <= 0f || direction.sqrMagnitude <= 0.000001f) return 0;
            var line = direction.normalized * range;
            var lineLengthSquared = line.sqrMagnitude;
            var hitCount = 0;
            var dummies = FindObjectsByType<WofTrainingDummyRuntime>(FindObjectsSortMode.None);
            for (var index = 0; index < dummies.Length; index++)
            {
                var dummy = dummies[index];
                if (dummy == null || dummy.IsDown) continue;
                var toTarget = dummy.DamageProbePosition - origin;
                var projection = Mathf.Clamp01(Vector3.Dot(toTarget, line) / lineLengthSquared);
                var closest = origin + line * projection;
                var hitRadius = Mathf.Max(0f, radius) + WofTrainingDummyCombatRules.HitRadius;
                if (Vector3.SqrMagnitude(closest - dummy.DamageProbePosition) > hitRadius * hitRadius) continue;
                if (dummy.ApplyServerSpellImpact(spell, sourceClientId)) hitCount++;
            }
            return hitCount;
        }

        private void LateUpdate()
        {
            RefreshModelScale();
            var camera = Camera.main;
            if (_healthCanvas != null && camera != null)
                _healthCanvas.transform.rotation = camera.transform.rotation;
        }

        private void RefreshModelScale()
        {
            if (_model == null) return;
            var scale = IsDown ? new Vector3(1.08f, 0.34f, 1.08f) : Vector3.one;
            if (!IsDown && Time.unscaledTime < _hitReactionUntil)
            {
                var pulse = 1f + Mathf.Sin((HitReactionSeconds - (_hitReactionUntil - Time.unscaledTime)) /
                                          HitReactionSeconds * Mathf.PI) * 0.06f;
                scale = new Vector3(pulse, 1f / pulse, pulse);
            }
            _model.localScale = scale;
        }

        private void BuildHealthDisplay()
        {
            var root = new GameObject("HealthDisplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 2.72f, 0f);
            root.transform.localScale = Vector3.one * 0.012f;
            _healthCanvas = root.GetComponent<Canvas>();
            _healthCanvas.renderMode = RenderMode.WorldSpace;
            _healthCanvas.sortingOrder = 24;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(108f, 42f);

            var panel = root.AddComponent<Image>();
            panel.color = new Color32(2, 6, 23, 230);
            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color32(249, 115, 22, 255);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.38f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4f, 0f);
            labelRect.offsetMax = new Vector2(-4f, -1f);
            _healthLabel = labelObject.GetComponent<Text>();
            _healthLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _healthLabel.fontSize = 11;
            _healthLabel.fontStyle = FontStyle.Bold;
            _healthLabel.alignment = TextAnchor.MiddleCenter;
            _healthLabel.color = new Color32(229, 231, 235, 255);

            var bar = new GameObject("HealthBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0.38f);
            barRect.offsetMin = new Vector2(5f, 5f);
            barRect.offsetMax = new Vector2(-5f, -2f);
            bar.GetComponent<Image>().color = new Color32(69, 10, 10, 255);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            _healthFill = fill.GetComponent<Image>();
            RefreshHealthDisplay();
        }

        private void RefreshHealthDisplay()
        {
            var ratio = Mathf.Clamp01(_health / WofTrainingDummyCombatRules.MaxHealth);
            if (_healthLabel != null)
                _healthLabel.text = $"Spell Dummy  {Mathf.RoundToInt(_health)}/{Mathf.RoundToInt(WofTrainingDummyCombatRules.MaxHealth)}";
            if (_healthFill == null) return;
            var rect = _healthFill.rectTransform;
            rect.anchorMax = new Vector2(ratio, 1f);
            _healthFill.color = ratio > 0.48f
                ? new Color32(34, 197, 94, 255)
                : ratio > 0.22f
                    ? new Color32(245, 158, 11, 255)
                    : new Color32(239, 68, 68, 255);
        }
    }
}
