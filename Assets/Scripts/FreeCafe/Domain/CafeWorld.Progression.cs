using System;

namespace Spotlight.FreeCafe
{
    /// <summary>Session-scoped equipment progression. Legacy domain callers remain unrestricted.</summary>
    public sealed partial class CafeWorld
    {
        public const int PrecisionOrdersRequired = 5;
        public const int LatteOrdersRequired = 12;
        public const int PrecisionActivationCost = 200;
        public const int LatteActivationCost = 350;
        public bool PrecisionEquipmentAvailable => !State.equipmentProgressionEnabled || State.sandboxEquipment || State.precisionEquipmentActive;
        public bool LatteEquipmentAvailable => !State.equipmentProgressionEnabled || State.sandboxEquipment || State.latteEquipmentActive;

        public void ConfigureEquipmentProgression(bool sandbox)
        {
            if (!State.equipmentProgressionConfigured)
            {
                State.equipmentProgressionConfigured = true;
                State.equipmentProgressionEnabled = true;
            }
            State.sandboxEquipment = sandbox;
            if (sandbox)
            {
                foreach (var product in Catalog.products)
                    if (!State.unlockedProducts.Contains(product.id)) State.unlockedProducts.Add(product.id);
            }
            else State.unlockedProducts.RemoveAll(id => !CanOfferProduct(Product(id)));
            UnlockContent();
            Notify();
        }

        public bool CanOfferProduct(ProductSpec product)
        {
            if (product == null) return false;
            if (!State.equipmentProgressionEnabled || State.sandboxEquipment) return true;
            bool needsPrecision = false, needsLatte = product.requiresLatteArt;
            foreach (var ingredient in product.ingredients)
            {
                needsPrecision |= ingredient.coffeeKind == "essence";
                needsLatte |= ingredient.specId == "milkfoam";
            }
            // First learn plain coffee, then milk/cream mixtures, then precision and latte art.
            if (State.successfulOrders < 2 && product.id != "blackcoffee" && product.id != "cold_brew") return false;
            return (!needsPrecision || PrecisionEquipmentAvailable) && (!needsLatte || LatteEquipmentAvailable);
        }

        public bool IsEquipmentAvailable(WorldEntity entity)
        {
            if (entity == null) return false;
            switch (entity.specId)
            {
                case "container.extractor":
                case "container.extractor.filter": return PrecisionEquipmentAvailable;
                case "tool.latte-art":
                case "container.milk-pitcher":
                case "machine.mixer": return LatteEquipmentAvailable;
                default: return true;
            }
        }

        public ActionResult ActivateEquipment(bool latte)
        {
            var check = EquipmentOrderCheck(latte);
            if (!check.success) return check;
            State.money -= latte ? LatteActivationCost : PrecisionActivationCost;
            if (latte) State.latteEquipmentActive = true; else State.precisionEquipmentActive = true;
            UnlockContent();
            Notify();
            return ActionResult.Yes(latte ? "拉花已激活：向拉花杯加入牛奶并点击打奶，再拖到饮品杯上开始拉花" : "精粹设备已激活：将咖啡粉装入滤杯，再用热水均匀注入");
        }
    }
}
