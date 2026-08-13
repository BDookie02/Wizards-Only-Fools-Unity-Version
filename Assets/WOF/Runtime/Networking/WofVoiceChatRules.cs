using System;
using System.Text;
using UnityEngine;

namespace WOF
{
    public static class WofVoiceChatRules
    {
        public const float DefaultRefreshSeconds = 0.12f;
        public const float PositionRefreshSeconds = 0.3f;
        public const int SharedChannelAudibleDistance = 64;
        private const string ChannelPrefix = "wof-voice-";
        private const string DisplayNamePrefix = "wof_";

        public static string CreateChannelName(string sessionCode)
        {
            if (string.IsNullOrWhiteSpace(sessionCode)) return string.Empty;
            var normalized = new StringBuilder(48);
            foreach (var character in sessionCode.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                {
                    normalized.Append(character);
                    if (normalized.Length >= 48) break;
                }
            }
            return normalized.Length == 0 ? string.Empty : ChannelPrefix + normalized;
        }

        public static string CreateParticipantDisplayName(ulong ownerClientId, string playerName)
        {
            var normalizedName = new StringBuilder(32);
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                foreach (var character in playerName.Trim())
                {
                    if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                    {
                        normalizedName.Append(character);
                        if (normalizedName.Length >= 32) break;
                    }
                }
            }
            if (normalizedName.Length == 0) normalizedName.Append("WIZARD");
            return $"{DisplayNamePrefix}{ownerClientId}_{normalizedName}";
        }

        public static bool TryParseOwnerClientId(string displayName, out ulong ownerClientId)
        {
            ownerClientId = 0;
            if (string.IsNullOrWhiteSpace(displayName) ||
                !displayName.StartsWith(DisplayNamePrefix, StringComparison.Ordinal))
            {
                return false;
            }
            var separator = displayName.IndexOf('_', DisplayNamePrefix.Length);
            return separator > DisplayNamePrefix.Length &&
                   ulong.TryParse(displayName.Substring(DisplayNamePrefix.Length,
                       separator - DisplayNamePrefix.Length), out ownerClientId);
        }

        public static float CalculateProximityVolume(float distance, float range, float outputVolume)
        {
            if (!IsFinite(distance) || !IsFinite(range) || !IsFinite(outputVolume) || range <= 0f)
                return 0f;
            if (distance >= range) return 0f;
            var remaining = 1f - Mathf.Clamp01(Mathf.Max(0f, distance) / range);
            return Mathf.Clamp01(outputVolume) * remaining * remaining;
        }

        public static bool ShouldTransmit(
            bool voiceEnabled,
            string inputMode,
            bool keyboardPushToTalkHeld,
            bool controllerPushToTalkHeld,
            bool gameplaySuppressed)
        {
            if (!voiceEnabled) return false;
            if (!string.Equals(inputMode, "pushToTalk", StringComparison.Ordinal)) return true;
            return !gameplaySuppressed && (keyboardPushToTalkHeld || controllerPushToTalkHeld);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
