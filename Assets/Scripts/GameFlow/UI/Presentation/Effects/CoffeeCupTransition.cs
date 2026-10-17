using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>A stationary, opaque safety backing and a separate coffee-cup iris above the stage.</summary>
    internal sealed class CoffeeCupTransition : IDisposable
    {
        private readonly Image backing;
        private readonly RawImage curtain;
        private Material material;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        public CoffeeCupTransition(RectTransform stage, Image effectOverlay)
        {
            backing = Create<Image>("CGBlackBacking", stage.parent);
            backing.color = Color.black;
            backing.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            backing.transform.SetSiblingIndex(stage.GetSiblingIndex());
            curtain = Create<RawImage>("CoffeeCupTransition", effectOverlay.transform.parent);
            curtain.transform.SetSiblingIndex(effectOverlay.transform.GetSiblingIndex());
            Initialize("Coffee cup transition (runtime)");
        }

        /// <summary>Creates a standalone curtain for transitions that outlive a UI panel.</summary>
        public CoffeeCupTransition(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            backing = Create<Image>("CGBlackBacking", parent);
            backing.color = Color.black;
            curtain = Create<RawImage>("CoffeeCupTransition", parent);
            Initialize("Coffee cup screen transition (runtime)");
        }

        private void Initialize(string materialName)
        {
            var shader = Resources.Load<Shader>("Dialogue/CoffeeCupIris");
            if (shader == null) throw new InvalidOperationException("Missing CoffeeCupIris shader.");
            material = new Material(shader) { name = materialName };
            curtain.material = material;
            // Keep the aperture independent of the panel CanvasGroup so exit fades
            // cannot dissolve the closing cup back into the scene behind.
            curtain.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            curtain.texture = Texture2D.whiteTexture;
            SetReveal(1f);
            backing.enabled = false;
        }

        private static T Create<T>(string name, Transform parent) where T : MaskableGraphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<T>();
            graphic.rectTransform.anchorMin = Vector2.zero;
            graphic.rectTransform.anchorMax = Vector2.one;
            graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
            graphic.raycastTarget = false;
            return graphic;
        }

        public void SetBacking(bool visible) => backing.enabled = visible;
        public void SetReveal(float reveal)
        {
            curtain.enabled = reveal < 1f;
            material.SetFloat(RevealId, Mathf.Clamp01(reveal));
            Rect rect = curtain.rectTransform.rect;
            material.SetFloat(AspectId, rect.height > 0f ? rect.width / rect.height : 16f / 9f);
        }
        public void Reset() => SetReveal(1f);
        public void Dispose()
        {
            UnityEngine.Object.Destroy(backing.gameObject);
            UnityEngine.Object.Destroy(curtain.gameObject);
            UnityEngine.Object.Destroy(material);
        }
    }

    /// <summary>
    /// Persistent full-screen handoff curtain for transitions between independently managed panels.
    /// The curtain is created lazily, so ordinary dialogue previews do not add a global UI object.
    /// </summary>
    internal static class CoffeeCupScreenTransition
    {
        private static GameObject host;
        private static CoffeeCupTransition curtain;
        private static Tween tween;
        private static bool pending;

        public static bool IsPending => pending;

        public static void BeginClose(float seconds)
        {
            EnsureCreated();
            KillTween();
            pending = true;
            curtain.SetBacking(false);
            curtain.SetReveal(1f);
            float elapsed = 0f;
            tween = DOTween.To(() => elapsed, value =>
            {
                elapsed = value;
                curtain.SetReveal(1f - Smooth(value));
            }, 1f, Mathf.Max(.05f, seconds)).SetUpdate(true);
        }

        public static bool TryBeginReveal(float seconds)
        {
            if (!pending) return false;
            EnsureCreated();
            KillTween();
            pending = false;
            curtain.SetBacking(false);
            curtain.SetReveal(0f);
            float elapsed = 0f;
            tween = DOTween.To(() => elapsed, value =>
            {
                elapsed = value;
                curtain.SetReveal(Smooth(value));
            }, 1f, Mathf.Max(.05f, seconds)).SetUpdate(true).OnComplete(() =>
            {
                tween = null;
                curtain.Reset();
            });
            return true;
        }

        public static void Clear()
        {
            pending = false;
            KillTween();
            if (curtain != null) curtain.Reset();
        }

        private static void EnsureCreated()
        {
            if (curtain != null) return;
            host = new GameObject("CoffeeCupScreenTransition", typeof(RectTransform), typeof(Canvas));
            UnityEngine.Object.DontDestroyOnLoad(host);
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            curtain = new CoffeeCupTransition(host.transform);
        }

        private static void KillTween()
        {
            if (tween == null) return;
            tween.Kill(false);
            tween = null;
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);
    }
}
