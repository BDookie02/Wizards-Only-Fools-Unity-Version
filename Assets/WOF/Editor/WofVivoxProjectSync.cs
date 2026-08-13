using System;
using UnityEditor.SettingsManagement;
using UnityEngine;

namespace WOF.Editor
{
    public static class WofVivoxProjectSync
    {
        private const string PackageName = "com.unity.services.vivox";
        private const string SettingsName = "Settings";
        private const string Server = "https://unity.vivox.com/appconfig/24741-wizar-40950";
        private const string Domain = "mtu1xp.vivox.com";
        private const string TokenIssuer = "24741-wizar-40950";

        public static void SyncProductionCredentialsBatch()
        {
            SyncProductionCredentials();
        }

        public static void SyncProductionCredentials()
        {
            var repository = new PackageSettingsRepository(PackageName, SettingsName);
            repository.Set("server", Server);
            repository.Set("domain", Domain);
            repository.Set("tokenIssuer", TokenIssuer);
            repository.Set("isServiceEnabled", true);
            repository.Set("isTestMode", false);
            repository.Set("isEnvironmentCustom", false);
            repository.Remove<string>("tokenKey");
            repository.Save();

            var saved = new PackageSettingsRepository(PackageName, SettingsName);
            if (!string.Equals(saved.Get<string>("server"), Server, StringComparison.Ordinal) ||
                !string.Equals(saved.Get<string>("domain"), Domain, StringComparison.Ordinal) ||
                !string.Equals(saved.Get<string>("tokenIssuer"), TokenIssuer, StringComparison.Ordinal) ||
                !saved.Get<bool>("isServiceEnabled") ||
                saved.Get<bool>("isTestMode") ||
                saved.Get<bool>("isEnvironmentCustom") ||
                saved.ContainsKey<string>("tokenKey"))
            {
                throw new InvalidOperationException(
                    "Vivox production project settings were not saved safely and completely.");
            }

            Debug.Log(
                "[WOF-VIVOX] PRODUCTION_SETTINGS_READY testMode=false tokenKeyStored=false");
        }
    }
}
