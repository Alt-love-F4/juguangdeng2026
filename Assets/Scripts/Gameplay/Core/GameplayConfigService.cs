using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Spotlight.Interaction;
using UnityEngine;

namespace Spotlight.Gameplay
{
    public static class GameplayConfigService
    {
        [Serializable] private sealed class Document<T> { public T[] entries; }
        private static readonly Dictionary<string, RecipeEntry> recipes = new();
        private static readonly Dictionary<string, IngredientEntry> ingredients = new();
        private static readonly Dictionary<string, DeviceEntry> devices = new();
        private static readonly Dictionary<string, GameParamEntry> gameParams = new();
        private static readonly Dictionary<string, EntityDefinition> entities = new();
        private static readonly Dictionary<string, NpcEntry> npcs = new();
        private static readonly List<RecipeStepEntry> recipeSteps = new();
        private static bool loaded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCache() => loaded = false;
        public static void EnsureLoaded()
        {
            if (loaded) return;
            recipes.Clear(); ingredients.Clear(); devices.Clear(); gameParams.Clear(); entities.Clear(); npcs.Clear(); recipeSteps.Clear();
            Load("tbrecipe", (RecipeEntry e) => recipes.Add(e.id, e));
            Load("tbingredient", (IngredientEntry e) => ingredients.Add(e.id, e));
            Load("tbdevice", (DeviceEntry e) => devices.Add(e.id, e));
            Load("tbgameparam", (GameParamEntry e) => gameParams.Add(e.key, e));
            Load("tbworldobject", (EntityDefinition e) => entities.Add(e.id, e));
            Load("tbnpc", (NpcEntry e) => npcs.Add(e.id, e));
            Load("tbrecipestep", (RecipeStepEntry e) => recipeSteps.Add(e));
            loaded = true;
        }
        private static void Load<T>(string assetName, Action<T> onRow)
        {
            var asset = Resources.Load<TextAsset>("Luban/" + assetName);
            if (asset == null) throw new InvalidOperationException("缺少配表：" + assetName + "，请运行生成工具");
            var doc = JsonUtility.FromJson<Document<T>>("{\"entries\":" + asset.text + "}");
            if (doc?.entries == null) throw new InvalidOperationException("无效配表：" + assetName);
            foreach (var row in doc.entries) onRow(row);
        }
        public static RecipeEntry GetRecipe(string id) { EnsureLoaded(); return recipes.GetValueOrDefault(id); }
        public static IngredientEntry GetIngredient(string id) { EnsureLoaded(); return ingredients.GetValueOrDefault(id); }
        public static DeviceEntry GetDevice(string id) { EnsureLoaded(); return devices.GetValueOrDefault(id); }
        public static EntityDefinition GetEntity(string id) { EnsureLoaded(); return entities.GetValueOrDefault(id); }
        public static NpcEntry GetNpc(string id) { EnsureLoaded(); return npcs.GetValueOrDefault(id); }
        public static IEnumerable<RecipeStepEntry> GetSteps(string id) { EnsureLoaded(); return recipeSteps.Where(s => s.recipe_id == id).OrderBy(s => s.sequence); }
        public static string GetParam(string key, string fallback = "") { EnsureLoaded(); return gameParams.TryGetValue(key, out var e) ? e.value : fallback; }
        public static int GetParamInt(string key, int fallback = 0) => int.TryParse(GetParam(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        public static float GetParamFloat(string key, float fallback = 0f) => float.TryParse(GetParam(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        public static IEnumerable<RecipeEntry> AllRecipes { get { EnsureLoaded(); return recipes.Values; } }
        public static IEnumerable<IngredientEntry> AllIngredients { get { EnsureLoaded(); return ingredients.Values; } }
        public static IEnumerable<DeviceEntry> AllDevices { get { EnsureLoaded(); return devices.Values; } }
        public static IEnumerable<EntityDefinition> AllEntities { get { EnsureLoaded(); return entities.Values; } }
        public static IEnumerable<NpcEntry> AllNpcs { get { EnsureLoaded(); return npcs.Values; } }
    }
    [Serializable] public sealed class RecipeEntry { public string id, name_key, cup_size, steps, comment; public int unlock_rep, price; public bool needs_latte_art; }
    [Serializable] public sealed class IngredientEntry { public string id, name_key, comment; public int max_stock, refill_cost, unlock_rep; }
    [Serializable] public sealed class DeviceEntry { public string id, name_key, comment; public float base_time, upgraded_time; public int upgrade_cost, unlock_rep; }
    [Serializable] public sealed class GameParamEntry { public string key, value, comment; }
    [Serializable] public sealed class RecipeStepEntry { public string id, recipe_id, operation, target_id, device_id; public int sequence, amount; }
    [Serializable] public sealed class NpcEntry { public string id, name, sprite_path, dialogue; public int social_id, unlock_rep; }
}
