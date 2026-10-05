using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>Authoritative simulation. Spatial commands do not depend on the focused order or GameObjects.</summary>
    public sealed partial class CafeWorld
    {
        public CafeCatalog Catalog { get; }
        public CafeSnapshot State { get; private set; }
        public event Action Changed;
        private readonly Dictionary<string,EntitySpec> specs=new Dictionary<string,EntitySpec>();
        private readonly Dictionary<string,OperationSpec> operations=new Dictionary<string,OperationSpec>();
        private readonly Dictionary<string,ProductSpec> products=new Dictionary<string,ProductSpec>();
        private readonly Dictionary<string,RecipeSpec> recipes=new Dictionary<string,RecipeSpec>();
        public CafeWorld(CafeCatalog catalog)
        {
            Catalog=catalog ?? throw new ArgumentNullException(nameof(catalog));
            ValidateCatalog();
            State=new CafeSnapshot { money=catalog.startingMoney,timeRemaining=catalog.daySeconds,arrivalRemaining=catalog.customerInterval,seats=catalog.economy.initialSeats };
            foreach(var initial in catalog.layout)
            {
                var spec=Spec(initial.specId);
                if(Find(initial.id)!=null || !CanPosition(initial.specId,initial.area,initial.x,initial.y).success) throw new ArgumentException("Invalid layout "+initial.id);
                State.entities.Add(new WorldEntity {id=initial.id,specId=initial.specId,area=initial.area,x=initial.x,y=initial.y,stock=spec.initialStock});
            }
            UnlockContent();
        }
        private void ValidateCatalog()
        {
            if(Catalog.entities==null || Catalog.operations==null || Catalog.products==null || Catalog.layout==null || Catalog.areas==null || Catalog.economy==null) throw new ArgumentException("Catalog structure missing");
            if(!Finite(Catalog.daySeconds) || Catalog.daySeconds<=0 || Catalog.customerPatience<=0 || Catalog.customerInterval<=0 || Catalog.startingMoney<0) throw new ArgumentException("Invalid business timing");
            foreach(var spec in Catalog.entities)
            {
                if(spec==null || string.IsNullOrEmpty(spec.id) || spec.width<=0 || spec.height<=0 || !Finite(spec.width) || !Finite(spec.height) || spec.capacity<0 || spec.initialStock<0 || spec.refillCost<0 || spec.unitCost<0) throw new ArgumentException("Invalid entity specification");
                specs.Add(spec.id,spec);
            }
            foreach(var spec in Catalog.entities) if(spec.Has(Capability.Source) && Spec(spec.sourceItemId)==null) throw new ArgumentException("Invalid source "+spec.id);
            foreach(var op in Catalog.operations)
            {
                if(op==null || Spec(op.machineSpecId)==null || !Spec(op.machineSpecId).Has(Capability.Processor) || Spec(op.inputSpecId)==null || Spec(op.outputSpecId)==null || !Finite(op.seconds) || op.seconds<=0 || op.clicks<=0 || op.clickInterval<0) throw new ArgumentException("Invalid operation");
                foreach(var existing in operations.Values) if(existing.machineSpecId==op.machineSpecId && existing.inputSpecId==op.inputSpecId) throw new ArgumentException("Ambiguous operation "+op.id);
                operations.Add(op.id,op);
            }
            foreach(var product in Catalog.products)
            {
                if(product==null || Spec(product.cupSpecId)==null || product.price<0 || product.ingredients==null || product.ingredients.Length==0) throw new ArgumentException("Invalid product");
                int units=0;var ingredients=new HashSet<string>();
                foreach(var p in product.ingredients){if(p==null || Spec(p.specId)==null || p.units<=0 || !ingredients.Add(p.specId)) throw new ArgumentException("Invalid recipe "+product.id); units+=p.units;}
                if(units>Spec(product.cupSpecId).capacity) throw new ArgumentException("Recipe exceeds cup capacity "+product.id);
                products.Add(product.id,product);
            }
            if(Catalog.recipes!=null)
            {
                foreach(var recipe in Catalog.recipes)
                {
                    if(recipe==null || string.IsNullOrEmpty(recipe.id) || Product(recipe.productId)==null || recipe.steps==null || recipe.steps.Length==0) throw new ArgumentException("Invalid recipe steps");
                    if(recipes.ContainsKey(recipe.id)) throw new ArgumentException("Duplicate recipe "+recipe.id);
                    foreach(var step in recipe.steps) if(step==null || string.IsNullOrEmpty(step.kind) || string.IsNullOrEmpty(step.label)) throw new ArgumentException("Invalid recipe step "+recipe.id);
                    recipes.Add(recipe.id,recipe);
                }
            }
            var areas=new HashSet<AreaId>();foreach(var area in Catalog.areas) if(area==null || !areas.Add(area.id) || area.halfWidth<=0 || area.halfHeight<=0) throw new ArgumentException("Invalid area");
            if(Catalog.economy.beanUnitPrices==null || Catalog.economy.beanUnitPrices.Length!=3) throw new ArgumentException("Three bean price tiers required");
        }
        public EntitySpec Spec(string id) => id!=null && specs.TryGetValue(id,out var spec) ? spec : null;
        public ProductSpec Product(string id) => id!=null && products.TryGetValue(id,out var product) ? product : null;
        public RecipeSpec Recipe(string id) => id!=null && recipes.TryGetValue(id,out var recipe) ? recipe : null;
        public RecipeSpec RecipeFor(ProductSpec product)
        {
            if(product==null) return null;
            return Recipe(string.IsNullOrEmpty(product.recipeId) ? product.id : product.recipeId);
        }
        public WorldEntity Find(string id) => id==null ? null : State.entities.Find(e=>e.id==id);
        public CustomerRequest Request(string id) => id==null ? null : State.requests.Find(r=>r.id==id);
        public AreaSpec Area(AreaId id) { foreach(var area in Catalog.areas) if(area.id==id) return area;return null; }
        public int TotalUnits(WorldEntity entity){int sum=0;if(entity?.contents!=null) foreach(var p in entity.contents) sum+=p.units;return sum;}
        public ActionResult CanPosition(string specId,AreaId area,float x,float y)
        {
            var bounds=Area(area);var spec=Spec(specId);
            if(bounds==null || spec==null || !Finite(x) || !Finite(y)) return ActionResult.No("无效位置");
            if(Math.Abs(x)>bounds.halfWidth-spec.width*.5f || Math.Abs(y)>bounds.halfHeight-spec.height*.5f) return ActionResult.No("请放在画面内；上下黑边属于 UI");
            return ActionResult.Yes("放在这里");
        }
        public bool Locked(WorldEntity item) => item!=null && !string.IsNullOrEmpty(item.ownerId);
        public ActionResult Take(string sourceId,AreaId area,float x,float y,out WorldEntity item)
        {
            item=null;var source=Find(sourceId);var spec=source==null ? null : Spec(source.specId);
            if(spec==null || !spec.Has(Capability.Source)) return ActionResult.No("不是物品来源");
            if(source.area!=area) return ActionResult.No("请到物品所在区域取出");
            if(source.stock<=0) return ActionResult.No("库存不足；点击补货或从经营菜单购买原料");
            var valid=CanPosition(spec.sourceItemId,area,x,y);if(!valid.success) return valid;
            item=new WorldEntity {id=Allocate("free_"),specId=spec.sourceItemId,area=area,x=x,y=y,quality=source.quality};
            source.stock--;State.entities.Add(item);Notify();return ActionResult.Yes("取出："+Spec(item.specId).label);
        }
        public ActionResult Move(string itemId,AreaId area,float x,float y)
        {
            var item=Find(itemId);if(item==null || !Spec(item.specId).Has(Capability.Movable)) return ActionResult.No("固定设备或来源不能搬动");
            if(Locked(item)) return ActionResult.No("材料留在设备内；请接取产物或取消加工");
            var valid=CanPosition(item.specId,area,x,y);if(!valid.success) return valid;
            item.area=area;item.x=x;item.y=y;Notify();return ActionResult.Yes("已自由放置");
        }
        public void Rotate(string id){var item=Find(id);if(item!=null && Spec(item.specId).Has(Capability.Movable) && !Locked(item)){item.rotation=(item.rotation+15)%360;Notify();}}
        public void Focus(string id){if(Request(id)!=null){State.focusedRequestId=id;Notify();}}
        public void SetArea(AreaId area){if(Area(area)!=null){State.currentArea=area;Notify();}}
        public bool IsReceiver(WorldEntity target)=>target!=null && (Spec(target.specId).capabilities & (Capability.Container|Capability.Processor|Capability.Customer|Capability.Delivery|Capability.Discard))!=0;
        public ActionResult Preview(string itemId,string targetId)=>Preview(Find(itemId),Find(targetId));
        public ActionResult Preview(WorldEntity item,WorldEntity target)
        {
            if(item==null || target==null || item==target || item.area!=target.area) return ActionResult.No("目标不在同一区域");
            if(Locked(item) || Locked(target)) return ActionResult.No("材料正在设备内");
            var input=Spec(item.specId);var receiver=Spec(target.specId);
            if(input==null || receiver==null) return ActionResult.No("无效物品");
            if(receiver.Has(Capability.Discard)) return ActionResult.Yes("丢弃 · 扣除 "+Catalog.economy.wastePenalty+" 金币");
            if(receiver.Has(Capability.Processor)) return PreviewMachine(item,target);
            if(receiver.Has(Capability.Container))
            {
                if(input.Has(Capability.Portion)) return TotalUnits(target)+item.units>receiver.capacity ? ActionResult.No("容器容量不足") : ActionResult.Yes("加入 "+input.label);
                if(input.Has(Capability.Container) && TotalUnits(item)>0) return TotalUnits(target)+TotalUnits(item)>receiver.capacity ? ActionResult.No("目标杯子容量不足") : ActionResult.Yes("把内容物倒入 "+receiver.label+"（保留两个容器）");
            }
            if((receiver.Has(Capability.Customer)||receiver.Has(Capability.Delivery)) && input.Has(Capability.Container))
            {
                if(!State.open) return ActionResult.No("先开始营业再交付");var request=RequestForTarget(target);
                if(request==null) return ActionResult.No("没有待交付的订单");
                if(TotalUnits(item)==0) return ActionResult.No("空杯不能交付");
                var product=Product(request.productId);return ActionResult.Yes("确认上餐："+product.label+(Matches(item,product) ? " · 配方正确" : " · 配方不符，交付会失败并扣款"));
            }
            return ActionResult.No("不支持这种操作；Shift 只摆放");
        }
        public ActionResult Interact(string itemId,string targetId)
        {
            var item=Find(itemId);var target=Find(targetId);var preview=Preview(item,target);if(!preview.success) return preview;
            var receiver=Spec(target.specId);
            if(receiver.Has(Capability.Processor)) return InteractMachine(item,target);
            if(receiver.Has(Capability.Discard)){Charge(Catalog.economy.wastePenalty);State.entities.Remove(item);Notify();return ActionResult.Yes("已丢弃 "+Spec(item.specId).label+"，扣除 "+Catalog.economy.wastePenalty+" 金币");}
            if(receiver.Has(Capability.Container))
            {
                if(Spec(item.specId).Has(Capability.Portion)){AddPortion(target,new Portion{specId=item.specId,units=item.units,quality=item.quality});State.entities.Remove(item);}
                else {foreach(var p in item.contents) AddPortion(target,p);target.latteArt=false;item.contents.Clear();item.latteArt=false;}
                Notify();return preview;
            }
            return Serve(item,target);
        }
        public ActionResult TakeAndInteract(string sourceId,string targetId,AreaId area,float x,float y)
        {
            var source=Find(sourceId);if(source==null || !Spec(source.specId).Has(Capability.Source) || source.stock<=0 || source.area!=area) return ActionResult.No("来源无货或不在当前区域");
            var candidate=new WorldEntity{specId=Spec(source.specId).sourceItemId,area=area,quality=source.quality};
            var preview=Preview(candidate,Find(targetId));if(!preview.success) return preview;
            var valid=CanPosition(candidate.specId,area,x,y);if(!valid.success) return valid;
            Take(sourceId,area,x,y,out var item);return Interact(item.id,targetId);
        }
        private void AddPortion(WorldEntity container,Portion portion)
        {
            var existing=container.contents.Find(p=>p.specId==portion.specId && p.quality==portion.quality);
            if(existing==null) container.contents.Add(new Portion{specId=portion.specId,units=portion.units,quality=portion.quality});else existing.units+=portion.units;
            container.latteArt=false;
        }
        private CustomerRequest RequestForTarget(WorldEntity target)=>Spec(target.specId).Has(Capability.Customer) ? State.requests.Find(r=>r.customerId==target.id) : Request(State.focusedRequestId) ?? (State.requests.Count>0 ? State.requests[0] : null);
        private string Allocate(string prefix)
        {
            string id;do{id=prefix+State.nextId++;}while(Find(id)!=null || Request(id)!=null);return id;
        }
        private void Charge(int amount)=>State.money=Math.Max(0,State.money-Math.Max(0,amount));
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        private void Notify()=>Changed?.Invoke();
    }
}

