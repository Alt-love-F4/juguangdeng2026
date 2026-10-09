using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>动态实体使用场景的尺寸体系，不把编辑分组的缩放遗漏。仅视图规则，不改变配方/毫升/库存。</summary>
    public sealed class FreeCafeSpawnScale
    {
        private readonly Dictionary<AreaId,Dictionary<string,Vector3>> byArea=new Dictionary<AreaId,Dictionary<string,Vector3>>();
        private readonly Dictionary<string,Vector3> bySpec=new Dictionary<string,Vector3>();
        private readonly Dictionary<AreaId,Vector3> areaDefaults=new Dictionary<AreaId,Vector3>();

        public FreeCafeSpawnScale(FreeCafeRuntime runtime,IList<FreeEntityView> authored,CafeCatalog catalog)
        {
            for(int i=0;i<runtime.areaRoots.Length;i++)
            {
                var group=runtime.authoringPreviews!=null && i<runtime.authoringPreviews.Length ? runtime.authoringPreviews[i] : null;
                areaDefaults[(AreaId)i]=group!=null && group.transform.IsChildOf(runtime.areaRoots[i])
                    ? RelativeScale(runtime.areaRoots[i],group.transform) : Vector3.one;
                byArea[(AreaId)i]=new Dictionary<string,Vector3>();
            }
            // 实际存在的同规格实体优先，其次来源物品、设备产物，最后区域基准。
            foreach(var view in authored)
            {
                var entry=Array.Find(catalog.layout,e=>e.id==view.AuthoringEntityId);
                Seed(entry.area,entry.specId,Factor(runtime,view,entry.area));
            }
            foreach(var view in authored)
            {
                var entry=Array.Find(catalog.layout,e=>e.id==view.AuthoringEntityId);
                var spec=Array.Find(catalog.entities,e=>e.id==entry.specId);
                if(spec.Has(Capability.Source)) Seed(entry.area,spec.sourceItemId,Factor(runtime,view,entry.area));
            }
            foreach(var view in authored)
            {
                var entry=Array.Find(catalog.layout,e=>e.id==view.AuthoringEntityId);
                foreach(var operation in catalog.operations)
                    if(operation.machineSpecId==entry.specId) Seed(entry.area,operation.outputSpecId,Factor(runtime,view,entry.area));
            }
        }
        private void Seed(AreaId area,string specId,Vector3 factor)
        {
            if(string.IsNullOrEmpty(specId)) return;
            if(!byArea[area].ContainsKey(specId)) byArea[area].Add(specId,factor);
            if(!bySpec.ContainsKey(specId)) bySpec.Add(specId,factor);
        }
        public Vector3 Resolve(AreaId area,string specId,Vector3 prefabScale)
        {
            Vector3 factor;
            if(byArea.TryGetValue(area,out var map) && map.TryGetValue(specId,out factor)) return Vector3.Scale(prefabScale,factor);
            // 跨楼层携带与读档仍使用该规格原本的场景尺寸，不缩回 1 倍。
            if(bySpec.TryGetValue(specId,out factor)) return Vector3.Scale(prefabScale,factor);
            return Vector3.Scale(prefabScale,areaDefaults.TryGetValue(area,out factor) ? factor : Vector3.one);
        }
        private static Vector3 Factor(FreeCafeRuntime runtime,FreeEntityView view,AreaId area)
        {
            var scale=RelativeScale(runtime.areaRoots[(int)area],view.transform);
            var prefab=runtime.prefabLibrary.Require(view.PrefabSpecId).transform.localScale;
            return new Vector3(scale.x/Nonzero(prefab.x),scale.y/Nonzero(prefab.y),scale.z/Nonzero(prefab.z));
        }
        private static float Nonzero(float value)
        {
            value=Mathf.Abs(value);
            if(value<.00001f) throw new InvalidOperationException("实体 Prefab 根缩放不能为零");
            return value;
        }
        private static Vector3 RelativeScale(Transform area,Transform item) => new Vector3(
            area.InverseTransformVector(item.TransformVector(Vector3.right)).magnitude,
            area.InverseTransformVector(item.TransformVector(Vector3.up)).magnitude,
            area.InverseTransformVector(item.TransformVector(Vector3.forward)).magnitude);
    }
}
