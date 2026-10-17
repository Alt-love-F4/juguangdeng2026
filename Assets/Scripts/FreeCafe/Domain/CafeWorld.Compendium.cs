namespace Spotlight.FreeCafe
{
    /// <summary>Read-only queries for the 咖啡宝典 book: explain locks and quote purchases before the player commits.</summary>
    public sealed partial class CafeWorld
    {
        public bool ProductNeedsPrecision(ProductSpec product)
        {
            if (product?.ingredients == null) return false;
            foreach (var ingredient in product.ingredients)
                if (ingredient.coffeeKind == "essence") return true;
            return false;
        }

        public bool ProductNeedsLatte(ProductSpec product)
        {
            if (product == null) return false;
            if (product.requiresLatteArt) return true;
            if (product.ingredients == null) return false;
            foreach (var ingredient in product.ingredients)
                if (ingredient.specId == "milkfoam") return true;
            return false;
        }

        /// <summary>Null when the product is learned; otherwise the first requirement the player still lacks.</summary>
        public string ProductLockReason(ProductSpec product)
        {
            if (product == null) return "未知餐品";
            if (State.unlockedProducts.Contains(product.id)) return null;
            if (State.equipmentProgressionEnabled && !State.sandboxEquipment)
            {
                if (State.successfulOrders < 2 && product.id != "blackcoffee" && product.id != "cold_brew")
                    return "累计成功出品 2 单后学会（" + State.successfulOrders + " / 2）";
                if (ProductNeedsPrecision(product) && !PrecisionEquipmentAvailable) return "需订购「精粹萃取组」";
                if (ProductNeedsLatte(product) && !LatteEquipmentAvailable) return "需订购「拉花套装」";
            }
            if (State.reputation < product.unlockReputation) return "声望达到 " + product.unlockReputation + " 后学会";
            return "下次开店时学会";
        }

        /// <summary>Same checks as <see cref="ActivateEquipment"/>, without spending anything.</summary>
        public ActionResult EquipmentOrderCheck(bool latte)
        {
            if (!State.equipmentProgressionEnabled || State.sandboxEquipment) return ActionResult.No("自由模式已开放全部设备");
            if (latte ? LatteEquipmentAvailable : PrecisionEquipmentAvailable) return ActionResult.No("设备已经激活");
            if (State.open || HasTutorialCustomer) return ActionResult.No("请在教程结束后、营业前或打烊后激活设备");
            int required = latte ? LatteOrdersRequired : PrecisionOrdersRequired;
            int cost = latte ? LatteActivationCost : PrecisionActivationCost;
            if (latte && !PrecisionEquipmentAvailable) return ActionResult.No("请先激活精粹设备，再学习拉花");
            if (State.successfulOrders < required) return ActionResult.No("累计成功完成 " + required + " 单后可激活");
            if (State.money < cost) return ActionResult.No("激活需要 " + cost + " 王国币");
            return ActionResult.Yes("可以订购");
        }

        /// <summary>Price of topping one source up to its full stock, matching <see cref="BuySource"/>.</summary>
        public int RestockQuote(WorldEntity source, int beanQuality, out int missing)
        {
            missing = 0;
            var spec = source == null ? null : Spec(source.specId);
            if (spec == null || !spec.Has(Capability.Source)) return 0;
            missing = System.Math.Max(0, spec.initialStock - source.stock);
            if (missing == 0) return 0;
            if (spec.sourceItemId == "beans")
            {
                var prices = Catalog.economy.beanUnitPrices;
                int quality = beanQuality < 0 ? State.beanQuality : beanQuality;
                return prices != null && quality >= 0 && quality < prices.Length ? missing * prices[quality] : 0;
            }
            return spec.unitCost > 0 ? missing * spec.unitCost : spec.refillCost;
        }
    }
}
