using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofVivoxProjectSettingsTests
    {
        private const string ExpectedServer =
            "https://unity.vivox.com/appconfig/24741-wizar-40950";
        private const string ExpectedDomain = "mtu1xp.vivox.com";
        private const string ExpectedIssuer = "24741-wizar-40950";

        [Test]
        public void ProductionSettingsContainPublicCredentialsWithoutTokenKey()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);

            var settingsPath = Path.Combine(
                projectRoot,
                "ProjectSettings",
                "Packages",
                "com.unity.services.vivox",
                "Settings.json");
            Assert.That(File.Exists(settingsPath), Is.True, settingsPath);

            var json = File.ReadAllText(settingsPath);
            Assert.That(json, Does.Contain(ExpectedServer));
            Assert.That(json, Does.Contain(ExpectedDomain));
            Assert.That(json, Does.Contain(ExpectedIssuer));
            Assert.That(json, Does.Contain("isServiceEnabled"));
            Assert.That(json, Does.Contain("isTestMode"));
            Assert.That(json, Does.Contain("isEnvironmentCustom"));
            Assert.That(
                json.IndexOf("tokenKey", StringComparison.OrdinalIgnoreCase),
                Is.EqualTo(-1),
                "Production builds must never persist the Vivox token key.");
        }
    }
}
