using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WOF
{
    public readonly struct WofNavigationRecorderResult
    {
        public WofNavigationRecorderResult(bool ok, string message, string exportPath = "")
        {
            Ok = ok;
            Message = message ?? string.Empty;
            ExportPath = exportPath ?? string.Empty;
        }

        public bool Ok { get; }
        public string Message { get; }
        public string ExportPath { get; }
    }

    public readonly struct WofNavigationRecorderStatus
    {
        public WofNavigationRecorderStatus(
            bool active,
            string label,
            int sampleCount,
            long durationMilliseconds,
            int storedSessionCount)
        {
            Active = active;
            Label = label ?? string.Empty;
            SampleCount = sampleCount;
            DurationMilliseconds = durationMilliseconds;
            StoredSessionCount = storedSessionCount;
        }

        public bool Active { get; }
        public string Label { get; }
        public int SampleCount { get; }
        public long DurationMilliseconds { get; }
        public int StoredSessionCount { get; }
    }

    public static class WofNavigationRecorderRuntime
    {
        internal const int RecordingVersion = 1;
        internal const int SampleIntervalMilliseconds = 125;
        internal const int MaximumSamplesPerSession = 9000;
        internal const int MaximumStoredSessions = 8;
        internal const float SurvivalBlockSize = 512f;
        private const string DefaultLabel = "survival navigation";
        private const string StorageFileName = "navigation-sessions.json";

        private static WofNavigationRecordingSession s_ActiveRecording;

        public static bool IsActive => s_ActiveRecording != null;

        public static WofNavigationRecorderResult Start(string label = null)
        {
            if (s_ActiveRecording != null)
            {
                return new WofNavigationRecorderResult(
                    false,
                    $"Navigation recording already running: {s_ActiveRecording.label}");
            }

            var now = NowMilliseconds();
            s_ActiveRecording = new WofNavigationRecordingSession
            {
                id = MakeRecordingId(),
                label = SanitizeLabel(label),
                version = RecordingVersion,
                startedAt = now,
                endedAt = now,
                durationMs = 0,
                sampleIntervalMs = SampleIntervalMilliseconds,
                samples = new List<WofNavigationSample>(),
                lastSampleAt = 0
            };
            Debug.Log($"[WOF-AUTOMATION] NAV_RECORDING_STARTED label=\"{s_ActiveRecording.label}\"");
            return new WofNavigationRecorderResult(
                true,
                $"Navigation recording started: {s_ActiveRecording.label}");
        }

        public static void Record(
            string gameMode,
            Vector3 position,
            Vector3 rotation,
            Vector3 aimDirection,
            Vector3 velocity,
            Vector2 move,
            bool sprint,
            bool jump,
            bool slide,
            bool vclip,
            bool grounded,
            bool moving,
            bool sliding,
            bool crouching,
            bool spellMenuOpen,
            long? nowMilliseconds = null)
        {
            var recording = s_ActiveRecording;
            if (recording == null || recording.samples.Count >= MaximumSamplesPerSession)
            {
                return;
            }

            var now = nowMilliseconds ?? NowMilliseconds();
            if (now - recording.lastSampleAt < SampleIntervalMilliseconds)
            {
                return;
            }
            recording.lastSampleAt = now;

            var roundedPosition = RoundVector(position, 3);
            recording.samples.Add(new WofNavigationSample
            {
                gameMode = gameMode ?? string.Empty,
                pos = roundedPosition,
                rot = RoundVector(rotation, 4),
                aimDir = RoundVector(aimDirection, 4),
                velocity = RoundVector(velocity, 3),
                input = new WofNavigationInput
                {
                    forward = RoundForRecording(move.y, 3),
                    strafe = RoundForRecording(move.x, 3),
                    sprint = sprint,
                    jump = jump,
                    slide = slide,
                    vclip = vclip
                },
                state = new WofNavigationState
                {
                    grounded = grounded,
                    moving = moving,
                    sliding = sliding,
                    crouching = crouching,
                    sprinting = sprint,
                    spellMenuOpen = spellMenuOpen
                },
                t = Math.Max(0, now - recording.startedAt),
                chunk = new[]
                {
                    (int)Math.Floor(roundedPosition[0] / SurvivalBlockSize),
                    (int)Math.Floor(roundedPosition[2] / SurvivalBlockSize)
                }
            });
        }

        public static WofNavigationRecorderResult Stop()
        {
            if (s_ActiveRecording == null)
            {
                return new WofNavigationRecorderResult(false, "No navigation recording is running");
            }

            var session = FinalizeSession(s_ActiveRecording, NowMilliseconds());
            s_ActiveRecording = null;
            var sessions = LoadStoredSessions();
            sessions.Add(session);
            SaveStoredSessions(CapSessions(sessions));
            Debug.Log($"[WOF-AUTOMATION] NAV_RECORDING_STOPPED samples={session.samples.Count}");
            return new WofNavigationRecorderResult(
                true,
                $"Navigation recording stopped: {session.samples.Count} samples");
        }

        public static WofNavigationRecorderResult Export()
        {
            var sessions = LoadStoredSessions();
            if (s_ActiveRecording != null)
            {
                sessions.Add(FinalizeSession(s_ActiveRecording, NowMilliseconds()));
            }
            if (sessions.Count == 0)
            {
                return new WofNavigationRecorderResult(false, "No navigation recordings to export");
            }

            var latest = sessions[sessions.Count - 1];
            var safeLabel = Regex.Replace(latest.label.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            var fileName = $"wizards-nav-{(safeLabel.Length == 0 ? "recording" : safeLabel)}-{latest.id}.json";
            var exportPath = Path.Combine(GetStorageDirectory(), fileName);
            try
            {
                Directory.CreateDirectory(GetStorageDirectory());
                var payload = new WofNavigationExport
                {
                    exportedAt = NowMilliseconds(),
                    activeRecording = s_ActiveRecording != null,
                    sessions = sessions
                };
                File.WriteAllText(exportPath, JsonUtility.ToJson(payload, true));
                Debug.Log($"[WOF-AUTOMATION] NAV_RECORDING_EXPORTED path=\"{exportPath}\"");
                return new WofNavigationRecorderResult(
                    true,
                    $"Navigation recording exported: {latest.samples.Count} latest samples",
                    exportPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF-AUTOMATION] NAV_RECORDING_EXPORT_FAILED error=\"{exception.Message}\"");
                return new WofNavigationRecorderResult(false, "Navigation export failed");
            }
        }

        public static WofNavigationRecorderResult Clear()
        {
            s_ActiveRecording = null;
            try
            {
                var path = GetStoragePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF-AUTOMATION] NAV_RECORDING_CLEAR_WARNING error=\"{exception.Message}\"");
            }
            Debug.Log("[WOF-AUTOMATION] NAV_RECORDINGS_CLEARED");
            return new WofNavigationRecorderResult(true, "Navigation recordings cleared");
        }

        public static WofNavigationRecorderStatus GetStatus()
        {
            var now = NowMilliseconds();
            return new WofNavigationRecorderStatus(
                s_ActiveRecording != null,
                s_ActiveRecording?.label ?? string.Empty,
                s_ActiveRecording?.samples.Count ?? 0,
                s_ActiveRecording == null ? 0 : Math.Max(0, now - s_ActiveRecording.startedAt),
                LoadStoredSessions().Count);
        }

        internal static string SanitizeLabel(string value)
        {
            var label = string.IsNullOrEmpty(value) ? DefaultLabel : value;
            label = Regex.Replace(label, "[\\u0000-\\u001f\\u007f]", string.Empty);
            label = Regex.Replace(label, "\\s+", " ");
            label = Regex.Replace(label, "[^a-zA-Z0-9 _-]", string.Empty).Trim();
            if (label.Length > 48) label = label.Substring(0, 48);
            return label.Length == 0 ? DefaultLabel : label;
        }

        internal static string GetStorageDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "NavigationRecordings"));
        }

        private static string GetStoragePath()
        {
            return Path.Combine(GetStorageDirectory(), StorageFileName);
        }

        private static WofNavigationRecordingSession FinalizeSession(
            WofNavigationRecordingSession recording,
            long endedAt)
        {
            return new WofNavigationRecordingSession
            {
                id = recording.id,
                label = recording.label,
                version = RecordingVersion,
                startedAt = recording.startedAt,
                endedAt = endedAt,
                durationMs = Math.Max(0, endedAt - recording.startedAt),
                sampleIntervalMs = SampleIntervalMilliseconds,
                samples = new List<WofNavigationSample>(recording.samples)
            };
        }

        private static List<WofNavigationRecordingSession> LoadStoredSessions()
        {
            try
            {
                var path = GetStoragePath();
                if (!File.Exists(path)) return new List<WofNavigationRecordingSession>();
                var wrapper = JsonUtility.FromJson<WofNavigationSessionCollection>(File.ReadAllText(path));
                return wrapper?.sessions ?? new List<WofNavigationRecordingSession>();
            }
            catch
            {
                return new List<WofNavigationRecordingSession>();
            }
        }

        private static void SaveStoredSessions(List<WofNavigationRecordingSession> sessions)
        {
            try
            {
                Directory.CreateDirectory(GetStorageDirectory());
                File.WriteAllText(
                    GetStoragePath(),
                    JsonUtility.ToJson(new WofNavigationSessionCollection { sessions = sessions }, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF-AUTOMATION] NAV_RECORDING_STORAGE_WARNING error=\"{exception.Message}\"");
            }
        }

        private static List<WofNavigationRecordingSession> CapSessions(
            List<WofNavigationRecordingSession> sessions)
        {
            if (sessions.Count <= MaximumStoredSessions) return sessions;
            return sessions.GetRange(sessions.Count - MaximumStoredSessions, MaximumStoredSessions);
        }

        private static long NowMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static string MakeRecordingId()
        {
            return "nav-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        internal static double RoundForRecording(float value, int places)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0d;
            var scale = Math.Pow(10d, places);
            return Math.Floor(value * scale + 0.5d) / scale;
        }

        private static double[] RoundVector(Vector3 value, int places)
        {
            return new[]
            {
                RoundForRecording(value.x, places),
                RoundForRecording(value.y, places),
                RoundForRecording(value.z, places)
            };
        }
    }

    [Serializable]
    internal sealed class WofNavigationInput
    {
        public double forward;
        public double strafe;
        public bool sprint;
        public bool jump;
        public bool slide;
        public bool vclip;
    }

    [Serializable]
    internal sealed class WofNavigationState
    {
        public bool grounded;
        public bool moving;
        public bool sliding;
        public bool crouching;
        public bool sprinting;
        public bool spellMenuOpen;
    }

    [Serializable]
    internal sealed class WofNavigationSample
    {
        public string gameMode;
        public double[] pos;
        public double[] rot;
        public double[] aimDir;
        public double[] velocity;
        public WofNavigationInput input;
        public WofNavigationState state;
        public long t;
        public int[] chunk;
    }

    [Serializable]
    internal sealed class WofNavigationRecordingSession
    {
        public string id;
        public string label;
        public int version;
        public long startedAt;
        public long endedAt;
        public long durationMs;
        public int sampleIntervalMs;
        public List<WofNavigationSample> samples = new();
        [NonSerialized] public long lastSampleAt;
    }

    [Serializable]
    internal sealed class WofNavigationSessionCollection
    {
        public List<WofNavigationRecordingSession> sessions = new();
    }

    [Serializable]
    internal sealed class WofNavigationExport
    {
        public long exportedAt;
        public bool activeRecording;
        public List<WofNavigationRecordingSession> sessions = new();
    }
}
