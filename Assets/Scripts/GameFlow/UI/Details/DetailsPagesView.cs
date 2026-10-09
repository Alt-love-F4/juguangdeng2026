using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Runtime extension of the prefab's relationships and world page roots.</summary>
    public sealed class DetailsPagesView : MonoBehaviour
    {
        private const int PageSize = 6;
        private DetailsPanel panel;
        private TMP_FontAsset font;
        private Sprite unknownAvatar;
        private RectTransform relationships;
        private RectTransform world;
        private DetailsPanelData data;
        private string characterId;
        public int RelationshipPageIndex { get; private set; }

        public void Initialize(DetailsPanel owner, RectTransform relationshipsPage, RectTransform worldPage,
            TMP_FontAsset pageFont, Sprite fallback)
        {
            panel = owner;
            font = pageFont;
            unknownAvatar = fallback;
            relationships = Extend(relationshipsPage, "RelationshipsView");
            world = Extend(worldPage, "WorldView");
        }

        private static RectTransform Extend(RectTransform page, string name)
        {
            for (int i = 0; i < page.childCount; i++) page.GetChild(i).gameObject.SetActive(false);
            return DetailsViewUI.Root(page, name, Vector2.zero, Vector2.one);
        }

        public void Refresh(DetailsPanelData snapshot)
        {
            data = snapshot;
            RelationshipPageIndex = Mathf.Clamp(RelationshipPageIndex, 0, PageCount - 1);
            DetailsCharacterData selected = FindCharacter(characterId);
            if (selected != null && selected.Unlocked) RenderCharacter(selected);
            else ReturnToRelationships();
            RenderWorld();
        }

        private int PageCount => Mathf.Max(1, (data.Characters.Count + PageSize - 1) / PageSize);
        private DetailsCharacterData FindCharacter(string id)
        {
            if (data == null || id == null) return null;
            foreach (var character in data.Characters) if (character.Id == id) return character;
            return null;
        }

        public bool ShowCharacter(string id)
        {
            var character = FindCharacter(id);
            if (character == null || !character.Unlocked) return false;
            characterId = id;
            panel.SelectPage(DetailsPanel.Page.Relationships);
            RenderCharacter(character);
            return true;
        }

        public void ReturnToRelationships()
        {
            characterId = null;
            if (data != null) RenderRelationships();
        }

        public void ChangeRelationshipPage(int delta)
        {
            if (data == null) return;
            RelationshipPageIndex = (int)System.Math.Max(0L, System.Math.Min(PageCount - 1L, (long)RelationshipPageIndex + delta));
            ReturnToRelationships();
        }

        private void RenderRelationships()
        {
            DetailsViewUI.Clear(relationships);
            DetailsViewUI.Text(relationships, font, "Title", "人物关系", new Vector2(0, .9f), Vector2.one, 30);
            if (data.Characters.Count == 0)
                DetailsViewUI.Text(relationships, font, "Empty", data.Relationships.Heading + "\n" + data.Relationships.Description,
                    new Vector2(.08f, .2f), new Vector2(.92f, .8f), 25, TextAlignmentOptions.Center);
            int start = RelationshipPageIndex * PageSize;
            for (int i = 0; i < PageSize && start + i < data.Characters.Count; i++)
            {
                var character = data.Characters[start + i];
                int column = i % 3;
                int row = i / 3;
                float left = column / 3f + .012f;
                float top = .86f - row * .36f;
                var button = DetailsViewUI.Button(relationships, font, "Character_" + (start + i), "",
                    new Vector2(left, top - .33f), new Vector2(left + .31f, top));
                DetailsViewUI.Avatar(button.transform, font, character.Unlocked ? character.Avatar : null, unknownAvatar,
                    new Vector2(.28f, .31f), new Vector2(.72f, .92f));
                string label = character.Unlocked ? character.Name : "未知人物";
                DetailsViewUI.Text(button.transform, font, "Name", label, new Vector2(.05f, .03f), new Vector2(.95f, .27f), 24, TextAlignmentOptions.Center);
                button.interactable = character.Unlocked;
                string id = character.Id;
                button.onClick.AddListener(() => panel.ShowCharacter(id));
                button.gameObject.AddComponent<DetailsHoverTarget>().Bind(panel, label,
                    character.Unlocked ? character.Introduction : "尚未结识此人物。");
            }
            var previous = DetailsViewUI.Button(relationships, font, "Previous", "←", new Vector2(.22f, .015f), new Vector2(.34f, .115f));
            previous.interactable = RelationshipPageIndex > 0;
            previous.onClick.AddListener(() => panel.ChangeRelationshipPage(-1));
            var next = DetailsViewUI.Button(relationships, font, "Next", "→", new Vector2(.66f, .015f), new Vector2(.78f, .115f));
            next.interactable = RelationshipPageIndex < PageCount - 1;
            next.onClick.AddListener(() => panel.ChangeRelationshipPage(1));
            DetailsViewUI.Text(relationships, font, "PageNumber", (RelationshipPageIndex + 1) + " / " + PageCount,
                new Vector2(.36f, .015f), new Vector2(.64f, .115f), 23, TextAlignmentOptions.Center);
        }

        private void RenderCharacter(DetailsCharacterData character)
        {
            DetailsViewUI.Clear(relationships);
            var back = DetailsViewUI.Button(relationships, font, "Return", "← 返回", new Vector2(0, .89f), new Vector2(.18f, 1));
            back.onClick.AddListener(panel.ReturnToRelationships);
            DetailsViewUI.Text(relationships, font, "Title", "人物信息", new Vector2(.23f, .89f), Vector2.one, 30);
            DetailsViewUI.Avatar(relationships, font, character.Avatar, unknownAvatar, new Vector2(.02f, .61f), new Vector2(.2f, .85f));
            DetailsViewUI.Text(relationships, font, "Name", character.Name, new Vector2(.24f, .76f), new Vector2(.98f, .86f), 29);
            var introduction = DetailsViewUI.Text(relationships, font, "Introduction", character.Introduction,
                new Vector2(.24f, .6f), new Vector2(.98f, .75f), 22, TextAlignmentOptions.TopLeft);
            introduction.raycastTarget = true;
            introduction.gameObject.AddComponent<DetailsHoverTarget>().Bind(panel, character.Name, character.Introduction);
            var content = DetailsViewUI.Scroll(relationships, "CharacterProgress", new Vector2(0, .01f), new Vector2(1, .55f));
            RenderBars(content, character.Bars, "暂无人物信息进度");
        }

        private void RenderWorld()
        {
            DetailsViewUI.Clear(world);
            DetailsViewUI.Text(world, font, "Title", data.World.Title, new Vector2(0, .89f), Vector2.one, 30);
            var content = DetailsViewUI.Scroll(world, "WorldProgress", new Vector2(0, .01f), new Vector2(1, .85f));
            RenderBars(content, data.WorldBars, data.World.Heading + "\n" + data.World.Description);
        }

        private void RenderBars(RectTransform content, IReadOnlyList<DetailsProgressData> bars, string empty)
        {
            if (bars.Count == 0)
            {
                var label = DetailsViewUI.Text(content, font, "Empty", empty, Vector2.zero, Vector2.one, 24, TextAlignmentOptions.Center);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight = 130;
            }
            foreach (var bar in bars)
            {
                var card = DetailsViewUI.Box(content, "Progress_" + bar.Key, Vector2.zero, Vector2.one, DetailsViewUI.Card);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
                DetailsViewUI.Text(card.transform, font, "Name", bar.Name, new Vector2(.03f, .59f), new Vector2(.7f, .96f));
                DetailsViewUI.Text(card.transform, font, "Value", bar.Value.ToString("0.##") + "  [" + bar.Min.ToString("0.##") + "–" + bar.Max.ToString("0.##") + "]",
                    new Vector2(.7f, .59f), new Vector2(.97f, .96f), 22, TextAlignmentOptions.MidlineRight);
                var track = DetailsViewUI.Box(card.transform, "Track", new Vector2(.03f, .43f), new Vector2(.97f, .52f), new Color(.08f, .06f, .11f));
                track.raycastTarget = false;
                float amount = bar.Max > bar.Min ? Mathf.Clamp01((bar.Value - bar.Min) / (bar.Max - bar.Min)) : 0;
                DetailsViewUI.Box(track.transform, "Fill", Vector2.zero, new Vector2(amount, 1), DetailsViewUI.Accent).raycastTarget = false;
                var lower = DetailsViewUI.Text(card.transform, font, "LowerDescription", bar.LowerDescription,
                    new Vector2(.03f, .04f), new Vector2(.49f, .36f), 18);
                var upper = DetailsViewUI.Text(card.transform, font, "UpperDescription", bar.UpperDescription,
                    new Vector2(.51f, .04f), new Vector2(.97f, .36f), 18, TextAlignmentOptions.MidlineRight);
                lower.raycastTarget = upper.raycastTarget = true;
                lower.gameObject.AddComponent<DetailsHoverTarget>().Bind(panel, bar.Name, bar.LowerDescription);
                upper.gameObject.AddComponent<DetailsHoverTarget>().Bind(panel, bar.Name, bar.UpperDescription);
                card.gameObject.AddComponent<DetailsHoverTarget>().Bind(panel, bar.Name, bar.Description);
            }
        }

        public void Clear()
        {
            data = null;
            characterId = null;
            RelationshipPageIndex = 0;
            if (relationships != null) DetailsViewUI.Clear(relationships);
            if (world != null) DetailsViewUI.Clear(world);
        }
    }
}
