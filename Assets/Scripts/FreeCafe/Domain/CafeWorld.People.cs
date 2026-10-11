using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public enum CafePersonRole { Customer, Staff, Player }

    /// <summary>人物身份快照。personId 是人，不是座位号；narrativeCharacterId 仅为未来 NCS 适配的引用。</summary>
    [Serializable]
    public sealed class CafePersonIdentity
    {
        public string personId;
        public string characterId; // 静态角色配置 ID；旧档为空时仍使用原有外观回退。
        public string displayName;
        public string narrativeCharacterId;
        public string visualId = "customer";
        public CafePersonRole role;
    }

    /// <summary>可选身份入口；只能返回身份，不隐式推进剧情或结算 NCS 事件。</summary>
    public interface ICafeCustomerIdentitySource
    {
        CafePersonIdentity CreateIdentity(string visitId, int seatIndex);
    }

    public interface ICafeAvailableCustomerIdentitySource : ICafeCustomerIdentitySource
    {
        CafePersonIdentity CreateIdentity(string visitId, int seatIndex, ISet<string> occupiedPersonIds);
    }

    public sealed partial class CafeWorld
    {
        public ActionResult ActivateBusinessControl(string entityId)
        {
            var entity = Find(entityId);
            if (entity == null || !Spec(entity.specId).Has(Capability.BusinessControl))
                return ActionResult.No("这不是营业入口");
            if (entity.area != AreaId.Cafe || State.currentArea != entity.area)
                return ActionResult.No("请在一楼大厅点击营业立牌");
            return StartDay();
        }

        public string PersonName(WorldEntity person)
        {
            if (person == null) return "等待顾客";
            if (person.person != null && !string.IsNullOrEmpty(person.person.displayName)) return person.person.displayName;
            return "客人";
        }

        private static CafePersonIdentity AnonymousCustomer(string visitId)
        {
            return new CafePersonIdentity
            {
                personId = "visitor_" + visitId,
                displayName = "客人 " + visitId.Replace("order_", ""),
                visualId = "customer", role = CafePersonRole.Customer
            };
        }

        private static bool ValidCustomerIdentity(CafePersonIdentity identity) => identity != null
            && !string.IsNullOrWhiteSpace(identity.personId) && !string.IsNullOrWhiteSpace(identity.displayName)
            && identity.role == CafePersonRole.Customer;

        private CafePersonIdentity CreateCustomerIdentity(string visitId, int seatIndex)
        {
            CafePersonIdentity supplied;
            if (customerIdentities is ICafeAvailableCustomerIdentitySource available)
            {
                var occupied = new HashSet<string>();
                foreach (var entity in State.entities)
                    if (entity.person != null) occupied.Add(entity.person.personId);
                supplied = available.CreateIdentity(visitId, seatIndex, occupied);
            }
            else supplied = customerIdentities?.CreateIdentity(visitId, seatIndex);
            var identity = supplied ?? AnonymousCustomer(visitId);
            if (!ValidCustomerIdentity(identity)) throw new InvalidOperationException("顾客身份适配器返回了无效身份");
            foreach (var existing in State.entities)
                if (existing.person != null && existing.person.personId == identity.personId)
                    throw new InvalidOperationException("同一人物不能同时占用两个客人实体：" + identity.personId);
            // 不将 Provider 中共享的可变数据直接写入存档。
            return new CafePersonIdentity
            {
                personId = identity.personId, characterId = identity.characterId, displayName = identity.displayName, role = identity.role,
                narrativeCharacterId = identity.narrativeCharacterId, visualId = identity.visualId
            };
        }

        private ActionResult RestorePeopleAndBusinessControls(CafeSnapshot candidate, Dictionary<string, WorldEntity> ids)
        {
            var personIds = new HashSet<string>();
            foreach (var entity in candidate.entities)
            {
                if (!Spec(entity.specId).Has(Capability.Customer))
                {
                    if (entity.person != null && !string.IsNullOrEmpty(entity.person.personId))
                        return ActionResult.No("非人物实体存在人物身份");
                    entity.person = null;
                    continue;
                }
                // 兼容旧档，也兼容 Unity 将 null 内联对象展开为空对象的情况。
                if (entity.person == null || (string.IsNullOrEmpty(entity.person.personId)
                    && string.IsNullOrEmpty(entity.person.displayName) && string.IsNullOrEmpty(entity.person.narrativeCharacterId)))
                {
                    string visit = entity.id;
                    foreach (var request in candidate.requests)
                        if (request.customerId == entity.id) { visit = request.id; break; }
                    entity.person = AnonymousCustomer(visit);
                }
                if (!ValidCustomerIdentity(entity.person) || !personIds.Add(entity.person.personId)) return ActionResult.No("存档人物身份无效：" + entity.id);
            }
            // 旧档没有新增营业立牌。仅补齐这种固定入口，不重置设备、库存、位置或订单。
            foreach (var initial in Catalog.layout)
            {
                if (!Spec(initial.specId).Has(Capability.BusinessControl)) continue;
                if (ids.TryGetValue(initial.id, out var existing))
                {
                    if (existing.specId != initial.specId || existing.area != initial.area)
                        return ActionResult.No("存档营业入口身份冲突");
                }
                else candidate.entities.Add(new WorldEntity
                {
                    id = initial.id, specId = initial.specId, area = initial.area, x = initial.x, y = initial.y
                });
            }
            return ActionResult.Yes("人物身份与营业入口已恢复");
        }
    }
}
