using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests.EditMode
{
    public sealed class WofPersistenceTests
    {
        private string _testRoot;

        [SetUp]
        public void SetUp()
        {
            _testRoot = Path.Combine("D:\\tmp\\wof-unity-tests", "persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_testRoot) &&
                _testRoot.StartsWith("D:\\tmp\\wof-unity-tests\\persistence-", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }

        [Test]
        public void VersionOneProfileMigratesAndPersistsWithoutLosingFields()
        {
            var path = Path.Combine(_testRoot, "survival-save-v1.json");
            File.WriteAllText(path,
                "{\"version\":1,\"playerName\":\"Migration QA\",\"survivalLevel\":7," +
                "\"survivalXp\":311,\"lastMode\":\"multiplayer-survival\"," +
                "\"questUnlockedSpells\":[\"blink\",\"fireball\"]}");

            var profile = WofSurvivalProfileStore.LoadFromPath(path);

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.version, Is.EqualTo(WofSurvivalProfileStore.CurrentVersion));
            Assert.That(profile.savedAtUnixMilliseconds, Is.GreaterThan(0L));
            Assert.That(profile.playerName, Is.EqualTo("Migration QA"));
            Assert.That(profile.survivalLevel, Is.EqualTo(7));
            Assert.That(profile.survivalXp, Is.EqualTo(311));
            Assert.That(profile.lastMode, Is.EqualTo("multiplayer-survival"));
            Assert.That(profile.questUnlockedSpells, Does.Contain("fireball"));
            Assert.That(File.Exists(WofSurvivalProfileStore.GetBackupPath(path)), Is.True);
            Assert.That(JsonUtility.FromJson<WofSurvivalProfile>(File.ReadAllText(path)).version,
                Is.EqualTo(WofSurvivalProfileStore.CurrentVersion));
        }

        [Test]
        public void CorruptPrimaryIsQuarantinedAndValidBackupIsRestored()
        {
            var path = Path.Combine(_testRoot, "survival-save-v1.json");
            File.WriteAllText(path, "{ definitely-not-valid-json");
            File.WriteAllText(WofSurvivalProfileStore.GetBackupPath(path),
                "{\"version\":1,\"playerName\":\"Recovered QA\",\"survivalLevel\":4}");

            var profile = WofSurvivalProfileStore.LoadFromPath(path);

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.playerName, Is.EqualTo("Recovered QA"));
            Assert.That(profile.version, Is.EqualTo(WofSurvivalProfileStore.CurrentVersion));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(Directory.GetFiles(_testRoot, "*.corrupt-*.json").Length, Is.EqualTo(1));
            Assert.That(JsonUtility.FromJson<WofSurvivalProfile>(File.ReadAllText(path)).playerName,
                Is.EqualTo("Recovered QA"));
        }

        [Test]
        public void MissingPrimaryIsRecreatedFromThePreviousValidGeneration()
        {
            var path = Path.Combine(_testRoot, "survival-save-v1.json");
            File.WriteAllText(WofSurvivalProfileStore.GetBackupPath(path),
                "{\"version\":2,\"playerName\":\"Backup Only QA\",\"survivalLevel\":8}");

            var profile = WofSurvivalProfileStore.LoadFromPath(path);

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.playerName, Is.EqualTo("Backup Only QA"));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(Directory.GetFiles(_testRoot, "*.corrupt-*.json"), Is.Empty);
        }

        [Test]
        public void AtomicSaveKeepsThePreviousValidGenerationAsBackup()
        {
            var path = Path.Combine(_testRoot, "survival-save-v1.json");
            var profile = new WofSurvivalProfile { playerName = "First QA", survivalLevel = 2 };
            Assert.That(WofSurvivalProfileStore.SaveToPath(path, profile, 1000L), Is.True);
            profile.playerName = "Second QA";
            profile.survivalLevel = 3;
            Assert.That(WofSurvivalProfileStore.SaveToPath(path, profile, 2000L), Is.True);

            var current = JsonUtility.FromJson<WofSurvivalProfile>(File.ReadAllText(path));
            var backup = JsonUtility.FromJson<WofSurvivalProfile>(
                File.ReadAllText(WofSurvivalProfileStore.GetBackupPath(path)));
            Assert.That(current.playerName, Is.EqualTo("Second QA"));
            Assert.That(current.savedAtUnixMilliseconds, Is.EqualTo(2000L));
            Assert.That(backup.playerName, Is.EqualTo("First QA"));
            Assert.That(backup.savedAtUnixMilliseconds, Is.EqualTo(1000L));
        }

        [Test]
        public void FutureOrVersionlessProfilesAreRejectedInsteadOfSilentlyMisread()
        {
            Assert.That(WofSurvivalProfileStore.TryDeserialize(
                "{\"version\":99,\"playerName\":\"Future QA\"}", out _, out _), Is.False);
            Assert.That(WofSurvivalProfileStore.TryDeserialize(
                "{\"playerName\":\"Versionless QA\"}", out _, out _), Is.False);
        }

        [Test]
        public void AutosaveKeepsTheExactReactFifteenSecondCadence()
        {
            Assert.That(WofSurvivalAutosaveRuntime.IntervalSeconds, Is.EqualTo(15f));
            Assert.That(WofSurvivalAutosaveRuntime.IsEligibleSession(null), Is.False);
        }
    }
}
