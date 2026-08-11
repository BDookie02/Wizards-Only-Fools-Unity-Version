using System.Linq;
using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofEngineSystemCatalogTests
    {
        private static readonly string[] ReactOrderedIds =
        {
            "terrain",
            "survival-world",
            "water",
            "rendering-canvas",
            "minimap-rendering",
            "avatar-rendering",
            "quests-navigation",
            "vegetation",
            "villages",
            "placeables",
            "spells",
            "hud",
            "app-frame",
            "launch-menu",
            "input",
            "player-controller",
            "multiplayer",
            "qa-tools"
        };

        [Test]
        public void BakedCatalogPreservesEveryReactSystemInExactOrder()
        {
            var document = WofEngineSystemCatalog.Load();

            Assert.That(document.version, Is.EqualTo(1));
            Assert.That(document.sourceModule, Is.EqualTo("src/game/systems/systemCatalog.ts"));
            Assert.That(document.sourceSha256, Has.Length.EqualTo(64));
            Assert.That(document.systems, Has.Length.EqualTo(WofEngineSystemCatalog.ReactSystemCount));
            Assert.That(document.systems.Select(system => system.id), Is.EqualTo(ReactOrderedIds));
        }

        [Test]
        public void EverySystemCardHasTheExactReactFieldsNeededByThePanel()
        {
            var systems = WofEngineSystemCatalog.Load().systems;

            Assert.That(systems.All(system => !string.IsNullOrWhiteSpace(system.name)), Is.True);
            Assert.That(systems.All(system => !string.IsNullOrWhiteSpace(system.category)), Is.True);
            Assert.That(systems.All(system => !string.IsNullOrWhiteSpace(system.owner)), Is.True);
            Assert.That(systems.All(system => !string.IsNullOrWhiteSpace(system.responsibility)), Is.True);
            Assert.That(systems.All(system => system.currentEntrypoints is { Length: > 0 }), Is.True);
            Assert.That(systems.All(system => !string.IsNullOrWhiteSpace(system.extractionTarget)), Is.True);
            Assert.That(systems[0].name, Is.EqualTo("Terrain"));
            Assert.That(systems[0].extractionTarget, Is.EqualTo("src/game/systems/world/terrain"));
            Assert.That(systems[^1].name, Is.EqualTo("QA Tools"));
            Assert.That(systems[^1].extractionTarget, Is.EqualTo("src/game/tools"));
        }

        [Test]
        public void EmptyCatalogPayloadFailsClosedWithoutInventingSystemCards()
        {
            var document = WofEngineSystemCatalog.Parse(string.Empty);

            Assert.That(document.systems, Is.Empty);
        }
    }
}
