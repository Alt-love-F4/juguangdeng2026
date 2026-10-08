using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    public enum CafeFloor { Basement, First, Second }

    [Serializable]
    public sealed class CafeAreaAnchor
    {
        public AreaId area;
        public CafeFloor floor;
        public Transform contentRoot;
        public Transform cameraAnchor;
        public float orthographicSize; // 0 保持场景原始镜头，仅制作区使用近景
    }

    /// <summary>Camera destinations within ONE Unity scene. Does not own gameplay, load scenes or crop the camera.</summary>
    public sealed class FreeCafeAreaManager : MonoBehaviour
    {
        public Camera worldCamera;
        public Vector3 cameraOffset=new Vector3(0,0,-10);
        public CafeAreaAnchor[] areas;
        public AreaId CurrentArea { get; private set; }
        private bool hasFocus;
        private float originalOrthographicSize;

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
            worldCamera.transform.position=entry.cameraAnchor.position+cameraOffset;
            if(worldCamera.orthographic) worldCamera.orthographicSize=entry.orthographicSize>0 ? entry.orthographicSize : originalOrthographicSize;
            CurrentArea=area;hasFocus=true;
            return true;
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
