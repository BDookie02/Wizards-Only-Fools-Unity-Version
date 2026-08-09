using System;
using UnityEngine;

namespace WOF
{
    public enum WofDarrelQuestProgress
    {
        Unstarted = 0,
        Accepted = 1,
        Completed = 2
    }

    [Serializable]
    public sealed class WofQuestDialogChoice
    {
        public WofQuestDialogChoice(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    [Serializable]
    public sealed class WofQuestDialogSession
    {
        public WofQuestDialogSession(
            string npcId,
            string townId,
            string displayName,
            string line,
            params WofQuestDialogChoice[] choices)
        {
            NpcId = npcId;
            TownId = townId;
            DisplayName = displayName;
            Line = line;
            Choices = choices ?? Array.Empty<WofQuestDialogChoice>();
        }

        public string NpcId { get; }
        public string TownId { get; }
        public string DisplayName { get; }
        public string Line { get; }
        public WofQuestDialogChoice[] Choices { get; }
    }

    public readonly struct WofQuestDialogTransition
    {
        public WofQuestDialogTransition(WofQuestDialogSession session, bool acceptedQuest, bool closed)
        {
            Session = session;
            AcceptedQuest = acceptedQuest;
            Closed = closed;
        }

        public WofQuestDialogSession Session { get; }
        public bool AcceptedQuest { get; }
        public bool Closed { get; }
    }

    public static class WofQuestDialogRules
    {
        public const string DarrelNpcId = "-64--48";
        public const string DarrelTownId = "base-village";
        public const string QuestStatusUnstarted = "unstarted";
        public const string QuestStatusAccepted = "assigned";
        public const string QuestStatusCompleted = "completed";

        public const string OpeningLine = "Darrel: Who are you what do you want!";
        public const string AcceptedReminderLine = "Darrel: The job is still open. Go to the fields, gather 1 leaves, 1 berries, and 1 roots. Brew the garden draught at a brewing station, drink it, and try not to act shocked when the spirit dragon offers lunch instead of a fight.";
        public const string CompletedLine = "Darrel: You got the Healing Crystals. Try not to make the spell look bad.";
        public const string JerkResponseLine = "Player: None of your business.\n\nDarrel: Then my business is none of yours. Try again when you remember how doors work.";
        public const string JobOfferLine = "Player: What kind of wizard has only 2 spells?\n\nDarrel: A pitiful one. Fine. I have a job if you want a spell. Travel to the sacred garden in an alternate dimension, face the spirit dragon, and bring back healing crystals.\n\nDarrel: First you need a garden draught. Go to the fields, gather 1 leaves, 1 berries, and 1 roots. Brew it at any brewing station you own, buy, craft, or find in a village. Drink it, and it will send you where you need to go.";
        public const string AcceptedLine = "Darrel: Good. Leaves, berries, roots. One of each, all from the fields. Brew the garden draught at a brewing station, drink it, and it will take you to the sacred garden.\n\nDarrel: The spirit dragon is supposed to fight you. If it offers lemonade, that is probably normal. Bring back the Healing Crystals.";

        public static WofDarrelQuestProgress ResolveProgress(string status)
        {
            if (string.Equals(status, QuestStatusCompleted, StringComparison.OrdinalIgnoreCase))
            {
                return WofDarrelQuestProgress.Completed;
            }
            if (string.Equals(status, QuestStatusAccepted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "started", StringComparison.OrdinalIgnoreCase))
            {
                return WofDarrelQuestProgress.Accepted;
            }
            return WofDarrelQuestProgress.Unstarted;
        }

        public static WofQuestDialogSession CreateInitial(WofDarrelQuestProgress progress)
        {
            if (progress == WofDarrelQuestProgress.Completed)
            {
                return Create(CompletedLine, new WofQuestDialogChoice("darrel-close", "Leave Darrel alone"));
            }
            if (progress == WofDarrelQuestProgress.Accepted)
            {
                return Create(AcceptedReminderLine,
                    new WofQuestDialogChoice("darrel-close", "I will get the ingredients"));
            }
            return Create(
                OpeningLine,
                new WofQuestDialogChoice("darrel-jerk", "None of your business."),
                new WofQuestDialogChoice("darrel-two-spells", "What kind of wizard has only 2 spells?"));
        }

        public static bool TryChoose(
            WofQuestDialogSession current,
            string choiceId,
            out WofQuestDialogTransition transition)
        {
            transition = default;
            if (current == null || string.IsNullOrWhiteSpace(choiceId))
            {
                return false;
            }

            switch (choiceId)
            {
                case "darrel-close":
                    transition = new WofQuestDialogTransition(null, false, true);
                    return true;
                case "darrel-jerk":
                    transition = new WofQuestDialogTransition(
                        Create(
                            JerkResponseLine,
                            new WofQuestDialogChoice("darrel-two-spells", "What kind of wizard has only 2 spells?"),
                            new WofQuestDialogChoice("darrel-close", "Leave")),
                        false,
                        false);
                    return true;
                case "darrel-two-spells":
                    transition = new WofQuestDialogTransition(
                        Create(
                            JobOfferLine,
                            new WofQuestDialogChoice("darrel-accept-job", "Take the job"),
                            new WofQuestDialogChoice("darrel-close", "Not right now")),
                        false,
                        false);
                    return true;
                case "darrel-accept-job":
                    transition = new WofQuestDialogTransition(
                        Create(AcceptedLine, new WofQuestDialogChoice("darrel-close", "Head to the fields")),
                        true,
                        false);
                    return true;
                default:
                    return false;
            }
        }

        private static WofQuestDialogSession Create(string line, params WofQuestDialogChoice[] choices)
        {
            return new WofQuestDialogSession(DarrelNpcId, DarrelTownId, "Darrel", line, choices);
        }
    }

    public static class WofQuestTargetMath
    {
        public const float InteractionRange = 9.5f;
        public const float CloseRange = 3.75f;
        public const float AimRadius = 1.75f;
        public const float TargetCenterHeight = 1.7f;

        public static bool TryScoreTarget(
            Vector3 rayOrigin,
            Vector3 rayDirection,
            Vector3 targetCenter,
            out float score)
        {
            score = float.PositiveInfinity;
            if (!IsFinite(rayOrigin) || !IsFinite(rayDirection) || !IsFinite(targetCenter) ||
                rayDirection.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            rayDirection.Normalize();
            var offset = targetCenter - rayOrigin;
            var distance = offset.magnitude;
            if (distance > InteractionRange)
            {
                return false;
            }

            var forwardDistance = Vector3.Dot(rayDirection, offset);
            if (forwardDistance <= 0f)
            {
                return false;
            }

            var lateralSquared = Mathf.Max(0f, distance * distance - forwardDistance * forwardDistance);
            var lateralDistance = Mathf.Sqrt(lateralSquared);
            var closeEnough = distance <= CloseRange;
            var aimedEnough = lateralDistance <= AimRadius + distance * 0.035f;
            if (!closeEnough && !aimedEnough)
            {
                return false;
            }

            score = lateralDistance * 2.3f + distance * 0.12f + (closeEnough ? -1.1f : 0f);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
