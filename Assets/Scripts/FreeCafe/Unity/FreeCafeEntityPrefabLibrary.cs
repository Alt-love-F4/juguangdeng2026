using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    [Serializable]
    public sealed class FreeCafeEntityPrefabEntry
    {
        public string specId;
        public FreeEntityView prefab;
    }

    /// <summary>每种实体规格对应独立 Prefab；缺失映射明确失败，不静默回退。</summary>
    [CreateAssetMenu(menuName="Free Cafe/Entity Prefab Library")]
    public sealed class FreeCafeEntityPrefabLibrary : ScriptableObject
    {
        public FreeCafeEntityPrefabEntry[] entries;

        public FreeEntityView Require(string specId)
        {
            var prefab=Resolve(specId);
            if(prefab==null) throw new InvalidOperationException("缺少实体 Prefab："+specId+"；运行 Tools > Free Cafe > Rebuild Entity Prefabs And Layout");
            return prefab;
        }
        public void Validate(CafeCatalog catalog)
        {
            if(catalog==null || entries==null) throw new InvalidOperationException("实体目录或 Prefab 注册表缺失");
            var ids=new System.Collections.Generic.HashSet<string>();
            var prefabs=new System.Collections.Generic.HashSet<FreeEntityView>();
            foreach(var entry in entries)
            {
                if(entry==null || string.IsNullOrEmpty(entry.specId) || entry.prefab==null || !ids.Add(entry.specId))
                    throw new InvalidOperationException("无效/重复实体 Prefab 映射");
                if(!prefabs.Add(entry.prefab)) throw new InvalidOperationException("不同规格不可共用同一个 Prefab："+entry.specId);
            }
            foreach(var spec in catalog.entities)
            {
                var prefab=Require(spec.id);
                if(prefab.PrefabSpecId!=spec.id || prefab.body==null || prefab.label==null || prefab.hitbox==null)
                    throw new InvalidOperationException("实体 Prefab 规格/组件不完整："+spec.id);
                if(spec.Has(Capability.Customer) && prefab.GetComponent<FreeCafePersonView>()==null)
                    throw new InvalidOperationException("人物 Prefab 缺少人物结构："+spec.id);
                if(spec.Has(Capability.BusinessControl) && prefab.GetComponent<FreeCafeBusinessSignView>()==null)
                    throw new InvalidOperationException("营业入口缺少立牌结构："+spec.id);
            }
        }
        public FreeEntityView Resolve(string specId)
        {
            if (entries==null) return null;
            foreach (var entry in entries)
                if (entry!=null && entry.specId==specId && entry.prefab!=null) return entry.prefab;
            return null;
        }
    }
}
