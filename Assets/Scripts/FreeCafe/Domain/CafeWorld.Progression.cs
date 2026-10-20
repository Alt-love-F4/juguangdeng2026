using System;

namespace Spotlight.FreeCafe
{
    /// <summary>Session-scoped equipment progression. Legacy domain callers remain unrestricted.</summary>
    public sealed partial class CafeWorld
    {
        /// <summary>Successful purchase only, after state/content notifications; true means latte equipment. Restore never emits this.</summary>
        public event Action<bool> EquipmentAcquired;

        public const int PrecisionOrdersRequired = 5;
        public const int LatteOrdersRequired = 6;
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
            return State.successfulOrders >= product.unlockSuccessfulOrders
                && (!ProductNeedsPrecision(product) || PrecisionEquipmentAvailable)
                && (!ProductNeedsLatte(product) || LatteEquipmentAvailable);
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
            EquipmentAcquired?.Invoke(latte);
            return ActionResult.Yes(latte ? "拉花已激活：向拉花杯加入牛奶并点击打奶，再拖到饮品杯上开始拉花" : "精粹设备已激活：将咖啡粉装入滤杯，再用热水均匀注入");
        }
    }
}
