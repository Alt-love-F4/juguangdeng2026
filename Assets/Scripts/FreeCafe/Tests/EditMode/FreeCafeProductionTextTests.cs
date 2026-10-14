using NUnit.Framework;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProductionTextTests
    {
        [TestCase("奶泡", "拉花")]
        [TestCase("奶沫20ml与薄奶泡", "拉花20ml与薄拉花")]
        [TestCase("精粹咖啡与热牛奶", "精粹咖啡与热牛奶")]
        [TestCase(null, null)]
        public void RecipeReferencesUseLatteArtTerminology(string input, string expected)
            => Assert.That(FreeCafeProductionText.Recipe(input), Is.EqualTo(expected));

        [TestCase("heart", "爱心")]
        [TestCase("rosetta", "树叶")]
        [TestCase("tulip", "郁金香")]
        [TestCase("x", "X 形")]
        [TestCase("a", "A 形")]
        [TestCase("unknown", "未识别")]
        [TestCase(null, "未识别")]
        [TestCase("", "未识别")]
        [TestCase("custom-pattern", "custom-pattern")]
        public void FinishedCupIncludesVolumeAndReadablePattern(string id, string label)
            => Assert.That(FreeCafeProductionText.LatteArtContents(25, id), Is.EqualTo("拉花25ML·图案" + label));
    }
}
