using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests.EditMode
{
    public sealed class WofAndroidOrientationTests
    {
        [Test]
        public void PlayerSettingsAllowOnlyLandscapeAutorotation()
        {
            var settingsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset"));
            var settings = File.ReadAllText(settingsPath);

            StringAssert.Contains("defaultScreenOrientation: 4", settings);
            StringAssert.Contains("allowedAutorotateToPortrait: 0", settings);
            StringAssert.Contains("allowedAutorotateToPortraitUpsideDown: 0", settings);
            StringAssert.Contains("allowedAutorotateToLandscapeRight: 1", settings);
            StringAssert.Contains("allowedAutorotateToLandscapeLeft: 1", settings);
        }

        [Test]
        public void RecreationAutomationPreservesLandscapeOnlyAutorotation()
        {
            var automationPath = Path.GetFullPath(Path.Combine(Application.dataPath, "WOF", "Editor", "WofProjectAutomation.cs"));
            var automation = File.ReadAllText(automationPath);

            StringAssert.Contains("PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;", automation);
            StringAssert.Contains("PlayerSettings.allowedAutorotateToPortrait = false;", automation);
            StringAssert.Contains("PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;", automation);
            StringAssert.Contains("PlayerSettings.allowedAutorotateToLandscapeLeft = true;", automation);
            StringAssert.Contains("PlayerSettings.allowedAutorotateToLandscapeRight = true;", automation);
        }
    }
}
