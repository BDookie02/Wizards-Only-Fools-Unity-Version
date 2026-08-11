using System;
using UnityEngine;

namespace WOF
{
    public enum WofQuestNpcRole
    {
        Villager = 0,
        QuestGiver = 1,
        TownLeader = 2
    }

    [Serializable]
    public sealed class WofQuestScriptPoint
    {
        public string id = string.Empty;
        public string title = string.Empty;
        [TextArea] public string dialog = string.Empty;
        [TextArea] public string eventScript = string.Empty;
    }

    [Serializable]
    public sealed class WofQuestNpcProgram
    {
        public string npcId = string.Empty;
        public string townId = string.Empty;
        public string hutId = string.Empty;
        public string displayName = string.Empty;
        public WofQuestNpcRole role = WofQuestNpcRole.Villager;
        public string theme = "village";
        public bool hasPosition;
        public Vector3 position;
        [TextArea] public string greeting = string.Empty;
        public WofQuestScriptPoint[] scriptPoints = Array.Empty<WofQuestScriptPoint>();
        public long updatedAt;
    }

    public readonly struct WofQuestNpcEditorTarget
    {
        public WofQuestNpcEditorTarget(
            string npcId,
            string townId,
            string hutId,
            string defaultName,
            string theme,
            Vector3 position)
        {
            NpcId = npcId ?? string.Empty;
            TownId = townId ?? string.Empty;
            HutId = hutId ?? string.Empty;
            DefaultName = defaultName ?? string.Empty;
            Theme = theme ?? "village";
            Position = position;
        }

        public string NpcId { get; }
        public string TownId { get; }
        public string HutId { get; }
        public string DefaultName { get; }
        public string Theme { get; }
        public Vector3 Position { get; }
    }

    [Serializable]
    internal sealed class WofQuestNpcProgramCollection
    {
        public int version = WofQuestDevStore.CurrentVersion;
        public string claimedDarrelNpcId = string.Empty;
        public WofQuestNpcProgram[] programs = Array.Empty<WofQuestNpcProgram>();
    }

    [Serializable]
    internal sealed class WofDarrelQuestSpawnData
    {
        public int version = WofDarrelQuestSpawnStore.CurrentVersion;
        public bool hasOverride;
        public Vector3 position;
        public float yawDegrees;
    }

    public readonly struct WofDarrelQuestSpawn
    {
        public WofDarrelQuestSpawn(Vector3 position, float yawDegrees, bool isOverride)
        {
            Position = position;
            YawDegrees = yawDegrees;
            IsOverride = isOverride;
        }

        public Vector3 Position { get; }
        public float YawDegrees { get; }
        public bool IsOverride { get; }
    }
}
