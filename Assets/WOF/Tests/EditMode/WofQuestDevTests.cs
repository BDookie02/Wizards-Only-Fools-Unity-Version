using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofQuestDevTests
    {
        [Test]
        public void DefaultDarrelProgram_PreservesReactSixPointContract()
        {
            var nextId = 0;
            var target = Target("darrel-special-8:-8", "Darrel");
            var program = WofQuestDevRules.CreateDefaultProgram(target, () => $"point-{++nextId}", 1234);

            Assert.That(program.displayName, Is.EqualTo("Darrel"));
            Assert.That(program.role, Is.EqualTo(WofQuestNpcRole.QuestGiver));
            Assert.That(program.greeting, Is.EqualTo("Who are you what do you want!"));
            Assert.That(program.scriptPoints, Has.Length.EqualTo(6));
            Assert.That(program.scriptPoints[0].title, Is.EqualTo("Opening"));
            Assert.That(program.scriptPoints[2].eventScript, Does.Contain("startQuest spellquest:healingcrystals"));
            Assert.That(program.scriptPoints[4].eventScript, Does.Contain("teleportDarrelQuest darrel-grove"));
            Assert.That(program.scriptPoints[5].dialog, Does.Contain("Peacefully ask for crystals"));
            Assert.That(program.updatedAt, Is.EqualTo(1234));
        }

        [Test]
        public void DefaultGenericProgram_PreservesReactGreetingAndEvent()
        {
            var program = WofQuestDevRules.CreateDefaultProgram(Target("npc-1", "Town Villager 1"), () => "point-a", 10);

            Assert.That(program.role, Is.EqualTo(WofQuestNpcRole.Villager));
            Assert.That(program.greeting, Is.EqualTo("The villager watches you carefully, waiting for the next line of the quest."));
            Assert.That(program.scriptPoints, Has.Length.EqualTo(1));
            Assert.That(program.scriptPoints[0].title, Is.EqualTo("Greeting"));
            Assert.That(program.scriptPoints[0].dialog, Is.EqualTo("Need something, wizard?"));
            Assert.That(program.scriptPoints[0].eventScript, Is.EqualTo("message Quest scriptpoint reached"));
        }

        [Test]
        public void PointEditing_PreservesReactInsertMoveCopyDeleteAndEventRules()
        {
            var first = new WofQuestScriptPoint { id = "one", title = "One", eventScript = "message one" };
            var second = WofQuestDevRules.DuplicatePoint(first, () => "two");
            var points = WofQuestDevRules.InsertPointAfter(new[] { first }, first.id, second);

            Assert.That(points, Has.Length.EqualTo(2));
            Assert.That(points[1].id, Is.EqualTo("two"));
            Assert.That(points[1].title, Is.EqualTo("One Copy"));
            Assert.That(WofQuestDevRules.MovePoint(points, "two", -1), Is.True);
            Assert.That(points[0].id, Is.EqualTo("two"));
            var removal = WofQuestDevRules.RemovePoint(points, "two", 0);
            Assert.That(removal.HasValue, Is.True);
            Assert.That(removal.Value.Points, Has.Length.EqualTo(1));
            Assert.That(WofQuestDevRules.RemovePoint(removal.Value.Points, "one", 0), Is.Null);
            Assert.That(WofQuestDevRules.AppendEventLine(" message one  ", "  setFlag qa=true "),
                Is.EqualTo(" message one\nsetFlag qa=true"));
            Assert.That(WofQuestDevRules.CountEvents(new WofQuestScriptPoint { eventScript = "one\n\n two\r\n  \rthree" }), Is.EqualTo(3));
            Assert.That(WofQuestDevRules.BuildEventLine(WofQuestEventBuilderKind.Message, "", "", ""),
                Is.EqualTo("message Good work, wizard."));
            Assert.That(WofQuestDevRules.BuildEventLine(WofQuestEventBuilderKind.SetFlag, "", "", ""),
                Is.EqualTo("setFlag town_01_quests=1"));
        }

        [Test]
        public void SanitizeProgram_EnforcesReactLimitsAndFallbacks()
        {
            var points = new WofQuestScriptPoint[30];
            for (var index = 0; index < points.Length; index++)
            {
                points[index] = new WofQuestScriptPoint
                {
                    id = string.Empty,
                    title = new string('T', 60),
                    dialog = new string('D', 950),
                    eventScript = new string('E', 950)
                };
            }
            var program = new WofQuestNpcProgram
            {
                npcId = "npc",
                displayName = " ",
                role = (WofQuestNpcRole)99,
                scriptPoints = points
            };
            var counter = 0;
            var clean = WofQuestDevRules.SanitizeProgram(program, Target("npc", "Fallback"), () => $"id-{++counter}", 88);

            Assert.That(clean.displayName, Is.EqualTo("Fallback"));
            Assert.That(clean.townId, Is.EqualTo("base-village"));
            Assert.That(clean.role, Is.EqualTo(WofQuestNpcRole.Villager));
            Assert.That(clean.hasPosition, Is.True);
            Assert.That(clean.scriptPoints, Has.Length.EqualTo(24));
            Assert.That(clean.scriptPoints[0].title, Has.Length.EqualTo(48));
            Assert.That(clean.scriptPoints[0].dialog, Has.Length.EqualTo(900));
            Assert.That(clean.scriptPoints[0].eventScript, Has.Length.EqualTo(900));
            Assert.That(clean.updatedAt, Is.EqualTo(88));
        }

        [Test]
        public void ProgramStore_RoundTripsAndRetainsPreviousGenerationBackup()
        {
            var root = CreateTempRoot();
            try
            {
                var path = Path.Combine(root, "programs.json");
                var first = WofQuestDevRules.CreateDefaultProgram(Target("npc-one", "One"), () => "one", 1);
                var second = WofQuestDevRules.CreateDefaultProgram(Target("npc-two", "Two"), () => "two", 2);
                WofQuestDevStore.SaveToPath(path, new WofQuestNpcProgramCollection
                {
                    claimedDarrelNpcId = "npc-one",
                    programs = new[] { first }
                });
                WofQuestDevStore.SaveToPath(path, new WofQuestNpcProgramCollection
                {
                    claimedDarrelNpcId = "npc-two",
                    programs = new[] { second }
                });

                var current = WofQuestDevStore.LoadFromPath(path);
                var backup = WofQuestDevStore.LoadFromPath(path + ".bak");
                Assert.That(current.programs[0].npcId, Is.EqualTo("npc-two"));
                Assert.That(current.claimedDarrelNpcId, Is.EqualTo("npc-two"));
                Assert.That(backup.programs[0].npcId, Is.EqualTo("npc-one"));
                Assert.That(backup.claimedDarrelNpcId, Is.EqualTo("npc-one"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void DarrelSpawnStore_RoundTripsOverrideAndRejectsInvalidData()
        {
            var root = CreateTempRoot();
            try
            {
                var path = Path.Combine(root, "spawn.json");
                var expected = new WofDarrelQuestSpawn(new Vector3(12.5f, 44f, -8.25f), 123f, true);
                WofDarrelQuestSpawnStore.SaveToPath(path, expected);
                var actual = WofDarrelQuestSpawnStore.LoadFromPath(path);
                Assert.That(actual.IsOverride, Is.True);
                Assert.That(actual.Position, Is.EqualTo(expected.Position));
                Assert.That(actual.YawDegrees, Is.EqualTo(123f));
                File.WriteAllText(path, "{\"version\":1,\"hasOverride\":true,\"position\":{\"x\":\"bad\"}}");
                var fallback = WofDarrelQuestSpawnStore.LoadFromPath(path);
                Assert.That(fallback.IsOverride, Is.False);
                Assert.That(fallback.Position, Is.EqualTo(WofDarrelGroveLayout.SpawnPosition));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void DarrelTeleportRpc_CarriesNoClientSelectedPosition()
        {
            var request = typeof(WofPlayerController).GetMethod(
                "RequestDarrelGroveTeleportRpc",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(request, Is.Not.Null);
            Assert.That(request.GetParameters(), Is.Empty,
                "The server must resolve the durable Darrel spawn; clients cannot supply teleport coordinates.");
        }

        [Test]
        public void QuestEventParser_PreservesReactCommentsSeparatorsAndNormalization()
        {
            var events = WofQuestScriptRuntime.ParseEvents("# note\n// skip\nset-Flag: qa=true\nmessage hello world\ncomplete_quest=test");

            Assert.That(events, Has.Count.EqualTo(3));
            Assert.That(events[0].Command, Is.EqualTo("setflag"));
            Assert.That(events[0].Value, Is.EqualTo("qa=true"));
            Assert.That(events[1].Command, Is.EqualTo("message"));
            Assert.That(events[1].Value, Is.EqualTo("hello world"));
            Assert.That(events[2].Command, Is.EqualTo("completequest"));
        }

        [Test]
        public void QuestScript_ExecutesUnlockFlagsQuestMessagesAndUnknownEvents()
        {
            var profile = NewProfile();
            var program = Program("npc-1", "Quest NPC", "unlockSpell Fireball\nstartQuest alpha\ncompleteQuest beta\nsetFlag qa=value\nsay hello\nwat nope");
            var result = WofQuestScriptRuntime.Execute(profile, program, program.scriptPoints[0], 0, 100);

            Assert.That(result.ProfileChanged, Is.True);
            Assert.That(profile.questUnlockedSpells, Does.Contain("fireball"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:alpha"), Is.EqualTo("started"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:beta"), Is.EqualTo("completed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "qa"), Is.EqualTo("value"));
            Assert.That(result.Messages, Does.Contain("Unlocked fireball"));
            Assert.That(result.Messages, Does.Contain("hello"));
            Assert.That(result.Messages, Does.Contain("Unknown event: wat"));
        }

        [Test]
        public void QuestScript_RandomUnlockAndAssignmentCompletionAreDeterministic()
        {
            var profile = NewProfile();
            profile.spellQuestAssignments = new[]
            {
                new WofSpellQuestAssignment
                {
                    npcId = "npc-1",
                    townId = "town",
                    displayName = "NPC",
                    questId = "spellquest:fireball",
                    spell = "fireball",
                    status = WofQuestDialogRules.QuestStatusAccepted
                }
            };
            var program = Program("npc-1", "NPC", "unlockRandomLockedSpell\nsetSpellQuestReady npc-1\ncompleteAssignedSpellQuest npc-1");
            var result = WofQuestScriptRuntime.Execute(profile, program, program.scriptPoints[0], 0, 200);

            Assert.That(profile.questUnlockedSpells, Does.Contain("fireball"));
            Assert.That(profile.spellQuestAssignments[0].status, Is.EqualTo(WofQuestDialogRules.QuestStatusCompleted));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "spellquest:fireball:ready"), Is.EqualTo("true"));
            Assert.That(result.Messages[0], Is.EqualTo("Gamble unlocked fireball"));
            Assert.That(result.Messages, Does.Contain("Fireball quest ready"));
        }

        [TestCase("", WofQuestScriptTeleport.LilyCoil, "Transported to Lily Coil")]
        [TestCase("lilycoil", WofQuestScriptTeleport.LilyCoil, "Transported to Lily Coil")]
        [TestCase("coil", WofQuestScriptTeleport.LilyCoil, "Transported to Lily Coil")]
        [TestCase("springcoil", WofQuestScriptTeleport.LilyCoil, "Transported to Lily Coil")]
        [TestCase("purplecoil", WofQuestScriptTeleport.LilyCoil, "Transported to Lily Coil")]
        [TestCase("purple-coil", WofQuestScriptTeleport.LilyCoil, "Transported to Lily Coil")]
        [TestCase("darrel-grove", WofQuestScriptTeleport.DarrelGrove, "Transported to Darrel's Grove")]
        public void QuestScript_ResolvesReactQuestRealmAliases(string value, WofQuestScriptTeleport expected, string message)
        {
            var profile = NewProfile();
            var program = Program("darrel-custom", "Darrel", $"teleportQuestRealm {value}");
            var result = WofQuestScriptRuntime.Execute(profile, program, program.scriptPoints[0], 0, 300);

            Assert.That(result.Teleport, Is.EqualTo(expected));
            Assert.That(result.Messages, Does.Contain(message));
            if (expected == WofQuestScriptTeleport.DarrelGrove)
            {
                Assert.That(WofSpellQuestRules.FindAssignment(profile, "darrel-custom")?.spell,
                    Is.EqualTo(WofSpellQuestRules.DarrelRewardSpell));
            }
        }

        [TestCase("/questdev", false, WofCommandConsoleAction.SetQuestDevEnabled, true)]
        [TestCase("/npcdev off", true, WofCommandConsoleAction.SetQuestDevEnabled, false)]
        [TestCase("/devquests enabled", false, WofCommandConsoleAction.SetQuestDevEnabled, true)]
        public void CommandRules_PreserveQuestDevAliasesAndToggle(
            string command,
            bool current,
            WofCommandConsoleAction expectedAction,
            bool expectedEnabled)
        {
            var result = WofCommandConsoleRules.Evaluate(command, false, current);
            Assert.That(result.Action, Is.EqualTo(expectedAction));
            Assert.That(result.Enabled, Is.EqualTo(expectedEnabled));
            Assert.That(result.Message, Is.EqualTo($"QUEST DEV {(expectedEnabled ? "ENABLED" : "DISABLED")}"));
        }

        [Test]
        public void CommandRules_PreserveQuestEditorAndDarrelAliases()
        {
            var editor = WofCommandConsoleRules.Evaluate("/questdev open Weird NPC!!");
            Assert.That(editor.Action, Is.EqualTo(WofCommandConsoleAction.OpenQuestNpcEditor));
            Assert.That(editor.Value, Is.EqualTo("Weird-NPC"));
            Assert.That(WofCommandConsoleRules.Evaluate("/darrelhere").Action, Is.EqualTo(WofCommandConsoleAction.ClaimDarrelHere));
            Assert.That(WofCommandConsoleRules.Evaluate("/claimdarrel").Message,
                Is.EqualTo("Claiming Darrel at your current hut or target."));
            Assert.That(WofCommandConsoleRules.Evaluate("/darrelquestspawnhere").Action,
                Is.EqualTo(WofCommandConsoleAction.SetDarrelQuestSpawn));
            Assert.That(WofCommandConsoleRules.Evaluate("/cleardarrelspawn").Action,
                Is.EqualTo(WofCommandConsoleAction.ResetDarrelQuestSpawn));
            Assert.That(WofCommandConsoleRules.Evaluate("/questdev sideways").Message,
                Is.EqualTo("Usage: /questdev on or /questdev off"));
        }

        private static WofQuestNpcEditorTarget Target(string npcId, string name)
        {
            return new WofQuestNpcEditorTarget(npcId, "base-village", npcId, name, "village", new Vector3(1f, 2f, 3f));
        }

        private static WofQuestNpcProgram Program(string npcId, string name, string events)
        {
            return new WofQuestNpcProgram
            {
                npcId = npcId,
                townId = "town",
                hutId = npcId,
                displayName = name,
                scriptPoints = new[]
                {
                    new WofQuestScriptPoint { id = "point", title = "Test", eventScript = events }
                }
            };
        }

        private static WofSurvivalProfile NewProfile()
        {
            var profile = new WofSurvivalProfile();
            WofSpellQuestRules.NormalizeProfile(profile);
            WofInventoryRules.NormalizeProfile(profile);
            return profile;
        }

        private static string CreateTempRoot()
        {
            var root = Path.Combine("D:\\tmp\\wof-unity\\tests", $"quest-dev-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
