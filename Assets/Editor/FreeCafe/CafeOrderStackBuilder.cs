using System;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>
    /// Upgrades only the existing lobby Orders subtree. Does not rebuild the HUD, bind a scene,
    /// replace existing buttons/text components, or author the HUD-owned CharacterAvatar.
    /// </summary>
    public static class CafeOrderStackBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/FreeCafe/CafeLobbyHud.prefab";
        private const string CompendiumPath = "Assets/Resources/Art/UIPrefab/CoffeeCompendiumPanel.prefab";
        private const string LegacyCompendiumPath = "Assets/Prefabs/FreeCafe/CoffeeCompendiumPanel.prefab";
        private static readonly Vector2 Center = new Vector2(.5f, .5f);
        private static readonly Vector2 CardSize = new Vector2(400f, 150f);

        [MenuItem("Tools/Free Cafe/Upgrade Lobby Order Stack Prefab Only")]
        public static void ApplyToPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before upgrading the lobby order stack.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("Missing existing lobby HUD: " + PrefabPath);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyToRoot(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || saved == null)
                    throw new InvalidOperationException("Could not save the lobby order stack prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[FreeCafe] Upgraded only HeaderDock/Orders in " + PrefabPath + "; no scene was changed.");
        }

        /// <summary>Upgrades an explicitly supplied prefab-content root without saving it or touching scenes.</summary>
        public static void ApplyToRoot(GameObject root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before upgrading the lobby order stack.");
            if (root == null) throw new ArgumentNullException(nameof(root));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CompendiumPath);
            if (source == null) source = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyCompendiumPath);
            var style = source == null ? null : source.GetComponent<CoffeeCompendiumPanel>();
            if (style == null || style.font == null || style.rowCard == null || style.couponCard == null ||
                style.divider == null || style.stampRing == null || style.sealRed == null ||
                style.pill == null || style.arrow == null)
                throw new InvalidOperationException("CoffeeCompendiumPanel is missing its font or order-card artwork.");
            Upgrade(root, style);
        }

        private static void Upgrade(GameObject root, CoffeeCompendiumPanel style)
        {
            var orders = Required<RectTransform>(root.transform.Find("HeaderDock/Orders"), "HeaderDock/Orders");
            // Both paths are supported so a second invocation reuses the exact same Order0 object.
            var order = orders.Find("Order0") ?? orders.Find("Cards/Order0");
            var frontRoot = Required<RectTransform>(order, "Order0 or Cards/Order0");
            var frontButton = Required<Button>(order, "Order0 Button");
            var frontImage = Required<Image>(order, "Order0 Image");
            var frontLabel = Required<TMP_Text>(order.Find("Label"), "Order0/Label");
            var previous = Required<RectTransform>(orders.Find("PreviousOrder"), "PreviousOrder");
            var next = Required<RectTransform>(orders.Find("NextOrder"), "NextOrder");
            Required<Button>(previous, "PreviousOrder Button");
            Required<Button>(next, "NextOrder Button");
            Required<Image>(previous, "PreviousOrder Image");
            Required<Image>(next, "NextOrder Image");
            var page = Required<TMP_Text>(orders.Find("Page"), "Page");

            Place(orders, new Vector2(0f, 1f), new Vector2(32f, -44f), new Vector2(440f, 220f));
            orders.pivot = new Vector2(0f, 1f);
            // The transparent Orders footprint must not block tabletop interaction.
            foreach (var graphic in orders.GetComponents<Graphic>()) graphic.raycastTarget = false;
            foreach (string name in new[] { "图层 3", "任务框 副本 2", "Heading", "左右切换按键 副本" })
            {
                var old = orders.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }

            var cards = Node(orders, "Cards", new Vector2(0f, 16f), CardSize);
            // Pose() writes anchoredPosition directly: all four motion roots share this center.
            frontRoot.SetParent(cards, false);
            Place(frontRoot, Center, Vector2.zero, CardSize);
            frontRoot.gameObject.SetActive(true);
            Paper(frontImage, style.couponCard, true);
            frontButton.targetGraphic = frontImage;
            var front = BuildCard(frontRoot, frontLabel, style, true);

            var waiting = new CafeOrderStackView.Card[2];
            for (int i = 0; i < waiting.Length; i++)
            {
                var sheet = Node(cards, "Waiting" + (i + 1), Vector2.zero, CardSize);
                Paper(Component<Image>(sheet), style.rowCard, false);
                waiting[i] = BuildCard(sheet, null, style, false);
                sheet.anchoredPosition = new Vector2(12f, 10f) * (i + 1);
                sheet.localScale = Vector3.one * (1f - .025f * (i + 1));
                sheet.localRotation = Quaternion.Euler(0f, 0f, -1.8f * (i + 1));
                waiting[i].group.alpha = 1f - .08f * (i + 1);
                sheet.gameObject.SetActive(true);
            }
            var outgoingRoot = Node(cards, "Outgoing", Vector2.zero, CardSize);
            Paper(Component<Image>(outgoingRoot), style.couponCard, false);
            var outgoing = BuildCard(outgoingRoot, null, style, false);
            outgoingRoot.gameObject.SetActive(false);
            waiting[1].root.SetAsFirstSibling();
            waiting[0].root.SetSiblingIndex(1);
            frontRoot.SetAsLastSibling();

            var navigation = Picture(orders, "SwitchBar", style.rowCard, new Vector2(0f, -85f), new Vector2(400f, 48f), Color.white);
            navigation.rectTransform.SetSiblingIndex(cards.GetSiblingIndex() + 1);
            ConfigureNavigation(previous, style, true);
            ConfigureNavigation(next, style, false);
            Place(page.rectTransform, Center, new Vector2(0f, -74f), new Vector2(236f, 23f));
            Format(page, style, 16f, style.ink, TextAlignmentOptions.Center);
            page.text = "暂无订单";
            page.gameObject.SetActive(true);
            var caption = Text(orders, "SwitchCaption", "营业后开始接单", new Vector2(0f, -96f),
                new Vector2(244f, 18f), 14f, style.inkSoft, style);

            previous.SetAsLastSibling();
            next.SetAsLastSibling();
            page.transform.SetAsLastSibling();
            caption.transform.SetAsLastSibling();

            var view = Component<CafeOrderStackView>(orders);
            view.cardRoot = cards;
            view.front = front;
            view.outgoing = outgoing;
            view.waiting = waiting;
            view.switchCaption = caption;
            // Cross-prefab / scene references are intentionally discovered by Awake.
            view.hud = null;
            view.iconSource = null;
        }

        private static CafeOrderStackView.Card BuildCard(RectTransform root, TMP_Text existingLabel,
            CoffeeCompendiumPanel style, bool front)
        {
            var group = Component<CanvasGroup>(root);
            group.alpha = 1f;
            group.interactable = front;
            group.blocksRaycasts = front;
            group.ignoreParentGroups = false;

            var heading = Text(root, "TicketHeading", "来客订单", new Vector2(-57f, 55f),
                new Vector2(244f, 22f), 15f, style.inkSoft, style, TextAlignmentOptions.Left);
            Picture(root, "HeaderDivider", style.divider, new Vector2(-4f, 38f), new Vector2(350f, 10f), Color.white);
            var stamp = Picture(root, "NumberStamp", style.stampRing, new Vector2(163f, 51f),
                new Vector2(43f, 43f), style.rust);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            var number = Text(stamp.transform, "Number", "—", Vector2.zero,
                new Vector2(36f, 24f), 17f, style.rust, style);
            Picture(root, "Seal", style.sealRed, new Vector2(118f, 55f), new Vector2(16f, 16f), Color.white);

            TMP_Text label;
            if (front)
            {
                label = existingLabel;
                // HUD caches these bounds, creates a 64px avatar at (-123, 0), then insets only
                // the left text edge by 72px. Missing portraits retain these original bounds.
                Place(label.rectTransform, Center, new Vector2(-35f, 0f), new Vector2(240f, 64f));
                Format(label, style, 20f, style.ink, TextAlignmentOptions.Left);
                label.text = "等待顾客入座";
                label.gameObject.SetActive(true);
            }
            else
            {
                label = Text(root, "Label", "顾客\n<b>等候中的饮品</b>", new Vector2(1f, 0f),
                    new Vector2(168f, 64f), 20f, style.ink, style, TextAlignmentOptions.Left);
            }

            // Do not create or bind front.CharacterAvatar: FreeCafeHud owns its lifetime,
            // placement, sprite and missing-portrait fallback. Other cards own independent images.
            Image avatar = null;
            if (!front)
            {
                avatar = Picture(root, "Avatar", null, new Vector2(-123f, 0f), new Vector2(64f, 64f), Color.white);
                avatar.preserveAspect = true;
                avatar.gameObject.SetActive(false);
            }
            var drink = Picture(root, "Drink", null, new Vector2(155f, 0f), new Vector2(56f, 56f), Color.white);
            drink.preserveAspect = true;
            drink.gameObject.SetActive(false);
            var details = Text(root, "Details", "先备好原料，等候第一位来客", new Vector2(0f, -42f),
                new Vector2(350f, 21f), 15f, style.inkSoft, style, TextAlignmentOptions.Left);
            var patience = Text(root, "Patience", "暂无订单", new Vector2(-107f, -61f),
                new Vector2(154f, 20f), 13f, style.inkSoft, style, TextAlignmentOptions.Left);
            var track = Picture(root, "PatienceTrack", style.pill, new Vector2(89f, -61f),
                new Vector2(176f, 5f), new Color(style.ink.r, style.ink.g, style.ink.b, .18f));
            var fill = Picture(track.transform, "Fill", style.pill, Vector2.zero, new Vector2(176f, 5f), style.good);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            return new CafeOrderStackView.Card {
                root = root, group = group, heading = heading, label = label, details = details,
                number = number, patience = patience, avatar = avatar, drink = drink, patienceFill = fill
            };
        }

        private static void ConfigureNavigation(RectTransform root, CoffeeCompendiumPanel style, bool previous)
        {
            Place(root, Center, new Vector2(previous ? -172f : 172f, -84f), new Vector2(48f, 44f));
            root.gameObject.SetActive(true);
            var image = root.GetComponent<Image>();
            Paper(image, style.rowCard, true);
            image.color = new Color(1f, 1f, 1f, .94f);
            var arrow = Picture(root, "StackArrow", style.arrow, Vector2.zero, new Vector2(23f, 20f), style.rust);
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, previous ? 180f : 0f);
        }

        private static T Required<T>(Transform target, string description) where T : Component
        {
            var result = target == null ? null : target.GetComponent<T>();
            if (result == null) throw new InvalidOperationException("Existing lobby HUD is missing " + description + ".");
            return result;
        }

        private static T Component<T>(Transform target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.gameObject.AddComponent<T>();
        }

        private static RectTransform Node(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var child = parent.Find(name);
            RectTransform rect;
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                rect = (RectTransform)go.transform;
            }
            else rect = Required<RectTransform>(child, name + " RectTransform");
            Place(rect, Center, position, size);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = Center;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Paper(Image image, Sprite sprite, bool raycast)
        {
            image.sprite = sprite;
            image.overrideSprite = null;
            image.color = Color.white;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = raycast;
        }

        private static Image Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            var image = Component<Image>(Node(parent, name, position, size));
            Paper(image, sprite, false);
            image.color = color;
            return image;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position,
            Vector2 size, float fontSize, Color color, CoffeeCompendiumPanel style,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = Node(parent, name, position, size);
            var label = rect.GetComponent<TMP_Text>();
            if (label == null) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            Format(label, style, fontSize, color, alignment);
            label.text = value;
            return label;
        }

        private static void Format(TMP_Text text, CoffeeCompendiumPanel style, float fontSize,
            Color color, TextAlignmentOptions alignment)
        {
            text.font = style.font;
            text.fontSize = text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * .8f;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = true;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
        }
    }
}
