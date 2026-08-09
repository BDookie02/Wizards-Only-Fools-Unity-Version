using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofPerformanceModePolicyTests
    {
        [Test]
        public void DesktopUserAgentRemainsDesktopWithTouchAndShortScreen()
        {
            var metrics = Metrics(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                "Win32",
                10,
                390,
                844);

            Assert.That(WofPerformanceModePolicy.ResolveNativeMobileLikeDevice(metrics), Is.False);
        }

        [TestCase("Mozilla/5.0 (Linux; Android 15; Pixel)", "Linux armv8l", 5)]
        [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)", "iPhone", 5)]
        public void NativeMobileUserAgentsResolveMobile(string userAgent, string platform, int touchPoints)
        {
            Assert.That(
                WofPerformanceModePolicy.ResolveNativeMobileLikeDevice(
                    Metrics(userAgent, platform, touchPoints, 390, 844)),
                Is.True);
        }

        [Test]
        public void IpadDesktopUserAgentResolvesMobileFromMacTouchSignature()
        {
            var metrics = Metrics(
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15)",
                "MacIntel",
                5,
                1024,
                1366);

            Assert.That(WofPerformanceModePolicy.ResolveNativeMobileLikeDevice(metrics), Is.True);
        }

        [TestCase(900, true)]
        [TestCase(901, false)]
        [TestCase(0, true)]
        public void UnknownTouchDeviceUsesReactShortSideBoundary(int shortSide, bool expected)
        {
            var metrics = Metrics("Unknown", "Unknown", 1, shortSide, 1200);

            Assert.That(WofPerformanceModePolicy.ResolveNativeMobileLikeDevice(metrics), Is.EqualTo(expected));
        }

        [TestCase("?perf=quality&mobilePerf=1", "", "", true, false)]
        [TestCase("?quality=1&perf=mobile", "", "", true, false)]
        [TestCase("?mobilePerf=1", "", "", false, true)]
        [TestCase("?perf=mobile", "", "", false, true)]
        [TestCase("?pcStandalone=1", "", "", true, false)]
        [TestCase("?pcStandalone=1&qaTouchLayout=1", "", "", false, true)]
        [TestCase("?qaMobileLayout=1", "", "", false, true)]
        [TestCase("?touchControls=1", "", "", false, true)]
        [TestCase("?qaTouchControls=1", "", "", false, true)]
        [TestCase("", "1", "1", true, false)]
        [TestCase("", "", "1", false, true)]
        [TestCase("", "", "", true, true)]
        [TestCase("", "", "", false, false)]
        public void PerformancePreferencePrecedenceMatchesReact(
            string query,
            string qualityPreference,
            string mobilePreference,
            bool nativeMobile,
            bool expected)
        {
            Assert.That(
                WofPerformanceModePolicy.ResolveMobilePerformanceMode(
                    query,
                    qualityPreference,
                    mobilePreference,
                    nativeMobile),
                Is.EqualTo(expected));
        }

        [TestCase("", false, false)]
        [TestCase("", true, true)]
        [TestCase("?pcStandalone=1", true, false)]
        [TestCase("?pcStandalone=1&qaTouchLayout=1", true, false)]
        [TestCase("?qaTouchLayout=1", false, false)]
        [TestCase("?qaMobileLayout=1", false, false)]
        [TestCase("?touchControls=1", false, true)]
        [TestCase("?qaTouchControls=1", false, true)]
        [TestCase("?pcStandalone=1&touchControls=1", false, true)]
        public void TouchGameplayDevicePrecedenceMatchesReact(
            string query,
            bool nativeMobile,
            bool expected)
        {
            Assert.That(
                WofPerformanceModePolicy.ResolveTouchGameplayDevice(query, nativeMobile),
                Is.EqualTo(expected));
        }

        [TestCase("?wofVillagerQa=1", true)]
        [TestCase("https://example.invalid/?pcStandalone=1&wofVillagerQa=%31#ignored", true)]
        [TestCase("?wofVillagerQa=0", false)]
        [TestCase("?WofVillagerQa=1", false)]
        [TestCase("", false)]
        public void VillagerViewProbeRequiresExactQaQueryFlag(string query, bool expected)
        {
            Assert.That(WofPerformanceModePolicy.ResolveVillagerViewProbe(query), Is.EqualTo(expected));
        }

        [TestCase("?wofDarrelQa=1", true)]
        [TestCase("?wofDarrelQa=0", false)]
        [TestCase("?WofDarrelQa=1", false)]
        [TestCase("?wofVillagerQa=1", false)]
        [TestCase("", false)]
        public void DarrelDialogProbeRequiresExactQaQueryFlag(string query, bool expected)
        {
            Assert.That(WofPerformanceModePolicy.ResolveDarrelDialogProbe(query), Is.EqualTo(expected));
        }

        [Test]
        public void UrlSearchParamsFirstDuplicateValueWinsLikeReact()
        {
            Assert.That(
                WofPerformanceModePolicy.ResolveMobilePerformanceMode(
                    "?perf=quality&perf=mobile",
                    string.Empty,
                    string.Empty,
                    false),
                Is.False);
        }

        [Test]
        public void QueryDecodingMatchesUrlSearchParams()
        {
            Assert.That(
                WofPerformanceModePolicy.ResolveMobilePerformanceMode(
                    "https://example.invalid/?mobilePerf=%31#ignored",
                    string.Empty,
                    string.Empty,
                    false),
                Is.True);
        }

        private static WofPerformanceDeviceMetrics Metrics(
            string userAgent,
            string platform,
            int touchPoints,
            int screenWidth,
            int screenHeight)
        {
            return new WofPerformanceDeviceMetrics(
                userAgent,
                platform,
                touchPoints,
                screenWidth,
                screenHeight,
                screenWidth,
                screenHeight);
        }
    }
}
