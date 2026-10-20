using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Data-driven controls inside the original PSD relationship and world regions.</summary>
    public sealed class DetailsPagesView : MonoBehaviour
    {
        private const int PageSize = 31; // Seven columns, five rows; first three cells hold special information and the last holds navigation.
        [SerializeField] private RectTransform characterGrid;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text characterName;
        [SerializeField] private RectTransform introductionContent;
        [SerializeField] private TMP_Text specialInformation;
        [SerializeField] private Image familiarityFill;
        [SerializeField] private TMP_Text familiarityValue;
        [SerializeField] private TMP_FontAsset headingFont;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private RectTransform selectorContent;
        [SerializeField] private TMP_Text worldDescription;
        [SerializeField] private TMP_Text worldValue;
        [SerializeField] private TMP_Text lower;
        [SerializeField] private TMP_Text upper;
        [SerializeField] private TMP_Text lowerDescription;
        [SerializeField] private TMP_Text upperDescription;
        [SerializeField] private Image worldFill;
        [SerializeField] private Sprite characterSilhouette;
        [SerializeField] private Sprite worldSelectedButton;
        [SerializeField] private Sprite worldIdleButton;
        [Header("Story archive")]
        [SerializeField] private RectTransform storyList;
        [SerializeField] private ScrollRect storyScroll;
        [SerializeField] private TMP_Text storyTitle;
        [SerializeField] private TMP_Text storyMeta;
        [SerializeField] private TMP_Text storyBody;
        [SerializeField] private GameObject storyReader;
        private DetailsPanel panel;
        private TMP_FontAsset font;
        private Sprite unknownAvatar;
        private DetailsPanelData data;
        private string characterId;
        private string worldKey;
        private DetailsStoryData selectedStory;
        private readonly List<Image> selectors = new List<Image>();
        private readonly List<Button> characterButtons = new List<Button>();
        public int RelationshipPageIndex { get; private set; }
        public string SelectedWorldProgressKey => worldKey;
        public string SelectedCharacterId => characterId;
        public int RelationshipPageCount => PageCount;
        private int PageCount => data == null ? 1 : Mathf.Max(1, (data.Characters.Count + PageSize - 1) / PageSize);

        public void Initialize(DetailsPanel owner, RectTransform relationshipsPage, RectTransform worldPage, TMP_FontAsset pageFont, Sprite fallback)
        {
            panel = owner; font = pageFont; unknownAvatar = fallback;
            previous.onClick.AddListener(PreviousPage); next.onClick.AddListener(NextPage);
        }
        private void PreviousPage() => panel.ChangeRelationshipPage(-1);
        private void NextPage() => panel.ChangeRelationshipPage(1);

        public void Refresh(DetailsPanelData snapshot)
        {
            panel.HidePageTooltip();
            data = snapshot;
            RelationshipPageIndex = Mathf.Clamp(RelationshipPageIndex, 0, PageCount - 1);
            var selected = FindCharacter(characterId);
            if (selected == null || !selected.Unlocked)
            {
                characterId = null;
                foreach (var character in data.Characters)
                    if (character.Unlocked) { characterId = character.Id; break; }
            }
            RenderRelationships();
            RenderCharacter(FindCharacter(characterId));
            RenderWorldSelectors();
            RenderStories();
        }

        private void RenderStories()
        {
            if (storyList == null || storyTitle == null || storyBody == null) return;
            for (int i = storyList.childCount - 1; i >= 0; i--) Destroy(storyList.GetChild(i).gameObject);
            selectedStory = null;
            storyTitle.text = "选择一段已解锁的剧情";
            storyMeta.text = string.Empty;
            storyBody.text = data == null || data.Stories.Count == 0 ? "暂无可查看的剧情记录。" : "点击左侧条目查看已解锁内容。";
            if (storyReader != null) storyReader.SetActive(true);
            if (data == null) return;
            foreach (var story in data.Stories)
            {
                var item = new GameObject("StoryItem", typeof(RectTransform), typeof(Image), typeof(Button));
                item.transform.SetParent(storyList, false);
                var rect = (RectTransform)item.transform; rect.sizeDelta = new Vector2(420f, 62f);
                var image = item.GetComponent<Image>(); image.color = story.Unlocked ? new Color(.24f, .12f, .05f, .12f) : new Color(.1f, .1f, .1f, .08f);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(item.transform, false);
                var labelRect = (RectTransform)label.transform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = new Vector2(18, 6); labelRect.offsetMax = new Vector2(-18, -6);
                var text = label.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 23; text.color = story.Unlocked ? new Color(.25f, .12f, .04f) : new Color(.25f, .25f, .25f); text.alignment = TextAlignmentOptions.MidlineLeft; text.text = story.Unlocked ? story.Title : "剧情尚未解锁";
                var button = item.GetComponent<Button>(); button.interactable = story.Unlocked;
                if (story.Unlocked) button.onClick.AddListener(() => ShowStory(story));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(storyList);
            if (storyScroll != null) storyScroll.verticalNormalizedPosition = 1f;
        }

        private void ShowStory(DetailsStoryData story)
        {
            selectedStory = story;
            storyTitle.text = story.Title;
            storyMeta.text = story.Group;
            var builder = new System.Text.StringBuilder();
            foreach (var block in story.Blocks)
            {
                if (!string.IsNullOrWhiteSpace(block.Caption)) builder.AppendLine(block.Caption);
                builder.AppendLine(block.BodyText).AppendLine();
            }
            storyBody.text = builder.Length == 0 ? "这段剧情暂时没有可展示的正文。" : builder.ToString().TrimEnd();
        }
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
            for (int i = 0; i < data.Characters.Count; i++)
                if (data.Characters[i].Id == id) { RelationshipPageIndex = i / PageSize; break; }
            RenderRelationships();
            RenderCharacter(character);
            panel.SelectPage(DetailsPanel.Page.Relationships);
            return true;
        }
        public void ReturnToRelationships()
        {
            panel.HidePageTooltip();
            characterId = null;
            if (data != null) { RenderCharacter(null); UpdateCharacterSelection(); }
        }
        public void ChangeRelationshipPage(int delta)
        {
            if (data == null) return;
            panel.HidePageTooltip();
            SetRelationshipPage((int)System.Math.Max(0L, System.Math.Min(PageCount - 1L, (long)RelationshipPageIndex + delta)));
        }
        public bool SetRelationshipPage(int index)
        {
            if (data == null || index < 0 || index >= PageCount) return false;
            panel.HidePageTooltip();
            RelationshipPageIndex = index;
            RenderRelationships();
            return true;
        }
        public bool ScrollTo(DetailsScrollRegion region, float position)
        {
            RectTransform content;
            switch (region)
            {
                case DetailsScrollRegion.CharacterIntroduction: content = introductionContent; break;
                case DetailsScrollRegion.WorldSelectors: content = selectorContent; break;
                default: return false;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var scroll = content.parent.GetComponent<ScrollRect>();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = position;
            return true;
        }
        private void RenderRelationships()
        {
            DetailsViewUI.Clear(characterGrid);
            characterButtons.Clear();
            for (int i = 0; i < 35; i++)
            {
                var cell = DetailsViewUI.Box(characterGrid, "Cell_" + i, Vector2.zero, Vector2.one, Color.clear);
                // These cells preserve the special-information area in the PSD grid.
                if (i < 3 || i == 34) { cell.raycastTarget = false; continue; }
                int index = RelationshipPageIndex * PageSize + i - 3;
                DetailsCharacterData character = index < data.Characters.Count ? data.Characters[index] : null;
                cell.name = character == null ? "EmptyCell_" + (i - 3) : "Character_" + character.Id;
                if (character != null && (character.Avatar != default || !character.Unlocked))
                {
                    var image = DetailsViewUI.Box(cell.transform, "Avatar", new Vector2(.13f, .05f), new Vector2(.87f, .98f), Color.white);
                    image.sprite = character.Unlocked ? character.Avatar : unknownAvatar;
                    image.preserveAspect = true; image.raycastTarget = false;
                }
                if (character == null) { cell.raycastTarget = false; continue; }
                var button = cell.gameObject.AddComponent<Button>(); button.targetGraphic = cell;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.interactable = character.Unlocked;
                characterButtons.Add(button);
                string id = character.Id;
                button.onClick.AddListener(() => panel.ShowCharacter(id));
                if (character.Unlocked) Label(cell.transform, "Name", character.Name, new Vector2(.02f, 0), new Vector2(.98f, .16f), 16, TextAlignmentOptions.Center);
                var hover = cell.GetComponent<DetailsHoverTarget>();
                if (hover == default) hover = cell.gameObject.AddComponent<DetailsHoverTarget>();
                hover.Bind(panel, character.Unlocked ? character.Name : "尚未结识",
                    character.Unlocked ? CharacterHoverDescription(character)
                        : "你还不认识这个人。也许在王都的某个角落，你们终会相遇。");
            }
            previous.interactable = RelationshipPageIndex > 0;
            next.interactable = RelationshipPageIndex < PageCount - 1;
            pageLabel.text = (RelationshipPageIndex + 1) + " / " + PageCount;
            UpdateCharacterSelection();
        }
        private void UpdateCharacterSelection()
        {
            foreach (var button in characterButtons)
                ((Image)button.targetGraphic).color = button.name == "Character_" + characterId ? new Color(.59f, .32f, .08f, .13f) : Color.clear;
        }
        private void RenderCharacter(DetailsCharacterData character)
        {
            DetailsViewUI.Clear(introductionContent);
            portrait.sprite = character != null && character.Avatar != default ? character.Avatar : characterSilhouette;
            characterName.text = character != null ? character.Name : "人物姓名";
            string introduction = character != null ? character.Introduction
                : data.Relationships.Heading + " · " + data.Relationships.Description;
            var text = Label(introductionContent, "Introduction", introduction, Vector2.zero, Vector2.one, 22);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.pivot = new Vector2(.5f, 1);
            text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = new Vector2(0, text.GetPreferredValues(introduction, 241, 0).y + 8);
            text.rectTransform.anchoredPosition = Vector2.zero;
            introductionContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, text.rectTransform.sizeDelta.y);
            introductionContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            familiarityFill.fillAmount = 0; familiarityValue.text = "0 / 100";
            var info = new System.Text.StringBuilder();
            if (character != null)
                for (int i = 0; i < character.Bars.Count; i++)
                {
                    var bar = character.Bars[i];
                    if (i == 0)
                    {
                        familiarityFill.fillAmount = Ratio(bar);
                        familiarityValue.text = bar.Min < 0
                            ? bar.Name + " " + bar.Value.ToString("+0;-0;0") + "（" + bar.Min + " 至 " + bar.Max + "）"
                            : bar.Value.ToString("0.##") + " / " + bar.Max.ToString("0.##");
                    }
                    if (info.Length > 0) info.Append('\n');
                    info.Append(bar.Name).Append("：").Append(bar.Value.ToString("0.##"));
                    if (!string.IsNullOrEmpty(bar.Description)) info.Append("  ").Append(bar.Description);
                }
            if (character != null && !string.IsNullOrWhiteSpace(character.RelationshipSummary))
            {
                if (info.Length > 0) info.Insert(0, character.RelationshipSummary + System.Environment.NewLine);
                else info.Append(character.RelationshipSummary);
            }
            specialInformation.text = info.Length > 0 ? info.ToString() : character == null ? "尚未结识此人物，无法获得相关信息" : "暂无特殊信息";
            var hover = specialInformation.GetComponent<DetailsHoverTarget>();
            if (hover != default) hover.Bind(panel, characterName.text, info.ToString());

        }
        private static string CharacterHoverDescription(DetailsCharacterData character)
        {
            var text = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(character.RelationshipSummary)) text.AppendLine(character.RelationshipSummary);
            if (!string.IsNullOrWhiteSpace(character.Introduction)) text.Append(character.Introduction);
            return text.Length == 0 ? "暂无人物介绍。" : text.ToString().Trim();
        }
        private void RenderWorldSelectors()
        {
            DetailsViewUI.Clear(selectorContent); selectors.Clear();
            var overview = DetailsViewUI.Box(selectorContent, "WorldOverview", Vector2.zero, Vector2.one, Color.white);
            overview.sprite = worldIdleButton;
            overview.raycastTarget = false;
            overview.rectTransform.sizeDelta = new Vector2(255, 166);
            overview.gameObject.AddComponent<LayoutElement>().preferredHeight = 166;
            Label(overview.transform, "Heading", data.World.Heading,
                new Vector2(.07f, .72f), new Vector2(.93f, .94f), 23, TextAlignmentOptions.Center);
            var overviewText = Label(overview.transform, "Summary", data.World.Description,
                new Vector2(.08f, .08f), new Vector2(.92f, .68f), 16);
            overviewText.overflowMode = TextOverflowModes.Overflow;
            bool valid = false;
            foreach (var bar in data.WorldBars) if (bar.Key == worldKey) valid = true;
            if (!valid) worldKey = data.WorldBars.Count > 0 ? data.WorldBars[0].Key : null;
            for (int i = 0; i < Mathf.Max(4, data.WorldBars.Count); i++)
            {
                var bar = i < data.WorldBars.Count ? data.WorldBars[i] : null;
                var image = DetailsViewUI.Box(selectorContent, bar == null ? "Empty_" + i : "Selector_" + bar.Key, Vector2.zero, Vector2.one, Color.white);
                image.sprite = worldIdleButton;
                image.rectTransform.sizeDelta = new Vector2(255, 107);
                image.gameObject.AddComponent<LayoutElement>().preferredHeight = 107;
                selectors.Add(image);
                if (bar == null) { image.raycastTarget = false; continue; }
                var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                button.transition = Selectable.Transition.None; button.navigation = new Navigation { mode = Navigation.Mode.None };
                string key = bar.Key;
                button.onClick.AddListener(() => panel.SelectWorldProgress(key));
                if (bar.Icon != default)
                {
                    var emblem = DetailsViewUI.Box(image.transform, "Emblem", new Vector2(.06f, .14f), new Vector2(.32f, .86f), Color.white);
                    emblem.sprite = bar.Icon; emblem.preserveAspect = true; emblem.raycastTarget = false;
                    var factionName = Label(image.transform, "Name", bar.Name, new Vector2(.34f, .42f), new Vector2(.97f, .9f), 25, TextAlignmentOptions.BottomLeft);
                    factionName.enableAutoSizing = true; factionName.fontSizeMin = 16; factionName.fontSizeMax = 25;
                    var epithet = Label(image.transform, "Subtitle", FirstSegment(bar.Subtitle), new Vector2(.34f, .1f), new Vector2(.97f, .42f), 15, TextAlignmentOptions.TopLeft);
                    epithet.color = new Color(.52f, .32f, .13f);
                }
                else Label(image.transform, "Name", bar.Name, new Vector2(.08f, .08f), new Vector2(.92f, .92f), 31, TextAlignmentOptions.Center);
                var hover = image.GetComponent<DetailsHoverTarget>();
                if (hover == default) hover = image.gameObject.AddComponent<DetailsHoverTarget>();
                hover.Bind(panel, bar.Name, bar.Description);
            }
            selectorContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            RenderWorldProgress();
        }
        public bool SelectWorldProgress(string key)
        {
            if (data == null) return false;
            foreach (var bar in data.WorldBars)
                if (bar.Key == key)
                {
                    worldKey = key; RenderWorldProgress(); panel.SelectPage(DetailsPanel.Page.World); return true;
                }
            return false;
        }
        private void RenderWorldProgress()
        {
            DetailsProgressData selected = null;
            for (int i = 0; i < data.WorldBars.Count; i++)
            {
                bool active = data.WorldBars[i].Key == worldKey;
                selectors[i].sprite = active ? worldSelectedButton : worldIdleButton;
                if (active) selected = data.WorldBars[i];
            }
            bool profile = selected != null && selected.Icon != default;
            RenderFactionProfile(profile ? selected : null);
            worldDescription.text = selected == null ? data.World.Heading
                : profile ? (selected.HasValue ? "你的立场会让这股力量更信任你，或更加警惕你。" : "你还没有与这股力量真正打过交道。")
                : selected.Description;
            worldValue.text = selected == null || !selected.HasValue ? ""
                : selected.Min < 0 ? selected.ValueName + "：" + selected.Value.ToString("+0;-0;0") + "（" + selected.Min.ToString("0.##") + " ~ " + selected.Max.ToString("0.##") + "）"
                : selected.ValueName + "：" + selected.Value.ToString("0.##") + " / " + selected.Max.ToString("0.##");
            worldFill.fillAmount = selected != null && selected.HasValue ? Ratio(selected) : 0;
            lower.text = selected != null && selected.HasValue ? selected.Min.ToString("0.##") : "";
            upper.text = selected != null && selected.HasValue ? selected.Max.ToString("0.##") : "";
            lowerDescription.text = selected != null ? selected.LowerDescription : data.World.Description;
            upperDescription.text = selected != null ? selected.UpperDescription : "";
            BindHover(lower, selected, true); BindHover(upper, selected, false);
        }
        private RectTransform factionProfile;
        private Image factionEmblem;
        private TMP_Text factionName, factionSubtitle, factionSummary;

        /// <summary>Faction card in the free area above the world description line (PSD info region).</summary>
        private void RenderFactionProfile(DetailsProgressData faction)
        {
            if (faction == null) { if (factionProfile != null) factionProfile.gameObject.SetActive(false); return; }
            if (factionProfile == null)
            {
                factionProfile = DetailsViewUI.Root(worldDescription.transform.parent, "FactionProfile", new Vector2(0, 1), new Vector2(0, 1));
                factionProfile.pivot = new Vector2(0, 1);
                factionProfile.sizeDelta = new Vector2(836, 182);
                factionProfile.anchoredPosition = new Vector2(768, -216);
                factionEmblem = DetailsViewUI.Box(factionProfile, "Emblem", new Vector2(0, 0), new Vector2(0, 1), Color.white);
                factionEmblem.rectTransform.offsetMax = new Vector2(160, 0);
                factionEmblem.preserveAspect = true; factionEmblem.raycastTarget = false;
                factionName = Label(factionProfile, "Name", "", new Vector2(0, 1), new Vector2(1, 1), 32, TextAlignmentOptions.BottomLeft);
                Place(factionName.rectTransform, 180, 0, 44);
                factionSubtitle = Label(factionProfile, "Subtitle", "", new Vector2(0, 1), new Vector2(1, 1), 18, TextAlignmentOptions.TopLeft);
                factionSubtitle.color = new Color(.52f, .32f, .13f);
                Place(factionSubtitle.rectTransform, 180, 48, 26);
                var viewport = DetailsViewUI.Box(factionProfile, "Summary", new Vector2(0, 0), new Vector2(1, 1), Color.clear);
                viewport.rectTransform.offsetMin = new Vector2(180, 0); viewport.rectTransform.offsetMax = new Vector2(0, -78);
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                var content = DetailsViewUI.Root(viewport.transform, "Content", new Vector2(0, 1), Vector2.one);
                content.pivot = new Vector2(.5f, 1);
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.viewport = viewport.rectTransform; scroll.content = content;
                scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
                factionSummary = Label(content, "Text", "", Vector2.zero, Vector2.one, 18);
                factionSummary.overflowMode = TextOverflowModes.Overflow; factionSummary.lineSpacing = 4;
                content.gameObject.AddComponent<VerticalLayoutGroup>().childControlHeight = true;
            }
            factionProfile.gameObject.SetActive(true);
            factionEmblem.sprite = faction.Icon;
            factionName.text = faction.Name;
            factionSubtitle.text = faction.Subtitle;
            factionSummary.text = faction.Description;
            var summaryScroll = factionSummary.transform.parent.parent.GetComponent<ScrollRect>();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)factionSummary.transform.parent);
            summaryScroll.verticalNormalizedPosition = 1;
            var hover = factionSummary.transform.parent.parent.GetComponent<DetailsHoverTarget>();
            if (hover == default) hover = factionSummary.transform.parent.parent.gameObject.AddComponent<DetailsHoverTarget>();
            hover.Bind(panel, faction.Name, faction.Description);
        }
        private static void Place(RectTransform rect, float left, float top, float height)
        {
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(0, -top);
        }
        private static string FirstSegment(string subtitle)
        {
            if (string.IsNullOrEmpty(subtitle)) return string.Empty;
            int split = subtitle.IndexOf('·');
            return (split > 0 ? subtitle.Substring(0, split) : subtitle).Trim();
        }
        private void BindHover(TMP_Text label, DetailsProgressData bar, bool isLower)
        {
            var hover = label.GetComponent<DetailsHoverTarget>();
            if (hover == default) hover = label.gameObject.AddComponent<DetailsHoverTarget>();
            label.raycastTarget = bar != null;
            hover.Bind(panel, bar != null ? bar.Name : "", bar != null ? (isLower ? bar.LowerDescription : bar.UpperDescription) : "");
        }
        private static float Ratio(DetailsProgressData bar) => bar.Max > bar.Min ? Mathf.Clamp01((bar.Value - bar.Min) / (bar.Max - bar.Min)) : 0;
        private TMP_Text Label(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = DetailsViewUI.Text(parent, font, name, value, min, max, size, alignment);
            if (size >= 29 && headingFont != default) label.font = headingFont;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = new Color(.35f, .19f, .07f); return label;
        }
        public void Clear()
        {
            data = null; characterId = null; worldKey = null; RelationshipPageIndex = 0;
            DetailsViewUI.Clear(characterGrid); DetailsViewUI.Clear(selectorContent); DetailsViewUI.Clear(introductionContent);
            selectors.Clear(); characterButtons.Clear();
            portrait.sprite = characterSilhouette;
            characterName.text = "人物姓名";
            specialInformation.text = string.Empty;
            familiarityFill.fillAmount = 0; familiarityValue.text = string.Empty;
            worldFill.fillAmount = 0;
            RenderFactionProfile(null);
            worldDescription.text = worldValue.text = lower.text = upper.text = lowerDescription.text = upperDescription.text = string.Empty;
        }
        private void OnDestroy()
        {
            if (previous != default) previous.onClick.RemoveListener(PreviousPage);
            if (next != default) next.onClick.RemoveListener(NextPage);
        }
    }
}
