using System.Linq;
using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofBushLayoutTests
    {
        [Test]
        public void DesktopBushInventoryMatchesReactGoldenFixture()
        {
            var bushes = WofBushLayout.BuildBushes();
            var lobes = WofBushLayout.BuildLobes(bushes);

            Assert.That(bushes, Has.Count.EqualTo(129));
            Assert.That(lobes, Has.Count.EqualTo(645));
            Assert.That(lobes.Count(item => item.ColorIndex == 0), Is.EqualTo(215));
            Assert.That(lobes.Count(item => item.ColorIndex == 1), Is.EqualTo(215));
            Assert.That(lobes.Count(item => item.ColorIndex == 2), Is.EqualTo(215));
        }

        [Test]
        public void FirstBushMatchesReactGoldenFixture()
        {
            var bush = WofBushLayout.BuildBushes()[0];

            Assert.That(bush.X, Is.EqualTo(157.0318370894529d).Within(0.0000001d));
            Assert.That(bush.Y, Is.EqualTo(2d));
            Assert.That(bush.Z, Is.EqualTo(13.083016932941973d).Within(0.0000001d));
            Assert.That(bush.WidthScale, Is.EqualTo(8.446017392812161d).Within(0.0000001d));
            Assert.That(bush.HeightScale, Is.EqualTo(3.934944319538772d).Within(0.0000001d));
            Assert.That(bush.Yaw, Is.EqualTo(2.908490084320703d).Within(0.0000001d));
            Assert.That(bush.Variant, Is.EqualTo(0.7081778754945844d).Within(0.0000001d));
        }

        [Test]
        public void FirstCenterLobeMatchesReactGoldenFixture()
        {
            var lobe = WofBushLayout.BuildLobes(WofBushLayout.BuildBushes())[0];

            Assert.That(lobe.BushIndex, Is.EqualTo(0));
            Assert.That(lobe.LobeIndex, Is.EqualTo(0));
            Assert.That(lobe.ColorIndex, Is.EqualTo(0));
            Assert.That(lobe.X, Is.EqualTo(157.0318370894529d).Within(0.0000001d));
            Assert.That(lobe.Y, Is.EqualTo(3.6133271710108965d).Within(0.0000001d));
            Assert.That(lobe.Z, Is.EqualTo(13.083016932941973d).Within(0.0000001d));
            Assert.That(lobe.Yaw, Is.EqualTo(3.227170128293266d).Within(0.0000001d));
            Assert.That(lobe.Width, Is.EqualTo(4.72976973997481d).Within(0.0000001d));
            Assert.That(lobe.Height, Is.EqualTo(3.226654342021793d).Within(0.0000001d));
            Assert.That(lobe.Depth, Is.EqualTo(2.5970632508955895d).Within(0.0000001d));
        }

        [Test]
        public void SeededRandomMatchesReactMulberryFixture()
        {
            var random = new WofSeededRandom("base-village-bushes:510");

            Assert.That(random.NextDouble(), Is.EqualTo(0.8079055629204959d).Within(0.0000000001d));
            Assert.That(random.NextDouble(), Is.EqualTo(0.5256529743783176d).Within(0.0000000001d));
        }
    }
}
