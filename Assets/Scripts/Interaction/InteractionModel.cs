using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Interaction
{
    [Serializable] public sealed class EntityDefinition
    {
        public string id, role, name, item_id, tags, accepts, behavior_id, sprite_path;
        public float x, y, height;
        public int capacity = 1;
        public bool HasTag(string tag) => (tags ?? "").Split('|').Contains(tag);
        public bool Accepts(EntityDefinition item) => (accepts ?? "").Split('|').Any(t => t.Length > 0 && item.HasTag(t));
    }

    [Serializable] public sealed class ItemState
    {
        public string instanceId, definitionId, locationId, bindingId;
        public int amount = 1;
        public List<string> contents = new();
        public string payloadJson;
    }
    [Serializable] public sealed class PlacementSnapshot
    {
        public int version = 1, nextId = 1;
        public List<ItemState> items = new();
    }
    public readonly struct InteractionTarget
    {
        public readonly EntityDefinition Definition;
        public readonly ItemState Item;
        public InteractionTarget(EntityDefinition definition, ItemState item = null) { Definition = definition; Item = item; }
    }
    public readonly struct InteractionResult
    {
        public readonly bool Success;
        public readonly string Message;
        public InteractionResult(bool success, string message = "") { Success = success; Message = message; }
        public static InteractionResult Accept(string message = "") => new(true, message);
        public static InteractionResult Reject(string message) => new(false, message);
    }

    /// <summary>Pure state: no input, scene objects, animations, inventory or cafe rules.</summary>
    public sealed class PlacementLedger
    {
        private readonly Dictionary<string, EntityDefinition> definitions;
        private PlacementSnapshot state = new();
        public IReadOnlyList<ItemState> Items => state.items;
        public PlacementLedger(IEnumerable<EntityDefinition> definitions) => this.definitions = definitions.ToDictionary(d => d.id);
        public EntityDefinition Definition(string id) => definitions.GetValueOrDefault(id);
        public ItemState Find(string instanceId) => state.items.Find(i => i.instanceId == instanceId);
        public ItemState At(string locationId) => state.items.Find(i => i.locationId == locationId);
        public InteractionResult CanPlace(ItemState item, string locationId)
        {
            if (item == null || item.amount <= 0 || Definition(item.definitionId) is not { } definition) return InteractionResult.Reject("无效物品");
            var slot = Definition(locationId);
            if (slot == null || slot.role != "slot" && slot.role != "machine") return InteractionResult.Reject("这里不能放置物品");
            if (!slot.Accepts(definition)) return InteractionResult.Reject("这里不接受这种物品");
            int count = state.items.Count(i => i.locationId == locationId && i.instanceId != item.instanceId);
            if (count >= Math.Max(1, slot.capacity)) return InteractionResult.Reject("位置已经被占用");
            return InteractionResult.Accept();
        }
        public InteractionResult Place(ItemState item, string locationId)
        {
            var result = CanPlace(item, locationId);
            if (!result.Success) return result;
            if (string.IsNullOrEmpty(item.instanceId)) item.instanceId = "item_" + state.nextId++;
            var existing = Find(item.instanceId);
            if (existing != null && !ReferenceEquals(existing, item)) return InteractionResult.Reject("重复物品标识");
            if (existing == null) state.items.Add(item);
            item.locationId = locationId;
            return result;
        }
        public void Remove(string instanceId) => state.items.RemoveAll(i => i.instanceId == instanceId);
        public void Clear() => state.items.Clear();
        public PlacementSnapshot Capture() => state;
        public InteractionResult Restore(PlacementSnapshot snapshot)
        {
            if (snapshot == null || snapshot.version != 1 || snapshot.items == null) return InteractionResult.Reject("不支持的摆放存档");
            var candidate = new PlacementLedger(definitions.Values);
            var ids = new HashSet<string>();
            foreach (var item in snapshot.items)
            {
                if (item == null || string.IsNullOrEmpty(item.instanceId) || !ids.Add(item.instanceId)) return InteractionResult.Reject("摆放存档包含重复物品");
                if (!candidate.Place(item, item.locationId).Success) return InteractionResult.Reject("摆放存档与当前配置不兼容");
            }
            snapshot.nextId = Math.Max(snapshot.nextId, snapshot.items.Count + 1);
            foreach (var item in snapshot.items)
                if (item.instanceId.StartsWith("item_") && int.TryParse(item.instanceId.Substring(5), out var allocated)) snapshot.nextId = Math.Max(snapshot.nextId, allocated + 1);
            state = snapshot;
            return InteractionResult.Accept();
        }
    }
}
