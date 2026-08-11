using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofEnginePlaceableTests
    {
        [Test]
        public void Catalog_MatchesExactReactOrderIdsAndCategoryCounts()
        {
            var expected = new[]
            {
                "hut-mushroom-red", "hut-mushroom-lavender", "hut-grass-mound", "hut-log-cabin",
                "hut-dirt-grass-roof", "mountain-cabin", "swamp-treehouse-platform", "campfire-small",
                "bush-round", "training-spell-dummy", "magic-portal-marker", "spellbook-pedestal"
            };

            Assert.That(WofEnginePlaceableCatalog.All.Select(value => value.Id), Is.EqualTo(expected));
            Assert.That(WofEnginePlaceableCatalog.All.Count(value => value.Category == WofEnginePlaceableCategory.Huts), Is.EqualTo(5));
            Assert.That(WofEnginePlaceableCatalog.All.Count(value => value.Category == WofEnginePlaceableCategory.Village), Is.EqualTo(2));
            Assert.That(WofEnginePlaceableCatalog.All.Count(value => value.Category == WofEnginePlaceableCategory.Magic), Is.EqualTo(2));
            Assert.That(WofEnginePlaceableCatalog.MaximumPlacedObjects, Is.EqualTo(64));
            Assert.That(WofEnginePlaceableCatalog.MaximumSaveSlots, Is.EqualTo(6));
        }

        [Test]
        public void Catalog_PreservesReactPlacementMetadata()
        {
            var platform = WofEnginePlaceableCatalog.Find("swamp-treehouse-platform");
            var campfire = WofEnginePlaceableCatalog.Find("campfire-small");

            Assert.That(platform.FootprintRadius, Is.EqualTo(13f));
            Assert.That(platform.MaxSlopeDelta, Is.EqualTo(2f));
            Assert.That(platform.HeightOffset, Is.EqualTo(2.2f));
            Assert.That(campfire.YawMode, Is.EqualTo(WofEnginePlaceableYawMode.Random));
            Assert.That(WofEnginePlaceableCatalog.GetDefaultGridSize(platform), Is.EqualTo(2));
            Assert.That(WofEnginePlaceableCatalog.GetDefaultGridSize(campfire), Is.EqualTo(1));
        }

        [Test]
        public void Plan_SnapsCoordinatesAndSamplesFiveReactSurfacePoints()
        {
            var definition = WofEnginePlaceableCatalog.Find("hut-log-cabin");
            var sampled = new List<Vector2>();
            float Height(float x, float z)
            {
                sampled.Add(new Vector2(x, z));
                return 3f;
            }

            var plan = WofEnginePlacementMath.Plan(
                definition, Height, Vector3.zero, 0.4f, true, 2f, 3.4f, -4.6f);

            Assert.That(plan.Ok, Is.True);
            Assert.That(plan.Position, Is.EqualTo(new Vector3(4f, 3f, -4f)));
            Assert.That(plan.YawRadians, Is.EqualTo(0.4f));
            Assert.That(plan.GridSize, Is.EqualTo(2f));
            Assert.That(plan.Snapped, Is.True);
            Assert.That(sampled, Has.Count.EqualTo(5));
        }

        [Test]
        public void ValidateSurface_RejectsReactSlopeDelta()
        {
            var definition = WofEnginePlaceableCatalog.Find("campfire-small");
            float Height(float x, float z) => x > 0f ? 2f : 0f;

            var plan = WofEnginePlacementMath.ValidateSurface(definition, Height, 0f, 0f);

            Assert.That(plan.Ok, Is.False);
            Assert.That(plan.Reason, Is.EqualTo("too steep for that object"));
        }

        [Test]
        public void TrainingDummy_UsesPlayerHeightAndEightUnitReactSpawnWithoutPersistence()
        {
            var plan = WofEnginePlacementMath.PlanTrainingDummy(new Vector3(3f, 7f, 11f), Mathf.PI * 0.5f);
            var sanitized = WofEnginePlaceableStorage.SanitizeObjects(new[]
            {
                new WofEnginePlaceableRecord
                {
                    instanceId = "dummy", placeableId = "training-spell-dummy", label = "Spell Dummy",
                    x = plan.Position.x, y = plan.Position.y, z = plan.Position.z, yaw = plan.YawRadians
                }
            });

            Assert.That(plan.Ok, Is.True);
            Assert.That(plan.Position.x, Is.EqualTo(11f).Within(0.0001f));
            Assert.That(plan.Position.y, Is.EqualTo(7f));
            Assert.That(plan.Position.z, Is.EqualTo(11f).Within(0.0001f));
            Assert.That(plan.YawRadians, Is.EqualTo(Mathf.PI * 0.5f));
            Assert.That(sanitized, Is.Empty);
        }

        [Test]
        public void TrainingDummy_UsesExactReactHealthDamageAndRespawnTiming()
        {
            Assert.That(WofTrainingDummyCombatRules.MaxHealth, Is.EqualTo(120f));
            Assert.That(WofTrainingDummyCombatRules.RespawnSeconds, Is.EqualTo(1.8d));
            Assert.That(WofSpellRuntimeTuning.LightningRadius, Is.EqualTo(12f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Fireball), Is.EqualTo(24f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.IceShard), Is.EqualTo(34f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.ArcaneBeam), Is.EqualTo(34f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.RingsOfPower), Is.EqualTo(22f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Lightning), Is.EqualTo(32f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Flamethrower), Is.EqualTo(6f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Kunai), Is.EqualTo(18f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Tornado), Is.EqualTo(12f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.MeteorShower), Is.EqualTo(30f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Sleep), Is.EqualTo(5f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Poison), Is.EqualTo(7f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.Acid), Is.EqualTo(9f));
            Assert.That(WofTrainingDummyCombatRules.GetDamage(WofSpellId.IceSpell), Is.Zero);

            var health = WofTrainingDummyCombatRules.MaxHealth;
            var sequence = 0;
            WofTrainingDummyDamageResult result = default;
            for (var index = 0; index < 5; index++)
            {
                result = WofTrainingDummyCombatRules.Apply(health, sequence, WofSpellId.Fireball, 10d + index);
                Assert.That(result.Applied, Is.True);
                health = result.Health;
                sequence = result.HitSequence;
            }

            Assert.That(result.Health, Is.Zero);
            Assert.That(result.RespawnAt, Is.EqualTo(15.8d).Within(0.000001d));
            Assert.That(result.HitSequence, Is.EqualTo(5));
            Assert.That(WofTrainingDummyCombatRules.IsRespawnDue(result.Health, result.RespawnAt, 15.799d), Is.False);
            Assert.That(WofTrainingDummyCombatRules.IsRespawnDue(result.Health, result.RespawnAt, 15.8d), Is.True);

            var ignoredWhileDown = WofTrainingDummyCombatRules.Apply(
                result.Health,
                result.HitSequence,
                WofSpellId.Fireball,
                15d);
            Assert.That(ignoredWhileDown.Applied, Is.False);
        }

        [Test]
        public void TrainingDummy_CombatStateRoundTripsThroughNetworkRecord()
        {
            var source = new WofEnginePlaceableRecord
            {
                instanceId = "dummy-7",
                placeableId = "training-spell-dummy",
                label = "Spell Dummy",
                x = 4f,
                y = 8f,
                z = -12f,
                yaw = 0.75f,
                trainingDummyHealth = 62f,
                trainingDummyRespawnAt = 45.25d,
                trainingDummyHitSequence = 9,
                trainingDummyLastSpell = (int)WofSpellId.Poison
            };

            var network = new WofNetworkEnginePlaceableRecord(source);
            var roundTrip = network.ToRuntimeRecord();

            Assert.That(roundTrip.trainingDummyHealth, Is.EqualTo(62f));
            Assert.That(roundTrip.trainingDummyRespawnAt, Is.EqualTo(45.25d));
            Assert.That(roundTrip.trainingDummyHitSequence, Is.EqualTo(9));
            Assert.That(roundTrip.trainingDummyLastSpell, Is.EqualTo((int)WofSpellId.Poison));
        }

        [Test]
        public void RandomYaw_IsStableAndFixedYawIgnoresPlayer()
        {
            var campfire = WofEnginePlaceableCatalog.Find("campfire-small");
            var portal = WofEnginePlaceableCatalog.Find("magic-portal-marker");
            var first = WofEnginePlacementMath.GetPlacementYaw(campfire, 2.4f, 10f, -20f);
            var second = WofEnginePlacementMath.GetPlacementYaw(campfire, -1f, 10f, -20f);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.InRange(-Mathf.PI, Mathf.PI));
            Assert.That(WofEnginePlacementMath.GetPlacementYaw(portal, 2.4f, 10f, -20f), Is.Zero);
        }

        [Test]
        public void Collision_UsesReactCircleSpacingAndIgnoreId()
        {
            var bush = WofEnginePlaceableCatalog.Find("bush-round");
            var records = new[]
            {
                new WofEnginePlaceableRecord
                {
                    instanceId = "bush-1", placeableId = bush.Id, label = bush.Name, x = 0f, z = 0f
                }
            };

            Assert.That(WofEnginePlacementMath.FindCollision(bush, 4f, 0f, 0f, records).HasValue, Is.True);
            Assert.That(WofEnginePlacementMath.FindCollision(bush, 6f, 0f, 0f, records).HasValue, Is.False);
            Assert.That(WofEnginePlacementMath.FindCollision(bush, 0f, 0f, 0f, records, "bush-1").HasValue, Is.False);
        }

        [Test]
        public void Collision_IgnoresReactTrainingDummySceneObjects()
        {
            var bush = WofEnginePlaceableCatalog.Find("bush-round");
            var records = new[]
            {
                new WofEnginePlaceableRecord
                {
                    instanceId = "dummy", placeableId = "training-spell-dummy", label = "Spell Dummy", x = 0f, z = 0f
                }
            };

            Assert.That(WofEnginePlacementMath.FindCollision(bush, 0f, 0f, 0f, records).HasValue, Is.False);
        }

        [Test]
        public void Storage_SanitizesIdsLabelsObjectsAndSixNewestSlots()
        {
            var document = new WofEnginePlaceableStorageDocument();
            var definition = WofEnginePlaceableCatalog.Find("hut-log-cabin");
            for (var index = 0; index < 8; index++)
            {
                WofEnginePlaceableStorage.SaveSlot(document, $" Slot {index + 1} !! ", string.Empty,
                    new[]
                    {
                        new WofEnginePlaceableRecord
                        {
                            placeableId = definition.Id, x = index, y = 1f, z = 2f, yaw = 0f
                        }
                    }, index + 1);
            }

            var sanitized = WofEnginePlaceableStorage.Sanitize(document);

            Assert.That(sanitized.slots, Has.Count.EqualTo(6));
            Assert.That(sanitized.slots[0].savedAt, Is.EqualTo(8));
            Assert.That(sanitized.slots[0].objects[0].label, Is.EqualTo(definition.Name));
            Assert.That(WofEnginePlaceableStorage.SanitizeSlotId(" Slot 1 !! "), Is.EqualTo("slot-1-"));
            Assert.That(WofEnginePlaceableStorage.GetSlotLabel("slot-3"), Is.EqualTo("Slot 3"));
        }

        [Test]
        public void Storage_JsonRoundTripPreservesCurrentObjectsAndNamedSlotsForWebGlPersistence()
        {
            var current = new[]
            {
                new WofEnginePlaceableRecord
                {
                    instanceId = "engine-hut-log-cabin-current",
                    placeableId = "hut-log-cabin",
                    label = "Current Cabin",
                    x = 12f,
                    y = 3f,
                    z = -8f,
                    yaw = 1.25f
                }
            };
            var document = new WofEnginePlaceableStorageDocument
            {
                current = WofEnginePlaceableStorage.SanitizeObjects(current)
            };
            WofEnginePlaceableStorage.SaveSlot(document, "slot-2", "Village Build", current, 123456L);

            var restored = WofEnginePlaceableStorage.Deserialize(WofEnginePlaceableStorage.Serialize(document));

            Assert.That(restored.current, Has.Count.EqualTo(1));
            Assert.That(restored.current[0].instanceId, Is.EqualTo("engine-hut-log-cabin-current"));
            var slot = WofEnginePlaceableStorage.FindSlot(restored, "slot-2");
            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.label, Is.EqualTo("Village Build"));
            Assert.That(slot.savedAt, Is.EqualTo(123456L));
            Assert.That(slot.objects, Has.Count.EqualTo(1));
            Assert.That(slot.objects[0].placeableId, Is.EqualTo("hut-log-cabin"));
        }

        [Test]
        public void Storage_EmptyJsonRestoresAnEmptyDocument()
        {
            var restored = WofEnginePlaceableStorage.Deserialize(string.Empty);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.current, Is.Empty);
            Assert.That(restored.slots, Is.Empty);
        }
    }
}
