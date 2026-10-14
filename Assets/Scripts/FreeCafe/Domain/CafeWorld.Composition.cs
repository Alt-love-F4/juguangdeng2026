using System;
using System.Collections.Generic;
using System.Text;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public IngredientRuleSpec IngredientDefinition(string id)
        {
            if(Catalog.ingredientRules!=null)foreach(var rule in Catalog.ingredientRules)if(rule.id==id)return rule;
            return null;
        }
        public string CoffeeKind(Portion portion)
        {
            if(portion==null || portion.specId!="coffee")return null;
            if(portion.coffeeKind=="essence")return "essence";
            return TemperatureBand(TemperatureCelsius(portion))==LiquidTemperature.Room ? "cold_brew" : "black";
        }
        public string IngredientLabel(Portion portion)
        {
            if(portion==null)return "未知成分";
            string kind=CoffeeKind(portion);
            if(kind!=null)
            {
                float water=CoffeeWaterMl(portion);
                float strength=water>0 ? CoffeeGroundsGrams(portion)/water : 0;
                var settings=Catalog.compositionSettings;
                string concentration=strength>=settings.strongCoffeeStrength ? "浓" : strength>=settings.mediumCoffeeStrength ? "中度" : "淡";
                return concentration+(kind=="essence" ? "精粹咖啡液" : kind=="black" ? "黑咖啡液" : "冷萃咖啡液");
            }
            return IngredientDefinition(CanonicalIngredient(portion.specId))?.label ?? Spec(portion.specId)?.label ?? portion.specId;
        }
        public string IngredientIdentity(Portion portion) => CanonicalIngredient(portion.specId)+":"+(CoffeeKind(portion)??"");
        private static string CanonicalIngredient(string id) => id=="hotwater" ? "water" : id=="hotmilk" ? "milk" : id;
        private bool SamePortionKind(Portion a,Portion b) => SameIngredient(a.specId,b.specId) && CoffeeKind(a)==CoffeeKind(b)
            && (a.latteArtSessionToken ?? "") == (b.latteArtSessionToken ?? "");
        private float DoseGrams(string id) => IngredientDefinition(id)?.gramsPerUnit ?? (id=="sugar" ? 5 : 12);
        private float DoseMl(string id) => IngredientDefinition(id)?.millilitersPerUnit ?? Prep.portionMl;
        private void ApplyCompositionRules(WorldEntity entity)
        {
            if(Catalog.compositionRules==null || IsExtractionFilter(entity))return;
            foreach(var portion in entity.contents)
                if(portion.specId=="coffee")
                {
                    EnsureCoffeeProvenance(portion);
                    portion.coffeeKind=CoffeeKind(portion);
                }
            // Rules run in declared order, repeatedly until stable. Bounded cycles are validated at authoring.
            for(int pass=0;pass<8;pass++)
            {
                bool changed=false;
                foreach(var rule in Catalog.compositionRules)
                {
                    if(rule.precisionOnly && !IsPrecisionExtractor(entity) && !IsExtractionFilter(entity))continue;
                    var catalyst=string.IsNullOrEmpty(rule.catalystId) ? null : entity.contents.Find(p=>p.specId==rule.catalystId && (p.grams>0||p.units>0));
                    if(!string.IsNullOrEmpty(rule.catalystId) && catalyst==null)continue;
                    bool reacted=false;
                    foreach(var p in entity.contents)
                    {
                        if(p.specId!=rule.inputId || p.milliliters<=0 || (int)TemperatureBand(TemperatureCelsius(p))<rule.minimumTemperature || (int)TemperatureBand(TemperatureCelsius(p))>rule.maximumTemperature)continue;
                        if(!string.IsNullOrEmpty(rule.inputCoffeeKind) && CoffeeKind(p)!=rule.inputCoffeeKind)continue;
                        if(p.specId==rule.outputId && p.coffeeKind==rule.outputCoffeeKind)continue;
                        if(p.specId!=rule.outputId)
                        {
                            AddConstituent(p,CanonicalIngredient(p.specId),p.grams,p.milliliters);
                            p.grams=0;
                        }
                        p.specId=rule.outputId;p.coffeeKind=string.IsNullOrEmpty(rule.outputCoffeeKind)?null:rule.outputCoffeeKind;
                        p.extracted=p.coffeeKind=="black"||p.coffeeKind=="essence";
                        changed=true;reacted=true;
                    }
                    // Catalysts remain real ingredients unless the rule explicitly consumes them.
                    // Consumed mass is retained on the generated liquid, never silently discarded.
                    if(reacted && rule.consumeCatalyst && catalyst!=null)
                    {
                        float outputVolume=0;foreach(var p in entity.contents)if(p.specId==rule.outputId)outputVolume+=p.milliliters;
                        if(outputVolume>0)
                        {
                            float grams=catalyst.grams>0 ? catalyst.grams : catalyst.units*DoseGrams(catalyst.specId);
                            foreach(var p in entity.contents)if(p.specId==rule.outputId)
                                AddConstituent(p,catalyst.specId,grams*p.milliliters/outputVolume,0);
                            entity.contents.Remove(catalyst);
                        }
                    }
                }
                if(!changed)break;
            }
        }
        private bool IsReactionResidue(Portion p,WorldEntity container)
        {
            if(p.specId=="grounds")return container.contents.Exists(x=>x.specId=="coffee" && x.milliliters>0);
            return (p.specId=="cocoa.powder" && container.contents.Exists(x=>x.specId=="cocoa.drink" && x.milliliters>0)) ||
                (p.specId=="matcha.powder" && container.contents.Exists(x=>x.specId=="matcha.drink" && x.milliliters>0));
        }
        public string CompositionName(WorldEntity entity) => ClassifyCoffee(entity).label;

        // Additional water dilutes an existing liquid, but does not discard powder provenance.
        private void DiluteCoffeeWater(WorldEntity entity)
        {
            var coffees=entity.contents.FindAll(p=>p.specId=="coffee" && p.milliliters>0);
            if(coffees.Count==0)return;
            float coffeeVolume=0;foreach(var p in coffees){EnsureCoffeeProvenance(p);coffeeVolume+=p.milliliters;}
            for(int i=entity.contents.Count-1;i>=0;i--)
            {
                var water=entity.contents[i];if((water.specId!="water" && water.specId!="hotwater")||water.milliliters<=0)continue;
                foreach(var p in coffees)
                {
                    float extra=water.milliliters*p.milliliters/coffeeVolume;
                    float total=p.milliliters+extra;
                    SetTemperature(p,(TemperatureCelsius(p)*p.milliliters+TemperatureCelsius(water)*extra)/total);
                    AddConstituent(p,"water",0,extra);p.milliliters=total;
                }
                entity.contents.RemoveAt(i);
                coffeeVolume+=water.milliliters;
            }
            foreach(var p in coffees)if(p.coffeeKind!="essence"){p.coffeeKind=CoffeeKind(p);p.extracted=p.coffeeKind=="black";}
        }
        private void ValidateCompositionCatalog()
        {
            Catalog.compositionSettings=Catalog.compositionSettings ?? new CoffeeCompositionSettings();
            var settings=Catalog.compositionSettings;
            if(!Finite(settings.defaultCoffeeStrength)||settings.defaultCoffeeStrength<=0||!Finite(settings.strongCoffeeStrength)||!Finite(settings.mediumCoffeeStrength)||settings.mediumCoffeeStrength<=0||settings.strongCoffeeStrength<settings.mediumCoffeeStrength)throw new ArgumentException("Invalid coffee concentration settings");
            if(Catalog.products!=null)foreach(var product in Catalog.products)
                if(product.evaluation!=null)
                {
                    if(!ValidProductEvaluation(product.evaluation))throw new ArgumentException("Invalid evaluation "+product.id);
                    foreach(var amount in product.evaluation.amounts)if(Spec(amount.ingredientId)==null)throw new ArgumentException("Unknown product ingredient "+amount.ingredientId);
                }
            if(Catalog.ingredientRules==null)return;var ids=new HashSet<string>();
            foreach(var r in Catalog.ingredientRules)
                if(r==null||Spec(r.id)==null||!ids.Add(r.id)||string.IsNullOrEmpty(r.label)||!Finite(r.gramsPerUnit)||r.gramsPerUnit<0||!Finite(r.millilitersPerUnit)||r.millilitersPerUnit<0)
                    throw new ArgumentException("Invalid ingredient rule");
            ids.Clear();if(Catalog.compositionRules!=null)foreach(var r in Catalog.compositionRules)
                if(r==null||!ids.Add(r.id)||Spec(r.inputId)==null||Spec(r.outputId)==null||(!string.IsNullOrEmpty(r.catalystId)&&Spec(r.catalystId)==null)||r.minimumTemperature<0||r.maximumTemperature>2||r.minimumTemperature>r.maximumTemperature)
                    throw new ArgumentException("Invalid composition rule");
            if(Catalog.drinkNameRules!=null)foreach(var r in Catalog.drinkNameRules)
            {
                if(r==null||string.IsNullOrEmpty(r.label)||r.requiredIngredients==null||r.requiredIngredients.Length==0||r.allowedIngredients==null)throw new ArgumentException("Invalid drink name rule");
                foreach(var id in r.requiredIngredients)if(Spec(id)==null)throw new ArgumentException("Unknown naming ingredient "+id);
                foreach(var id in r.allowedIngredients)if(Spec(id)==null)throw new ArgumentException("Unknown naming ingredient "+id);
            }
        }
    }
}
