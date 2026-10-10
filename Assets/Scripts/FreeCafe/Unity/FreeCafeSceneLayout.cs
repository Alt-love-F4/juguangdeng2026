using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>只在启动时读取场景实体布局。配表定义规格，Scene 定义初始位置；读档后由快照权威驱动。</summary>
    public static class FreeCafeSceneLayout
    {
        public static List<FreeEntityView> FindAuthoredEntities(FreeCafeRuntime runtime)
        {
            var result = new List<FreeEntityView>();
            var ids = new HashSet<string>();
            if (runtime.areaRoots == null) throw new InvalidOperationException("缺少场景区域根");
            foreach (var area in runtime.areaRoots)
            {
                if (area == null) throw new InvalidOperationException("缺少场景区域根");
                foreach (var view in area.GetComponentsInChildren<FreeEntityView>(true))
                {
                    // 区域场景可相互嵌套（工作台嵌在大厅下）：实体归属最近的区域根
                    if (NearestAreaRoot(runtime, view.transform) != area) continue;
                    // 运行时新生成的杯子/顾客没有 authoring id，不属于初始布局。
                    if (string.IsNullOrEmpty(view.AuthoringEntityId))
                    {
                        if (view.Runtime == null) throw new InvalidOperationException("场景实体缺少 authoringEntityId：" + view.name);
                        continue;
                    }
                    if (!ids.Add(view.AuthoringEntityId)) throw new InvalidOperationException("重复场景实体 ID：" + view.AuthoringEntityId);
                    result.Add(view);
                }
            }
            return result;
        }

        /// <summary>沿父链向上找到包含该节点的最近区域根；区域可嵌套，越深优先级越高。</summary>
        public static Transform NearestAreaRoot(FreeCafeRuntime runtime, Transform target)
        {
            for (var t = target; t != null; t = t.parent)
                foreach (var root in runtime.areaRoots)
                    if (t == root) return t;
            return null;
        }

        public static InitialEntity[] Capture(FreeCafeRuntime runtime, IList<FreeEntityView> entities)
        {
            var layout = new InitialEntity[entities.Count];
            for (int n = 0; n < entities.Count; n++)
            {
                var view = entities[n];
                int areaIndex = -1;
                // 区域场景可相互嵌套（工作台嵌在大厅下）：按最近的区域根归属
                var owner = NearestAreaRoot(runtime, view.transform);
                for (int i = 0; i < runtime.areaRoots.Length; i++)
                    if (owner == runtime.areaRoots[i]) { areaIndex = i; break; }
                if (areaIndex < 0) throw new InvalidOperationException("实体不在区域根下：" + view.name);
                var position = runtime.areaRoots[areaIndex].InverseTransformPoint(view.transform.position);
                layout[n] = new InitialEntity
                {
                    id = view.AuthoringEntityId, specId = view.PrefabSpecId, area = (AreaId)areaIndex,
                    x = position.x, y = position.y
                };
            }
            return layout;
        }

        /// <summary>仅扩大当前世界副本的摆放范围，允许作者的布局超出旧 JSON 白盒范围；不移动物体/镜头。</summary>
        public static void FitAuthoredBounds(FreeCafeRuntime runtime, CafeCatalog catalog, IList<FreeEntityView> entities)
        {
            foreach (var entry in catalog.layout)
            {
                var area = Array.Find(catalog.areas, a => a.id == entry.area);
                var spec = Array.Find(catalog.entities, e => e.id == entry.specId);
                if (area == null || spec == null || !Finite(entry.x) || !Finite(entry.y))
                    throw new InvalidOperationException("场景布局规格/区域/坐标无效：" + entry.id);
                float extentX = spec.width * .5f, extentY = spec.height * .5f;
                // Prefab 或其编辑分组的缩放/旋转也要计入初始可摆放范围。
                FreeEntityView view = null;
                foreach (var candidate in entities) if (candidate.AuthoringEntityId == entry.id) { view = candidate; break; }
                if (view != null && view.hitbox != null)
                {
                    var box = view.hitbox; var half = box.size * .5f;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var local = box.offset + new Vector2((corner & 1) == 0 ? -half.x : half.x, (corner & 2) == 0 ? -half.y : half.y);
                        var point = runtime.areaRoots[(int)entry.area].InverseTransformPoint(box.transform.TransformPoint(local));
                        extentX = Mathf.Max(extentX, Mathf.Abs(point.x-entry.x));
                        extentY = Mathf.Max(extentY, Mathf.Abs(point.y-entry.y));
                    }
                }
                if (!Finite(extentX) || !Finite(extentY)) throw new InvalidOperationException("场景实体变换无效：" + entry.id);
                const float margin = .25f;
                area.halfWidth = Mathf.Max(area.halfWidth, Mathf.Abs(entry.x)+extentX+margin);
                area.halfHeight = Mathf.Max(area.halfHeight, Mathf.Abs(entry.y)+extentY+margin);
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static InitialEntity[] Capture(FreeCafeRuntime runtime) => Capture(runtime, FindAuthoredEntities(runtime));
    }
}
