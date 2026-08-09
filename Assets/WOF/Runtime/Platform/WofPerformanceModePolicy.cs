using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WOF
{
    public readonly struct WofPerformanceDeviceMetrics
    {
        public WofPerformanceDeviceMetrics(
            string userAgent,
            string platform,
            int maxTouchPoints,
            int screenWidth,
            int screenHeight,
            int innerWidth,
            int innerHeight)
        {
            UserAgent = userAgent ?? string.Empty;
            Platform = platform ?? string.Empty;
            MaxTouchPoints = maxTouchPoints;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            InnerWidth = innerWidth;
            InnerHeight = innerHeight;
        }

        public string UserAgent { get; }
        public string Platform { get; }
        public int MaxTouchPoints { get; }
        public int ScreenWidth { get; }
        public int ScreenHeight { get; }
        public int InnerWidth { get; }
        public int InnerHeight { get; }
    }

    /// <summary>
    /// Pure port of performanceModeRuntime.ts. Query values and stored user
    /// preferences intentionally retain the same precedence as the React game.
    /// </summary>
    public static class WofPerformanceModePolicy
    {
        public static bool ResolveNativeMobileLikeDevice(WofPerformanceDeviceMetrics metrics)
        {
            var userAgent = metrics.UserAgent;
            var hasTouch = metrics.MaxTouchPoints > 0;
            var screenWidth = ResolveDimension(metrics.ScreenWidth, metrics.InnerWidth);
            var screenHeight = ResolveDimension(metrics.ScreenHeight, metrics.InnerHeight);
            var shortSide = Math.Min(screenWidth, screenHeight);

            var iosLike = ContainsOrdinal(userAgent, "iPad") ||
                          ContainsOrdinal(userAgent, "iPhone") ||
                          ContainsOrdinal(userAgent, "iPod") ||
                          (string.Equals(metrics.Platform, "MacIntel", StringComparison.Ordinal) &&
                           metrics.MaxTouchPoints > 1);
            var androidLike = ContainsIgnoreCase(userAgent, "Android");
            if (iosLike || androidLike)
            {
                return true;
            }

            if (ContainsIgnoreCase(userAgent, "Windows NT") ||
                ContainsIgnoreCase(userAgent, "Macintosh") ||
                ContainsIgnoreCase(userAgent, "X11") ||
                ContainsIgnoreCase(userAgent, "Linux x86_64"))
            {
                return false;
            }

            var mobileUserAgent = ContainsIgnoreCase(userAgent, "Mobile") ||
                                  ContainsIgnoreCase(userAgent, "Tablet") ||
                                  ContainsIgnoreCase(userAgent, "iPhone") ||
                                  ContainsIgnoreCase(userAgent, "iPad") ||
                                  ContainsIgnoreCase(userAgent, "iPod");
            return mobileUserAgent || (hasTouch && shortSide <= 900);
        }

        public static bool ResolveMobilePerformanceMode(
            string query,
            string qualityPreference,
            string mobilePreference,
            bool nativeMobileLikeDevice)
        {
            var parameters = ParseQuery(query);
            if (HasValue(parameters, "perf", "quality") || HasValue(parameters, "quality", "1"))
            {
                return false;
            }

            if (HasValue(parameters, "mobilePerf", "1") || HasValue(parameters, "perf", "mobile"))
            {
                return true;
            }

            var forcedMobile = IsForcedMobileQaMode(parameters);
            if (HasValue(parameters, "pcStandalone", "1") && !forcedMobile)
            {
                return false;
            }

            if (string.Equals(qualityPreference, "1", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(mobilePreference, "1", StringComparison.Ordinal))
            {
                return true;
            }

            return forcedMobile || nativeMobileLikeDevice;
        }

        public static bool ResolveTouchGameplayDevice(string query, bool nativeMobileLikeDevice)
        {
            var parameters = ParseQuery(query);
            var forcedTouchControls = HasValue(parameters, "touchControls", "1") ||
                                      HasValue(parameters, "qaTouchControls", "1");
            if (HasValue(parameters, "pcStandalone", "1") && !forcedTouchControls)
            {
                return false;
            }

            return forcedTouchControls || nativeMobileLikeDevice;
        }

        public static bool ResolveVillagerViewProbe(string query)
        {
            return HasValue(ParseQuery(query), "wofVillagerQa", "1");
        }

        public static bool ResolveDarrelDialogProbe(string query)
        {
            return HasValue(ParseQuery(query), "wofDarrelQa", "1");
        }

        private static bool IsForcedMobileQaMode(IReadOnlyDictionary<string, string> parameters)
        {
            return HasValue(parameters, "qaTouchLayout", "1") ||
                   HasValue(parameters, "qaMobileLayout", "1") ||
                   HasValue(parameters, "touchControls", "1") ||
                   HasValue(parameters, "qaTouchControls", "1");
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            var questionMark = query.IndexOf('?');
            if (questionMark >= 0)
            {
                query = query.Substring(questionMark + 1);
            }
            var fragment = query.IndexOf('#');
            if (fragment >= 0)
            {
                query = query.Substring(0, fragment);
            }

            foreach (var pair in query.Split('&'))
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }
                var equals = pair.IndexOf('=');
                var key = equals >= 0 ? pair.Substring(0, equals) : pair;
                var value = equals >= 0 ? pair.Substring(equals + 1) : string.Empty;
                key = Decode(key);
                if (!result.ContainsKey(key))
                {
                    result[key] = Decode(value);
                }
            }
            return result;
        }

        private static string Decode(string value)
        {
            return Uri.UnescapeDataString((value ?? string.Empty).Replace('+', ' '));
        }

        private static bool HasValue(IReadOnlyDictionary<string, string> parameters, string key, string value)
        {
            return parameters.TryGetValue(key, out var current) &&
                   string.Equals(current, value, StringComparison.Ordinal);
        }

        private static bool ContainsOrdinal(string value, string token)
        {
            return (value ?? string.Empty).IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return (value ?? string.Empty).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ResolveDimension(int primary, int fallback)
        {
            return primary > 0 ? primary : Math.Max(0, fallback);
        }
    }

    public static class WofPerformanceModeRuntime
    {
        private static bool? s_MobilePerformanceMode;
        private static bool? s_TouchGameplayDevice;
        private static bool? s_VillagerViewProbe;
        private static bool? s_DarrelDialogProbe;

        public static bool IsMobilePerformanceMode
        {
            get
            {
                if (!s_MobilePerformanceMode.HasValue)
                {
                    s_MobilePerformanceMode = ResolveCurrentMode();
                    Debug.Log($"[WOF-AUTOMATION] MOBILE_PERFORMANCE_MODE enabled={s_MobilePerformanceMode.Value}");
                }
                return s_MobilePerformanceMode.Value;
            }
        }

        public static bool IsTouchGameplayDevice
        {
            get
            {
                if (!s_TouchGameplayDevice.HasValue)
                {
                    s_TouchGameplayDevice = ResolveCurrentTouchGameplayDevice();
                    Debug.Log($"[WOF-AUTOMATION] TOUCH_GAMEPLAY_DEVICE enabled={s_TouchGameplayDevice.Value}");
                }
                return s_TouchGameplayDevice.Value;
            }
        }

        public static bool IsVillagerViewProbe
        {
            get
            {
                if (!s_VillagerViewProbe.HasValue)
                {
                    s_VillagerViewProbe = ResolveCurrentVillagerViewProbe();
                    if (s_VillagerViewProbe.Value)
                    {
                        Debug.Log("[WOF-AUTOMATION] VILLAGER_VIEW_PROBE enabled=true");
                    }
                }
                return s_VillagerViewProbe.Value;
            }
        }

        public static bool IsDarrelDialogProbe
        {
            get
            {
                if (!s_DarrelDialogProbe.HasValue)
                {
                    s_DarrelDialogProbe = ResolveCurrentDarrelDialogProbe();
                    if (s_DarrelDialogProbe.Value)
                    {
                        Debug.Log("[WOF-AUTOMATION] DARREL_DIALOG_PROBE enabled=true");
                    }
                }
                return s_DarrelDialogProbe.Value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_MobilePerformanceMode = null;
            s_TouchGameplayDevice = null;
            s_VillagerViewProbe = null;
            s_DarrelDialogProbe = null;
        }

        private static bool ResolveCurrentDarrelDialogProbe()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-darrel-dialog-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return WofPerformanceModePolicy.ResolveDarrelDialogProbe(Application.absoluteURL);
#else
            return false;
#endif
        }

        private static bool ResolveCurrentVillagerViewProbe()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-villager-view-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return WofPerformanceModePolicy.ResolveVillagerViewProbe(Application.absoluteURL);
#else
            return false;
#endif
        }

        private static bool ResolveCurrentTouchGameplayDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var metrics = WofBrowserPerformanceInterop.ReadMetrics();
            var nativeMobile = WofPerformanceModePolicy.ResolveNativeMobileLikeDevice(metrics);
            return WofPerformanceModePolicy.ResolveTouchGameplayDevice(Application.absoluteURL, nativeMobile);
#else
            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
#endif
        }

        private static bool ResolveCurrentMode()
        {
            var forceQuality = false;
            var forceMobile = false;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-quality-performance", StringComparison.OrdinalIgnoreCase))
                {
                    forceQuality = true;
                }
                else if (argument.Equals("--wof-mobile-performance", StringComparison.OrdinalIgnoreCase))
                {
                    forceMobile = true;
                }
            }
            if (forceQuality)
            {
                return false;
            }
            if (forceMobile)
            {
                return true;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            var metrics = WofBrowserPerformanceInterop.ReadMetrics();
            var nativeMobile = WofPerformanceModePolicy.ResolveNativeMobileLikeDevice(metrics);
            return WofPerformanceModePolicy.ResolveMobilePerformanceMode(
                Application.absoluteURL,
                WofBrowserPerformanceInterop.ReadQualityPreference(),
                WofBrowserPerformanceInterop.ReadMobilePreference(),
                nativeMobile);
#else
            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
#endif
        }
    }

    internal static class WofBrowserPerformanceInterop
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string WofPerformance_GetUserAgent();
        [DllImport("__Internal")] private static extern string WofPerformance_GetPlatform();
        [DllImport("__Internal")] private static extern int WofPerformance_GetMaxTouchPoints();
        [DllImport("__Internal")] private static extern int WofPerformance_GetScreenWidth();
        [DllImport("__Internal")] private static extern int WofPerformance_GetScreenHeight();
        [DllImport("__Internal")] private static extern int WofPerformance_GetInnerWidth();
        [DllImport("__Internal")] private static extern int WofPerformance_GetInnerHeight();
        [DllImport("__Internal")] private static extern string WofPerformance_GetQualityPreference();
        [DllImport("__Internal")] private static extern string WofPerformance_GetMobilePreference();
#endif

        internal static WofPerformanceDeviceMetrics ReadMetrics()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WofPerformanceDeviceMetrics(
                WofPerformance_GetUserAgent(),
                WofPerformance_GetPlatform(),
                WofPerformance_GetMaxTouchPoints(),
                WofPerformance_GetScreenWidth(),
                WofPerformance_GetScreenHeight(),
                WofPerformance_GetInnerWidth(),
                WofPerformance_GetInnerHeight());
#else
            return default;
#endif
        }

        internal static string ReadQualityPreference()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WofPerformance_GetQualityPreference() ?? string.Empty;
#else
            return string.Empty;
#endif
        }

        internal static string ReadMobilePreference()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WofPerformance_GetMobilePreference() ?? string.Empty;
#else
            return string.Empty;
#endif
        }
    }
}
