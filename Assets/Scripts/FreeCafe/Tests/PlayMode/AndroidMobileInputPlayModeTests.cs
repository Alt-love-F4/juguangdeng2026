using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class AndroidMobileInputPlayModeTests
    {
        [Test]
        public void TouchPourRequiresStableReceiverDwellAndVerticalGesture()
        {
            var gesture = new TouchPourGestureRecognizer();
            Vector2 origin = new Vector2(300, 400);

            Assert.That(gesture.Update("cup", origin, 0f, out _), Is.False);
            Assert.That(gesture.Update("cup", origin, .219f, out _), Is.False);
            Assert.That(gesture.IsArmed, Is.False);
            Assert.That(gesture.Update("cup", origin, .22f, out _), Is.False);
            Assert.That(gesture.IsArmed, Is.True);
            Assert.That(gesture.Update("cup", origin + Vector2.right * 30f, .23f, out _), Is.False);
            Assert.That(gesture.Update("cup", origin + Vector2.up * 24f, .24f, out Vector2 pourOrigin), Is.True);
            Assert.That(pourOrigin, Is.EqualTo(origin));
        }

        [Test]
        public void TouchPourTargetChangeRestartsDwellAndResetClearsArmedState()
        {
            var gesture = new TouchPourGestureRecognizer();
            var point = new Vector2(120, 240);
            gesture.Update("cup-a", point, 0f, out _);
            gesture.Update("cup-b", point, .3f, out _);
            Assert.That(gesture.IsArmed, Is.False);

            gesture.Update("cup-b", point, .53f, out _);
            Assert.That(gesture.IsArmed, Is.True);
            gesture.Reset();
            Assert.That(gesture.IsArmed, Is.False);
            Assert.That(gesture.Update(null, point, .8f, out _), Is.False);
        }

        [Test]
        public void TouchPourRejectsMissingReceiverAndMostlyHorizontalMotion()
        {
            var gesture = new TouchPourGestureRecognizer();
            var point = new Vector2(10, 10);
            Assert.That(gesture.Update(null, point, 0f, out _), Is.False);
            gesture.Update("cup", point, 1f, out _);
            gesture.Update("cup", point, 1.22f, out _);
            Assert.That(gesture.Update("cup", point + Vector2.right * 40f, 1.23f, out _), Is.False);
        }

        [Test]
        public void EditorDoesNotActivateAndroidTouchBranch()
        {
            Assert.That(MobileTouchInput.IsAndroidPlayer, Is.False);
            Assert.That(MobileTouchInput.TryGetPrimaryTouch(out _, out _), Is.False);
        }
    }
}
