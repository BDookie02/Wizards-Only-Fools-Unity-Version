using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofAndroidLinkerTests
    {
        [Test]
        public void RuntimePrimitiveCollidersArePreservedForIl2CppBuilds()
        {
            var linkPath = Path.Combine(Application.dataPath, "WOF", "link.xml");
            Assert.That(File.Exists(linkPath), Is.True, linkPath);

            var contents = File.ReadAllText(linkPath);
            Assert.That(contents, Does.Contain("UnityEngine.PhysicsModule"));
            Assert.That(contents, Does.Contain("UnityEngine.SphereCollider"));
            Assert.That(contents, Does.Contain("UnityEngine.CapsuleCollider"));
        }
    }
}
