using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DialoguePortraitSizingTests
    {
        [Test]
        public void EveryConfiguredPortraitHasSizingAndDefaultEqualHeight()
        {
            var table = Resources.Load<DialoguePortraitSizing>("Dialogue/DialoguePortraitSizing");
            Assert.That(table, Is.Not.Null);
            var script = PresentationDebugCharacterGallery.Create();
            for (int i = 0; i < script.Count; i++)
            {
                Check(script[i].LeftCharacter);
                Check(script[i].RightCharacter);
            }
            void Check(DialogueCharacter person)
            {
                if (person?.Portrait == null) return;
                var entry = table.Find(person.CharacterKey, person.Portrait);
                Assert.That(entry, Is.Not.Null, person.CharacterKey);
                Assert.That(entry.heightScale, Is.EqualTo(1f), person.CharacterKey);
                Assert.That(entry.visibleRect.height, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void AllPortraitMeshesNormalizeAlphaBoundsAndRespectIndividualOverrides()
        {
            var table = Resources.Load<DialoguePortraitSizing>("Dialogue/DialoguePortraitSizing");
            Assert.That(table, Is.Not.Null);
            var go = new GameObject("SizingTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var image = go.GetComponent<Image>();
                image.rectTransform.sizeDelta = new Vector2(403.125f, 911.71875f);
                var effect = go.AddComponent<DialoguePortraitBottomAlignment>();
                foreach (var entry in table.entries)
                {
                    image.sprite = entry.sprite;
                    effect.Configure(entry.characterId);
                    using (var mesh = new VertexHelper())
                    {
                        var padding = UnityEngine.Sprites.DataUtility.GetPadding(entry.sprite);
                        var r = entry.sprite.rect;
                        // Reproduce the trimmed Simple Image quad in arbitrary fitted dimensions.
                        float width = r.width * .2f, height = r.height * .2f;
                        mesh.AddVert(new Vector3(padding.x*.2f, padding.y*.2f), Color.white, Vector2.zero);
                        mesh.AddVert(new Vector3(padding.x*.2f, height-padding.w*.2f), Color.white, Vector2.up);
                        mesh.AddVert(new Vector3(width-padding.z*.2f, height-padding.w*.2f), Color.white, Vector2.one);
                        mesh.AddVert(new Vector3(width-padding.z*.2f, padding.y*.2f), Color.white, Vector2.right);
                        effect.ModifyMesh(mesh);
                        UIVertex bottom = default, top = default;
                        mesh.PopulateUIVertex(ref bottom, 0); mesh.PopulateUIVertex(ref top, 1);
                        float fullHeight = (top.position.y-bottom.position.y)/(1f-(padding.y+padding.w)/r.height);
                        Assert.That(fullHeight*entry.visibleRect.height,
                            Is.EqualTo(image.rectTransform.rect.height*table.heightRatio*entry.heightScale).Within(.01f), entry.characterId);
                        float visibleBottom = bottom.position.y + fullHeight*(entry.visibleRect.yMin-padding.y/r.height);
                        Assert.That(visibleBottom, Is.EqualTo(image.rectTransform.rect.yMin+entry.offset.y).Within(.01f), entry.characterId);
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
