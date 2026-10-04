using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Spotlight.Gameplay
{
    public enum RecipeStepKind { SelectCup, AddIngredient, GrindClick, MokaPot, MatchaGrind, CocoaMinigame, AddMilk, AddHoney, LatteArt, Serve, BeanSelection, Process }
    public sealed class RecipeStep
    {
        public RecipeStepKind Kind;
        public string IngredientId, DeviceId, TargetId;
        public int ClickCount, Amount = 1;
        public float DurationSec;
    }
    public static class RecipeStepParser
    {
        public static List<RecipeStep> ForRecipe(RecipeEntry recipe)
        {
            var rows = GameplayConfigService.GetSteps(recipe.id).ToArray();
            if (rows.Length == 0) return Parse(recipe.steps); // compatibility for legacy authors, not a second authority
            var result = new List<RecipeStep>();
            foreach (var row in rows)
            {
                if (!Enum.TryParse<RecipeStepKind>(row.operation, true, out var kind)) throw new FormatException("未知配方操作：" + row.operation);
                if (kind == RecipeStepKind.AddIngredient && GameplayConfigService.GetIngredient(row.target_id) == null) throw new FormatException("未知原料：" + row.target_id);
                if (!string.IsNullOrEmpty(row.device_id) && GameplayConfigService.GetDevice(row.device_id) == null) throw new FormatException("未知设备：" + row.device_id);
                result.Add(new RecipeStep { Kind = kind, IngredientId = row.target_id, TargetId = row.target_id, DeviceId = row.device_id,
                    Amount = Math.Max(1, row.amount), ClickCount = Math.Max(1, row.amount) });
            }
            return result;
        }
        public static List<RecipeStep> Parse(string steps)
        {
            var result = new List<RecipeStep>();
            if (string.IsNullOrWhiteSpace(steps)) throw new FormatException("配方不能为空");
            foreach (var raw in steps.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim();
                if (token.StartsWith("grind_click_")) { result.Add(new RecipeStep { Kind = RecipeStepKind.GrindClick, DeviceId = "grinder", ClickCount = (int)Number(token, "grind_click_") }); continue; }
                if (token.StartsWith("moka_pot_")) { result.Add(new RecipeStep { Kind = RecipeStepKind.MokaPot, DeviceId = "moka_pot", DurationSec = Number(token, "moka_pot_") }); continue; }
                if (token.StartsWith("matcha_grind_")) { result.Add(new RecipeStep { Kind = RecipeStepKind.MatchaGrind, DeviceId = "matcha_bowl", DurationSec = Number(token, "matcha_grind_") }); continue; }
                if (token.StartsWith("cocoa_minigame_")) { result.Add(new RecipeStep { Kind = RecipeStepKind.CocoaMinigame, DeviceId = "cocoa_pot", DurationSec = Number(token, "cocoa_minigame_") }); continue; }
                if (token.StartsWith("add_milk_")) { result.Add(new RecipeStep { Kind = RecipeStepKind.AddMilk, IngredientId = "milk", DeviceId = "milk_cup", DurationSec = Number(token, "add_milk_") }); continue; }
                var step = token switch
                {
                    "select_cup" => new RecipeStep { Kind = RecipeStepKind.SelectCup },
                    "add_coffee" => Ingredient("coffee_bean"), "add_matcha" => Ingredient("matcha"),
                    "add_chocolate" => Ingredient("chocolate"), "add_honey" => Ingredient("honey"),
                    "latte_art" => new RecipeStep { Kind = RecipeStepKind.LatteArt }, "serve" => new RecipeStep { Kind = RecipeStepKind.Serve },
                    _ => throw new FormatException("未知配方步骤：" + token)
                };
                result.Add(step);
            }
            return result;
        }
        private static RecipeStep Ingredient(string id) => new() { Kind = RecipeStepKind.AddIngredient, IngredientId = id };
        private static float Number(string token, string prefix)
        {
            if (!float.TryParse(token.Substring(prefix.Length).TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
                throw new FormatException("无效配方参数：" + token);
            return value;
        }
    }
}
