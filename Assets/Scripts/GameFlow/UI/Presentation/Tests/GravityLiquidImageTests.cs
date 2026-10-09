#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class GravityLiquidImageTests
    {
        [TestCase(0f, 0.1f)] [TestCase(0f, 0.8f)]
        [TestCase(30f, 0.2f)] [TestCase(45f, 0.6f)]
        [TestCase(90f, 0.7f)] [TestCase(135f, 0.3f)]
        [TestCase(180f, 0.9f)] [TestCase(270f, 0.5f)]
        public void SurfaceSolverPreservesArea(float degrees, float fill)
        {
            Rect bounds = Rect.MinMaxRect(-0.3f, -0.48f, 0.3f, 0.5f);
            Vector2 normal = new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));
            float offset = LiquidContainerMath.SolveSurfaceOffset(bounds, normal, fill);
            Assert.That(LiquidContainerMath.AreaBelowPlane(bounds, normal, offset), Is.EqualTo(fill).Within(0.00001f));
        }

        [Test]
        public void MouthCapacityIsOpenAndSymmetric()
        {
            Rect bounds = new Rect(-0.5f, -0.5f, 1f, 1f);
            Assert.That(LiquidContainerMath.OpenTopCapacity(bounds, Vector2.up), Is.EqualTo(1f).Within(0.00001f));
            Assert.That(LiquidContainerMath.OpenTopCapacity(bounds, new Vector2(1f, 1f).normalized), Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(LiquidContainerMath.OpenTopCapacity(bounds, new Vector2(-1f, 1f).normalized), Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(LiquidContainerMath.OpenTopCapacity(bounds, Vector2.right), Is.Zero);
            Assert.That(LiquidContainerMath.OpenTopCapacity(bounds, Vector2.down), Is.Zero);
        }

        private Scene scene;
        private Transform parent;
        private GameObject cup;
        private GravityLiquidImage liquid;
        private float oldTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "DebugRoomCanvas") parent = root.transform;
            Assert.That(parent, Is.Not.Null);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GravityLiquidImage.prefab");
            Assert.That(prefab, Is.Not.Null);
            cup = Object.Instantiate(prefab, parent);
            cup.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            liquid = cup.GetComponent<GravityLiquidImage>();
            liquid.AllowSpilling = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (cup != null) Object.Destroy(cup);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = oldTimeScale;
        }

        [UnityTest]
        public IEnumerator ShaderCompilesAndTiltKeepsWorldSurfaceHorizontal()
        {
            Image image = cup.GetComponent<Image>();
            Assert.That(image.material.shader.name, Is.EqualTo(GravityLiquidImage.ShaderName));
            Assert.That(image.material.shader.isSupported, Is.True);
            cup.transform.localRotation = Quaternion.Euler(0f, 0f, 37f);
            cup.transform.localScale = new Vector3(1.5f, 0.7f, 1f);
            liquid.ResetMotion();
            yield return null;
            Vector2 n = liquid.SurfaceNormal;
            Vector3 tangent = cup.transform.TransformVector(new Vector3(n.y, -n.x, 0f));
            Assert.That(Mathf.Abs(tangent.y / tangent.magnitude), Is.LessThan(0.0001f));
            Assert.That(LiquidContainerMath.AreaBelowPlane(liquid.InnerBounds, n, liquid.SurfaceOffset),
                Is.EqualTo(liquid.CurrentFill).Within(0.0001f));
            Assert.That(ShaderUtil.ShaderHasError(image.material.shader), Is.False);
        }

        [UnityTest]
        public IEnumerator ImpulseSettlesEvenWhenTimeScaleIsZero()
        {
            Time.timeScale = 0f;
            liquid.AddImpulse(2f);
            yield return null;
            Assert.That(Mathf.Abs(liquid.SurfaceNormal.x), Is.GreaterThan(0.00001f));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(Mathf.Abs(liquid.SurfaceNormal.x), Is.LessThan(0.02f));
        }

        [UnityTest]
        public IEnumerator MovingImageExcitesSloshThenSettles()
        {
            cup.GetComponent<RectTransform>().anchoredPosition += new Vector2(40f, 0f);
            yield return null;
            Assert.That(Mathf.Abs(liquid.SurfaceNormal.x), Is.GreaterThan(0.00001f));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(Mathf.Abs(liquid.SurfaceNormal.x), Is.LessThan(0.02f));
        }

        [UnityTest]
        public IEnumerator OpenMouthSpillsAndRefillRestoresLiquid()
        {
            liquid.AllowSpilling = true;
            cup.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            liquid.ResetMotion();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(liquid.CurrentFill, Is.LessThan(0.02f));
            cup.transform.localRotation = Quaternion.identity;
            liquid.Refill();
            yield return null;
            Assert.That(liquid.CurrentFill, Is.EqualTo(liquid.FillAmount).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator InstancesOwnMaterialsAndRestoreOriginalOnDisable()
        {
            GameObject other = Object.Instantiate(cup, parent);
            try
            {
                yield return null;
                Image image = cup.GetComponent<Image>();
                Assert.That(other.GetComponent<Image>().material, Is.Not.SameAs(image.material));
                liquid.enabled = false;
                yield return null;
                Assert.That(image.material.shader.name, Is.Not.EqualTo(GravityLiquidImage.ShaderName));
                liquid.enabled = true;
                yield return null;
                Assert.That(image.material.shader.name, Is.EqualTo(GravityLiquidImage.ShaderName));
            }
            finally { Object.Destroy(other); }
        }

        [UnityTest]
        public IEnumerator StencilMaskReceivesChangingSurfaceProperties()
        {
            // Reuse the existing Image as a mask; no new Canvas or test scene layout.
            Transform maskParent = parent.Find("Background");
            cup.transform.SetParent(maskParent, false);
            Image parentImage = maskParent.GetComponent<Image>();
            Assert.That(parentImage, Is.Not.Null);
            Mask mask = maskParent.gameObject.AddComponent<Mask>();
            try
            {
                yield return null;
                liquid.FillAmount = 0.25f;
                liquid.RefreshVisual();
                Material rendered = cup.GetComponent<Image>().materialForRendering;
                Assert.That(rendered.GetVector("_SurfacePlane").w, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(rendered.GetFloat("_Stencil"), Is.GreaterThan(0f));
                liquid.FillAmount = 0.75f;
                yield return null;
                Assert.That(rendered.GetVector("_SurfacePlane").w, Is.EqualTo(0.75f).Within(0.0001f));
            }
            finally { Object.Destroy(mask); }
        }

        [UnityTest]
        public IEnumerator CaptureSharedScenePreview()
        {
            liquid.FillAmount = 0.55f;
            cup.GetComponent<RectTransform>().sizeDelta = new Vector2(350f, 430f);
            cup.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            liquid.ResetMotion();
            yield return null;
            // Render the existing shared canvas with its existing camera; no new test Scene/Canvas/camera.
            Canvas canvas = parent.GetComponent<Canvas>();
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "DebugCamera") camera = root.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null);
            GameObject stage = parent.Find("Stage").gameObject;
            bool stageWasActive = stage.activeSelf;
            stage.SetActive(false);
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
            Texture2D screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                Canvas.ForceUpdateCanvases();
                liquid.RefreshVisual();
                camera.Render();
                RenderTexture.active = target;
                screenshot.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                screenshot.Apply();
                string folder = Path.GetFullPath("output/liquid-ui");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "shared-scene-preview.png"), screenshot.EncodeToPNG());
                Assert.That(ShaderUtil.ShaderHasError(cup.GetComponent<Image>().material.shader), Is.False);
                int bluePixels = 0;
                foreach (Color32 pixel in screenshot.GetPixels32())
                    if (pixel.b > 100 && pixel.g > 60 && pixel.b > pixel.r * 1.5f && pixel.g > pixel.r * 1.3f) bluePixels++;
                Assert.That(bluePixels, Is.GreaterThan(1000), "Liquid must actually render, not merely compile.");
                // Inspect the actual rasterized liquid top in three central columns. A tilted cup must not tilt the water.
                int minTop = 720, maxTop = -1;
                for (int x = 620; x <= 660; x += 20)
                {
                    int top = -1;
                    for (int y = 0; y < 720; y++)
                    {
                        Color pixel = screenshot.GetPixel(x, y);
                        if (pixel.r < 0.35f && pixel.g > 0.35f && pixel.b > 0.5f) top = y;
                    }
                    Assert.That(top, Is.GreaterThan(0), "Expected visible liquid in center column " + x);
                    minTop = Mathf.Min(minTop, top);
                    maxTop = Mathf.Max(maxTop, top);
                }
                Assert.That(maxTop - minTop, Is.LessThanOrEqualTo(2), "Rasterized liquid surface must remain horizontal under Canvas batching.");
            }
            finally
            {
                stage.SetActive(stageWasActive);
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(screenshot);
            }
        }
    }
}
#endif
