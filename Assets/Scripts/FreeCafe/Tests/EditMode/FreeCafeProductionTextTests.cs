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

        [TestCase("coffee", "essence", "咖啡液", "精粹咖啡液")]
        [TestCase("coffee", "black", "咖啡液", "普通咖啡液")]
        [TestCase("coffee", "cold_brew", "咖啡液", "冷萃咖啡液")]
        [TestCase("coffee", null, "咖啡液", "咖啡液")]
        [TestCase("milkfoam", null, "奶泡", "拉花")]
        [TestCase("hotmilk", null, "热牛奶", "热牛奶")]
        public void RecipeIngredientNamesDescribeRequiredCoffeeSource(string id, string kind, string label, string expected)
            => Assert.That(FreeCafeProductionText.RecipeIngredient(new Portion { specId = id, coffeeKind = kind }, label),
                Is.EqualTo(expected));

        [TestCase("heart", "爱心")]
        [TestCase("rosetta", "树叶")]
        [TestCase("tulip", "郁金香")]
        [TestCase("x", "X 形")]
        [TestCase("a", "A 形")]
        [TestCase("bean", "咖啡豆")]
        [TestCase("sword_crown", "剑与王冠")]
        [TestCase("concord_ring", "圣谐之环")]
        [TestCase("new_rose", "五瓣玫瑰")]
        [TestCase("broken_chain", "断链")]
        [TestCase("unknown", "未识别")]
        [TestCase(null, "未识别")]
        [TestCase("", "未识别")]
        [TestCase("custom-pattern", "custom-pattern")]
        public void FinishedCupIncludesVolumeAndReadablePattern(string id, string label)
            => Assert.That(FreeCafeProductionText.LatteArtContents(25, id), Is.EqualTo("拉花25ML·图案" + label));
    }
}
