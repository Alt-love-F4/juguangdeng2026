using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public enum NarrativeInventoryOwner { MapInventory, CafeSupply }

    [Serializable]
    public sealed class NarrativeItemDefinition
    {
        public string itemKey;
        public string nameText;
        public string iconPath;
        public string descText;
        public NarrativeInventoryOwner inventoryOwner;
    }

    public static class NarrativeItemCatalog
    {
        public static NarrativeInventoryOwner ParseOwner(string owner, string itemKey)
        {
            switch (owner)
            {
                case "MapInventory": return NarrativeInventoryOwner.MapInventory;
                case "CafeSupply":
                    throw new FormatException("Item " + itemKey + ": CafeSupply inventory owner is unsupported; no authoritative supply mapping is configured.");
                default:
                    throw new FormatException("Item " + itemKey + ": unknown inventory owner " + owner);
            }
        }

        // Pass references from action Item costs, ITEM_ADD/ITEM_REMOVE results and ITEM_GTE conditions.
        // An empty catalog is valid when there are no item references.
        public static void Validate(IEnumerable<NarrativeItemDefinition> items, IEnumerable<string> referencedItemKeys = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemKey))
                    throw new FormatException("Item catalog contains an empty itemKey.");
                if (!keys.Add(item.itemKey))
                    throw new FormatException("Duplicate item catalog key: " + item.itemKey);
                if (item.inventoryOwner == NarrativeInventoryOwner.CafeSupply)
                    throw new FormatException("Item " + item.itemKey + ": CafeSupply inventory owner is unsupported; no authoritative supply mapping is configured.");
                if (item.inventoryOwner != NarrativeInventoryOwner.MapInventory)
                    throw new FormatException("Item " + item.itemKey + ": unknown inventory owner " + item.inventoryOwner);
            }

            if (referencedItemKeys == null) return;
            foreach (var key in referencedItemKeys)
                if (string.IsNullOrWhiteSpace(key) || !keys.Contains(key))
                    throw new FormatException("Unknown item catalog reference: " + key);
        }
    }
}
