using UnityEngine;
namespace Spotlight.FreeCafe
{
    [ExecuteAlways,RequireComponent(typeof(Camera))]
    public sealed class FreeCafeViewport : MonoBehaviour
    {
        public float topMargin=180f/1080f,bottomMargin=180f/1080f,worldAspect=1.8f;
        private Camera cameraComponent;
        private void OnEnable() { cameraComponent=GetComponent<Camera>(); Apply(); }
        private void LateUpdate()=>Apply();
        public void Apply()
        {
            if(cameraComponent==null) cameraComponent=GetComponent<Camera>();
            float available=1-topMargin-bottomMargin;
            float width=available*Mathf.Max(1,Screen.height)*worldAspect/Mathf.Max(1,Screen.width);
            float height=available;
            if(width>1) { width=1;height=(float)Mathf.Max(1,Screen.width)/Mathf.Max(1,Screen.height)/worldAspect; }
            var rect=new Rect((1-width)*.5f,bottomMargin+(available-height)*.5f,width,height);
            if(cameraComponent.rect!=rect) cameraComponent.rect=rect;
        }
    }
}
