using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeTutorialPanelPlayModeTests
    {
        private FreeCafeTutorialPanel panel;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (panel != null) Object.Destroy(panel.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedPanelUsesSmallTrailMarkersAndTranslucentMouseBadge()
        {
            var prefab = Resources.Load<FreeCafeTutorialPanel>("UI/FreeCafeTutorialPanel");
            Assert.That(prefab, Is.Not.Null);
            panel = Object.Instantiate(prefab);
            yield return null;
            Assert.That(panel.trail.Length, Is.EqualTo(10));
            for (int i = 0; i < panel.trail.Length; i++)
            {
                var rect = panel.trail[i].rectTransform;
                float size = i == 0 ? panel.trailHeadSize : panel.trailSize;
                Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.one * size));
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax));
                Assert.That(panel.trail[i].raycastTarget, Is.False);
            }
            var group = panel.gesture.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(.78f).Within(.001f));
            Assert.That(group.blocksRaycasts, Is.False);
            panel.SetMouseMode(true, true);
            Assert.That(group.alpha, Is.EqualTo(panel.gestureOpacity));
        }
    }
}
