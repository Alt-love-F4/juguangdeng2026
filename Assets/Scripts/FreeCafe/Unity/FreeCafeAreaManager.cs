using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    public enum CafeFloor { First, Second, Third }

    [Serializable]
    public sealed class CafeAreaAnchor
    {
        public AreaId area;
        public CafeFloor floor;
        public Transform contentRoot;
        public Transform cameraAnchor;
        public float orthographicSize; // 0 保持场景原始镜头，仅制作区使用近景
        [Tooltip("嵌套子场景：聚焦时区域根移动到镜头前（父场景原点），摄像头不平移")]
        public bool dockToView;
        public FreeCafeSubSceneDock dock;
    }

    /// <summary>Camera destinations within ONE Unity scene. Does not own gameplay, load scenes or crop the camera.</summary>
    public sealed class FreeCafeAreaManager : MonoBehaviour
    {
        public Camera worldCamera;
        public Vector3 cameraOffset=new Vector3(0,0,-10);
        public CafeAreaAnchor[] areas;
        [Tooltip("区域切换时镜头平滑平移的时间（秒）；0 表示瞬间切换。读档与首次聚焦始终瞬间完成")]
        public float panSmoothTime=.35f;
        public AreaId CurrentArea { get; private set; }
        public bool IsPanning { get; private set; }
        private bool hasFocus;
        private float originalOrthographicSize;
        private Vector3 panTarget,panVelocity;
        private float orthoTarget,orthoVelocity;

        public CafeAreaAnchor Find(AreaId area)
        {
            if(areas!=null) foreach(var entry in areas) if(entry!=null && entry.area==area) return entry;
            return null;
        }
        public void Initialize(Camera camera)
        {
            if(camera==null) throw new InvalidOperationException("Area manager requires the gameplay camera");
            worldCamera=camera; originalOrthographicSize=camera.orthographicSize;
            if(areas==null || areas.Length==0) throw new InvalidOperationException("Area manager has no anchors");
            for(int i=0;i<areas.Length;i++)
            {
                var entry=areas[i];
                if(entry==null || entry.contentRoot==null || entry.cameraAnchor==null) throw new InvalidOperationException("Area anchor is incomplete");
                for(int j=0;j<i;j++) if(areas[j].area==entry.area) throw new InvalidOperationException("Duplicate area anchor: "+entry.area);
                // Areas stay alive: changing the camera never resets devices, NPCs or animations.
                entry.contentRoot.gameObject.SetActive(true);
            }
            hasFocus=false;
        }
        public bool Focus(AreaId area,bool force=false)
        {
            var entry=Find(area);if(entry==null || worldCamera==null) return false;
            if(!force && hasFocus && CurrentArea==area) return true;
            // 首次聚焦（开局/读档）瞬间就位；之后的区域切换平滑平移，呈现子场景被推开的效果
            bool snap=!hasFocus;
            CurrentArea=area;hasFocus=true;
            // 嵌套子场景（工作台）：根停靠到镜头前，其余子场景归位
            foreach(var e in areas) if(e!=null && e.dock!=null) e.dock.SetDocked(e==entry && e.dockToView,snap);
            if(entry.dockToView)
            {
                // 子场景停靠在父场景原点，镜头对准父场景视野；从高楼层回来时平滑落回
                var viewRoot=entry.contentRoot.parent!=null ? entry.contentRoot.parent : entry.contentRoot;
                SetTarget(viewRoot.position+cameraOffset,entry.orthographicSize>0 ? entry.orthographicSize : originalOrthographicSize,snap);
                return true;
            }
            SetTarget(entry.cameraAnchor.position+cameraOffset,entry.orthographicSize>0 ? entry.orthographicSize : originalOrthographicSize,snap);
            return true;
        }
        /// <summary>立即完成当前平移；读档等需要瞬间就位的场合使用。</summary>
        public void SnapNow()
        {
            if(worldCamera==null) return;
            worldCamera.transform.position=panTarget;
            if(worldCamera.orthographic) worldCamera.orthographicSize=orthoTarget;
            panVelocity=Vector3.zero;orthoVelocity=0;IsPanning=false;
        }
        private void SetTarget(Vector3 position,float ortho,bool snap)
        {
            panTarget=position;orthoTarget=ortho;
            if(snap || panSmoothTime<=0) { SnapNow(); return; }
            panVelocity=Vector3.zero;orthoVelocity=0;IsPanning=true;
        }
        private void Update()
        {
            if(worldCamera==null || !IsPanning) return;
            var t=worldCamera.transform;
            t.position=Vector3.SmoothDamp(t.position,panTarget,ref panVelocity,panSmoothTime);
            if(worldCamera.orthographic) worldCamera.orthographicSize=Mathf.SmoothDamp(worldCamera.orthographicSize,orthoTarget,ref orthoVelocity,panSmoothTime);
            if((t.position-panTarget).sqrMagnitude<.0004f && (!worldCamera.orthographic || Mathf.Abs(worldCamera.orthographicSize-orthoTarget)<.01f)) SnapNow();
        }
        private void OnDrawGizmosSelected()
        {
            if(areas==null) return;
            Gizmos.color=new Color(.3f,.9f,1);
            foreach(var entry in areas) if(entry!=null && entry.cameraAnchor!=null)
            {
                Gizmos.DrawWireSphere(entry.cameraAnchor.position,.5f);
                Gizmos.DrawLine(entry.cameraAnchor.position,entry.cameraAnchor.position+cameraOffset);
            }
        }
    }
}
