using System;

namespace WOF
{
    public enum WofPublicSessionState
    {
        Idle,
        Initializing,
        Creating,
        Joining,
        Connected,
        Failed
    }

    public static class WofPublicSessionRules
    {
        public const string CloudProjectRequired =
            "PUBLIC ONLINE REQUIRES A LINKED UNITY CLOUD PROJECT.";

        public const string NetworkConfigurationRequired =
            "PUBLIC ONLINE NETWORK CONFIGURATION IS MISSING.";

        public const string SessionAlreadyRunning =
            "A SESSION IS ALREADY RUNNING.";

        public const string JoinCodeRequired =
            "ENTER A PUBLIC INVITE CODE.";

        public static string NormalizeJoinCode(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        public static string GetAvailabilityError(
            string cloudProjectId,
            bool hasNetworkManager,
            bool hasUnityTransport,
            bool sessionIsRunning)
        {
            if (string.IsNullOrWhiteSpace(cloudProjectId))
            {
                return CloudProjectRequired;
            }

            if (!hasNetworkManager || !hasUnityTransport)
            {
                return NetworkConfigurationRequired;
            }

            return sessionIsRunning ? SessionAlreadyRunning : string.Empty;
        }

        public static string FormatSessionError(string errorName)
        {
            return errorName switch
            {
                "NotAuthorized" => "PUBLIC ONLINE SIGN-IN WAS REJECTED.",
                "SessionNotFound" => "PUBLIC LOBBY NOT FOUND. CHECK THE INVITE CODE.",
                "SessionDeleted" => "THAT PUBLIC LOBBY HAS CLOSED.",
                "RateLimitExceeded" => "PUBLIC ONLINE IS BUSY. TRY AGAIN IN A MOMENT.",
                "InvalidParameter" => "THE PUBLIC INVITE CODE IS INVALID.",
                "InvalidSessionIdentifier" => "THE PUBLIC INVITE CODE IS INVALID.",
                "NetworkManagerNotInitialized" => NetworkConfigurationRequired,
                "TransportComponentMissing" => NetworkConfigurationRequired,
                "TranportComponentMissing" => NetworkConfigurationRequired,
                "NetworkManagerStartFailed" => "PUBLIC ONLINE COULD NOT START THE GAME NETWORK.",
                "NetworkSetupFailed" => "PUBLIC RELAY CONNECTION FAILED.",
                "QoSMeasurementFailed" => "PUBLIC RELAY COULD NOT SELECT A LOW-LATENCY REGION.",
                _ => "PUBLIC ONLINE CONNECTION FAILED."
            };
        }
    }
}
