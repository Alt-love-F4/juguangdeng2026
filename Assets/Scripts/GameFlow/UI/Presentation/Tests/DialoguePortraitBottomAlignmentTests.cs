using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Deterministic mesh and effect composition tests; no scene loading or automatic Update.</summary>
    public sealed class DialoguePortraitBottomAlignmentTests
    {
        private const float Tolerance = .001f;
        private const float FocusSeconds = .2f;
        private static readonly Color InactiveTint = new Color(.45f, .55f, .65f, .8f);
        private GameObject root;
        private Image left, right, leftFrame, rightFrame, overlay;
        private DialoguePortraitBottomAlignment alignment;
        private Texture2D texture;
        private Sprite sprite;
        private PresentationEffectLibrary library;
        private PresentationEffects effects;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PortraitBottomAlignmentTest", typeof(RectTransform));
            overlay = CreateImage("Overlay", root.transform);
            leftFrame = CreateImage("LeftFrame", root.transform);
            rightFrame = CreateImage("RightFrame", root.transform);
            ConfigureFrame(leftFrame.rectTransform, new Vector2(-250f, 55f));
            ConfigureFrame(rightFrame.rectTransform, new Vector2(250f, 55f));
            left = CreateImage("LeftPortrait", leftFrame.transform);
            right = CreateImage("RightPortrait", rightFrame.transform);
            left.rectTransform.sizeDelta = new Vector2(240f, 500f);
            left.rectTransform.pivot = new Vector2(.3f, .7f);
            left.rectTransform.anchoredPosition = new Vector2(13f, 27f);
            left.rectTransform.localScale = new Vector3(.9f, 1.1f, 1f);
            texture = new Texture2D(8, 4);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 4f), new Vector2(.5f, .5f),
                100f, 0, SpriteMeshType.FullRect);
            left.sprite = right.sprite = sprite;
            left.type = Image.Type.Simple;
            left.preserveAspect = true;
            alignment = left.gameObject.AddComponent<DialoguePortraitBottomAlignment>();
        }

        [TearDown]
        public void TearDown()
        {
            effects?.Dispose();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            // The Resources library is shared project data, not a fixture-owned instance.
        }

        [Test]
        public void ModifyMeshAlignsLowestVertexToRectBottomWithEitherTranslationDirection()
        {
            foreach (float gap in new[] { 120f, -75f })
            {
                UIVertex[] source = CreateVertices(left.rectTransform.rect.yMin + gap);
                using (VertexHelper mesh = CreateMesh(source))
                {
                    alignment.ModifyMesh(mesh);
                    Assert.That(MinY(mesh), Is.EqualTo(left.rectTransform.rect.yMin).Within(Tolerance));
                    for (int i = 0; i < source.Length; i++)
                    {
                        UIVertex actual = ReadVertex(mesh, i);
                        Assert.That(actual.position.x, Is.EqualTo(source[i].position.x));
                        Assert.That(actual.position.y, Is.EqualTo(source[i].position.y - gap).Within(Tolerance));
                        Assert.That(actual.position.z, Is.EqualTo(source[i].position.z));
                    }
                }
            }
        }

        [Test]
        public void ModifyMeshIsIdempotentAndFreshRebuildDoesNotAccumulateTranslation()
        {
            UIVertex[] source = CreateVertices(left.rectTransform.rect.yMin + 120f);
            using (VertexHelper mesh = CreateMesh(source))
            using (VertexHelper rebuilt = CreateMesh(source))
            {
                alignment.ModifyMesh(mesh);
                var aligned = new UIVertex[source.Length];
                for (int i = 0; i < aligned.Length; i++) aligned[i] = ReadVertex(mesh, i);
                alignment.ModifyMesh(mesh);
                alignment.ModifyMesh(rebuilt);
                for (int i = 0; i < aligned.Length; i++)
                {
                    Assert.That(ReadVertex(mesh, i).position, Is.EqualTo(aligned[i].position));
                    Assert.That(ReadVertex(rebuilt, i).position, Is.EqualTo(aligned[i].position));
                }
            }
        }

        [Test]
        public void ModifyMeshPreservesUvColorNormalsTangentsAndTopology()
        {
            UIVertex[] source = CreateVertices(left.rectTransform.rect.yMin + 120f);
            using (VertexHelper mesh = CreateMesh(source))
            {
                alignment.ModifyMesh(mesh);
                Assert.That(mesh.currentVertCount, Is.EqualTo(4));
                Assert.That(mesh.currentIndexCount, Is.EqualTo(6));
                for (int i = 0; i < source.Length; i++)
                {
                    UIVertex actual = ReadVertex(mesh, i);
                    Assert.That(actual.uv0, Is.EqualTo(source[i].uv0));
                    Assert.That(actual.uv1, Is.EqualTo(source[i].uv1));
                    Assert.That(actual.uv2, Is.EqualTo(source[i].uv2));
                    Assert.That(actual.uv3, Is.EqualTo(source[i].uv3));
                    Assert.That(actual.color, Is.EqualTo(source[i].color));
                    Assert.That(actual.normal, Is.EqualTo(source[i].normal));
                    Assert.That(actual.tangent, Is.EqualTo(source[i].tangent));
                }
                var result = new Mesh();
                try
                {
                    mesh.FillMesh(result);
                    Assert.That(result.triangles, Is.EqualTo(new[] { 0, 1, 2, 2, 3, 0 }));
                }
                finally { Object.DestroyImmediate(result); }
            }
        }

        [Test]
        public void ModifyMeshDoesNotMutatePortraitOrFrameTransformsOrImageColor()
        {
            left.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 11f);
            left.color = new Color(.2f, .4f, .6f, .35f);
            var portraitPose = new RectPose(left.rectTransform);
            var framePose = new RectPose(leftFrame.rectTransform);
            Color color = left.color;
            using (VertexHelper mesh = CreateMesh(CreateVertices(left.rectTransform.rect.yMin + 120f)))
                alignment.ModifyMesh(mesh);
            portraitPose.AssertUnchanged(left.rectTransform);
            framePose.AssertUnchanged(leftFrame.rectTransform);
            Assert.That(left.color, Is.EqualTo(color));
        }

        [Test]
        public void GeneratedImageMeshStaysBottomAlignedAfterSpriteReplacementAndResizing()
        {
            root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Sprite tall = Sprite.Create(texture, new Rect(0f, 0f, 2f, 4f), new Vector2(.5f, .5f),
                100f, 0, SpriteMeshType.FullRect);
            try
            {
                AssertGeneratedBottomAndHeight(left, 120f);
                left.sprite = tall;
                AssertGeneratedBottomAndHeight(left, 480f);
                left.rectTransform.sizeDelta = new Vector2(120f, 800f);
                AssertGeneratedBottomAndHeight(left, 240f);
                left.sprite = sprite;
                AssertGeneratedBottomAndHeight(left, 60f);
            }
            finally { Object.DestroyImmediate(tall); }
        }

        private static void AssertGeneratedBottomAndHeight(Image image, float expectedHeight)
        {
            image.SetVerticesDirty();
            Canvas.ForceUpdateCanvases();
            image.Rebuild(CanvasUpdate.PreRender);
            Mesh mesh = image.canvasRenderer.GetMesh(); // Borrowed renderer mesh; cleaned up with the fixture hierarchy.
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(4), "Inspect the actual Simple Image quad, not an empty renderer.");
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (Vector3 vertex in mesh.vertices)
            {
                min = Mathf.Min(min, vertex.y);
                max = Mathf.Max(max, vertex.y);
            }
            Assert.That(min, Is.EqualTo(image.rectTransform.rect.yMin).Within(Tolerance));
            Assert.That(max - min, Is.EqualTo(expectedHeight).Within(Tolerance),
                "Bottom alignment must preserve the generated aspect-fitted height.");
        }

        [Test]
        public void FocusKeepsAlignedFrameBottomFixedWhileUnalignedFrameUsesLegacyPosition()
        {
            RectTransform frame = leftFrame.rectTransform;
            Vector2 restPosition = frame.anchoredPosition;
            Vector3 restScale = frame.localScale;
            float baseline = Bottom(frame);
            Vector2 rightRestPosition = rightFrame.rectTransform.anchoredPosition;
            Vector3 rightRestScale = rightFrame.rectTransform.localScale;
            var imagePose = new RectPose(left.rectTransform);
            InitializeEffects();
            Assert.That(Bottom(frame), Is.EqualTo(baseline).Within(Tolerance));
            effects.SetFocus(DialogueSpeaker.Right);
            effects.Tick(FocusSeconds * .5f);
            Assert.That(frame.localScale.y, Is.EqualTo(restScale.y * 1.0125f).Within(Tolerance));
            Assert.That(Bottom(frame), Is.EqualTo(baseline).Within(Tolerance));
            effects.Tick(FocusSeconds);
            Assert.That(Bottom(frame), Is.EqualTo(baseline).Within(Tolerance));
            Assert.That(frame.anchoredPosition, Is.EqualTo(restPosition));
            Assert.That(frame.localScale, Is.EqualTo(restScale));
            imagePose.AssertUnchanged(left.rectTransform);
            Assert.That(right.GetComponent<DialoguePortraitBottomAlignment>(), Is.Null);
            Assert.That(rightFrame.rectTransform.anchoredPosition, Is.EqualTo(rightRestPosition));
            Assert.That(rightFrame.rectTransform.localScale.y,
                Is.EqualTo(rightRestScale.y * 1.025f).Within(Tolerance));
        }

        [Test]
        public void FocusWithoutFrameCompensatesOnAlignedImageOnly()
        {
            float baseline = Bottom(left.rectTransform);
            Vector3 restScale = left.rectTransform.localScale;
            Vector2 rightRestPosition = right.rectTransform.anchoredPosition;
            InitializeEffects(useFrames: false);
            effects.SetFocus(DialogueSpeaker.Right);
            effects.Tick(FocusSeconds * .5f);
            Assert.That(Bottom(left.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
            Assert.That(left.rectTransform.localScale.y,
                Is.EqualTo(restScale.y * 1.0125f).Within(Tolerance));
            Assert.That(right.rectTransform.anchoredPosition, Is.EqualTo(rightRestPosition));
            effects.Tick(FocusSeconds);
            Assert.That(Bottom(left.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
        }

        [Test]
        public void EntranceSizeCompensationLeavesAuthoredMotionOffsetAdditive()
        {
            RectTransform frame = leftFrame.rectTransform;
            float baseline = Bottom(frame);
            Vector2 restPosition = frame.anchoredPosition;
            Vector3 restScale = frame.localScale;
            InitializeEffects();
            effects.SetFocus(DialogueSpeaker.Right, immediate: true);
            var definition = library.Find(PresentationEffectIds.EnterShadowWalk);
            var entrance = effects.Play(PresentationEffectIds.EnterShadowWalk, 0);
            effects.Tick(definition.duration * .5f);
            Assert.That(frame.localScale.y, Is.EqualTo(restScale.y * .92f).Within(Tolerance));
            Assert.That(Bottom(frame), Is.EqualTo(baseline - 8f).Within(Tolerance),
                "Bottom locking must preserve the aligned portrait's below-baseline entrance offset.");
            Assert.That(frame.anchoredPosition.x,
                Is.EqualTo(restPosition.x - definition.strength * .5f).Within(Tolerance));
            effects.Tick(definition.duration);
            Assert.That(entrance.IsCompleted, Is.True);
            Assert.That(Bottom(frame), Is.EqualTo(baseline).Within(Tolerance));
        }

        [Test]
        public void NodMovesBottomThenRestoresFocusedBaseline()
        {
            float baseline = Bottom(leftFrame.rectTransform);
            InitializeEffects();
            effects.SetFocus(DialogueSpeaker.Right);
            effects.Tick(FocusSeconds);
            float duration = library.Find(PresentationEffectIds.PortraitNod).duration;
            float strength = library.Find(PresentationEffectIds.PortraitNod).strength;
            var nod = effects.Nod(0);
            effects.Tick(duration * .5f);
            Assert.That(nod.IsDone, Is.False);
            Assert.That(Bottom(leftFrame.rectTransform),
                Is.EqualTo(baseline - strength).Within(Tolerance));
            effects.Tick(duration);
            Assert.That(nod.IsCompleted, Is.True);
            Assert.That(Bottom(leftFrame.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
            effects.SetFocus(DialogueSpeaker.Left);
            effects.Tick(FocusSeconds);
            Assert.That(Bottom(leftFrame.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
        }

        [Test]
        public void PortraitFadePreservesBaselineAndComposesWithInactiveColorAtMidpointAndCompletion()
        {
            float baseline = Bottom(leftFrame.rectTransform);
            Color frameColor = new Color(.3f, .5f, .7f, .6f);
            leftFrame.color = frameColor;
            InitializeEffects();
            effects.SetFocus(DialogueSpeaker.Right);
            effects.Tick(FocusSeconds);
            AssertColor(left.color, InactiveTint);
            float duration = library.Find(PresentationEffectIds.PortraitFadeOut).duration;
            var fadeOut = effects.Play(PresentationEffectIds.PortraitFadeOut, 0);
            effects.Tick(duration * .5f);
            Assert.That(fadeOut.IsDone, Is.False);
            AssertColor(left.color, WithAlpha(InactiveTint, InactiveTint.a * .5f));
            AssertColor(leftFrame.color, WithAlpha(frameColor, frameColor.a * .5f));
            Assert.That(Bottom(leftFrame.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
            effects.Tick(duration);
            Assert.That(fadeOut.IsCompleted, Is.True);
            AssertColor(left.color, WithAlpha(InactiveTint, 0f));
            AssertColor(leftFrame.color, WithAlpha(frameColor, 0f));
            Assert.That(Bottom(leftFrame.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
            duration = library.Find(PresentationEffectIds.PortraitFadeIn).duration;
            var fadeIn = effects.Play(PresentationEffectIds.PortraitFadeIn, 0);
            effects.Tick(duration * .5f);
            Assert.That(fadeIn.IsDone, Is.False);
            AssertColor(left.color, WithAlpha(InactiveTint, InactiveTint.a * .5f));
            Assert.That(Bottom(leftFrame.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
            effects.Tick(duration);
            Assert.That(fadeIn.IsCompleted, Is.True);
            AssertColor(left.color, InactiveTint);
            AssertColor(leftFrame.color, frameColor);
            Assert.That(Bottom(leftFrame.rectTransform), Is.EqualTo(baseline).Within(Tolerance));
        }

        private void InitializeEffects(bool useFrames = true)
        {
            library = Resources.Load<PresentationEffectLibrary>("Dialogue/PresentationEffectLibrary");
            Assert.That(library, Is.Not.Null, "The authored presentation effect library must be available.");
            effects = new PresentationEffects(library, root.GetComponent<RectTransform>(), overlay,
                left, useFrames ? leftFrame : null, right, useFrames ? rightFrame : null,
                InactiveTint, FocusSeconds);
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void ConfigureFrame(RectTransform rect, Vector2 position)
        {
            rect.sizeDelta = new Vector2(330f, 640f);
            rect.pivot = new Vector2(.4f, .8f);
            rect.anchoredPosition = position;
            rect.localScale = new Vector3(1.1f, .85f, 1f);
        }

        private static float Bottom(RectTransform rect) =>
            rect.anchoredPosition.y + rect.rect.yMin * rect.localScale.y;

        private static UIVertex[] CreateVertices(float bottom)
        {
            var vertices = new UIVertex[4];
            Vector3[] positions = {
                new Vector3(-30f, bottom, 1f), new Vector3(-30f, bottom + 160f, 2f),
                new Vector3(70f, bottom + 160f, 3f), new Vector3(70f, bottom, 4f)
            };
            for (int i = 0; i < vertices.Length; i++)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = positions[i];
                vertex.uv0 = new Vector4(i * .1f, .2f, .3f, .4f);
                vertex.uv1 = new Vector4(.5f, i * .1f, .6f, .7f);
                vertex.uv2 = new Vector4(.8f, .9f, i * .1f, 1f);
                vertex.uv3 = new Vector4(.2f, .4f, .6f, i * .1f);
                vertex.color = new Color32((byte)(20 + i * 30), (byte)(40 + i), (byte)(80 + i), (byte)(120 + i));
                vertex.normal = new Vector3(0f, 0f, -1f);
                vertex.tangent = new Vector4(1f, 0f, 0f, -1f);
                vertices[i] = vertex;
            }
            return vertices;
        }

        private static VertexHelper CreateMesh(UIVertex[] vertices)
        {
            var mesh = new VertexHelper();
            foreach (UIVertex vertex in vertices) mesh.AddVert(vertex);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
            return mesh;
        }

        private static UIVertex ReadVertex(VertexHelper mesh, int index)
        {
            UIVertex vertex = default;
            mesh.PopulateUIVertex(ref vertex, index);
            return vertex;
        }

        private static float MinY(VertexHelper mesh)
        {
            float min = float.PositiveInfinity;
            for (int i = 0; i < mesh.currentVertCount; i++) min = Mathf.Min(min, ReadVertex(mesh, i).position.y);
            return min;
        }

        private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance));
        }

        private readonly struct RectPose
        {
            private readonly Vector2 anchorMin, anchorMax, pivot, sizeDelta;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;

            public RectPose(RectTransform rect)
            {
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax;
                pivot = rect.pivot; sizeDelta = rect.sizeDelta;
                position = rect.anchoredPosition3D; scale = rect.localScale; rotation = rect.localRotation;
            }

            public void AssertUnchanged(RectTransform rect)
            {
                Assert.That(rect.anchorMin, Is.EqualTo(anchorMin));
                Assert.That(rect.anchorMax, Is.EqualTo(anchorMax));
                Assert.That(rect.pivot, Is.EqualTo(pivot));
                Assert.That(rect.sizeDelta, Is.EqualTo(sizeDelta));
                Assert.That(rect.anchoredPosition3D, Is.EqualTo(position));
                Assert.That(rect.localScale, Is.EqualTo(scale));
                Assert.That(rect.localRotation, Is.EqualTo(rotation));
            }
        }
    }
}
