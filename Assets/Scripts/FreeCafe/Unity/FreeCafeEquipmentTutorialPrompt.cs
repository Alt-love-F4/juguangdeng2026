using System.Collections.Generic;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// Optional post-purchase guidance. Initialize immediately after creating the world, before purchases.
    /// The runtime should include IsOpen in its modal-input and business-simulation pause gates.
    /// Pending purchases are session-local; loading equipment state does not enqueue a tutorial.
    /// </summary>
    public sealed class FreeCafeEquipmentTutorialPrompt : MonoBehaviour
    {
        private FreeCafeRuntime app;
        private CafeWorld subscribedWorld;
        private readonly Queue<bool> pending = new Queue<bool>();
        private GameObject ui, dialog, notice;
        private TMP_Text question, notification;
        private TMP_FontAsset font;
        private int acquiredFrame = -1;
        private int retryAfterFrame;
        public bool IsOpen => dialog != null && dialog.activeInHierarchy;

        public void Initialize(FreeCafeRuntime owner)
        {
            // Repeated initialization with the same owner is harmless and preserves pending purchases.
            if (app != owner)
            {
                Unsubscribe();
                pending.Clear();
                Close();
                app = owner;
            }
            Subscribe();
            UpdateNotice();
        }

        private void Subscribe()
        {
            var world = app != null ? app.World : null;
            if (ReferenceEquals(world, subscribedWorld)) return;
            Unsubscribe();
            subscribedWorld = world;
            if (subscribedWorld != null) subscribedWorld.EquipmentAcquired += OnEquipmentAcquired;
        }

        private void Unsubscribe()
        {
            if (subscribedWorld != null) subscribedWorld.EquipmentAcquired -= OnEquipmentAcquired;
            subscribedWorld = null;
        }

        private void OnEquipmentAcquired(bool latte)
        {
            pending.Enqueue(latte);
            acquiredFrame = Time.frameCount;
            // Separate persistent UI: Report(ActivateEquipment(...)) may overwrite runtime.Hint
            // after this event returns. This notification must survive that overwrite and an open book.
            EnsureUI();
            UpdateNotice();
        }

        private void LateUpdate()
        {
            Subscribe();
            if (app == null || pending.Count == 0) return;
            EnsureUI();
            UpdateNotice();
            if (IsOpen || Time.frameCount <= acquiredFrame || Time.frameCount < retryAfterFrame) return;
            if (app.World == null || app.IsDragging || app.PracticeTutorialActive || app.CafeTutorialActive
                || app.InputBlocked || app.TutorialInputBlocked || DialoguePanel.IsAnyPlaying) return;
            question.text = (pending.Peek() ? "已获得拉花设备" : "已获得精粹设备")
                + "\n\n是否进行简要操作教程？\n以后也可从咖啡谱或 GM 启动。";
            dialog.SetActive(true);
            notice.SetActive(false);
        }

        private void Choose(bool learn)
        {
            if (!IsOpen || pending.Count == 0 || app == null) return;
            bool latte = pending.Peek();
            // Clear our modal gate before entering the workstation or requesting the tutorial.
            Close();
            if (!learn)
            {
                pending.Dequeue();
                app.ShowHint("以后可从咖啡谱或 GM 启动设备操作教程。");
            }
            else
            {
                app.Enter(AreaId.Counter);
                if (app.World != null && app.World.State.currentArea == AreaId.Counter
                    && app.StartEquipmentTutorial(latte))
                    pending.Dequeue();
                else
                {
                    // A failed handoff is not a dismissal. Preserve the purchase and allow retry later.
                    retryAfterFrame = Time.frameCount + 1;
                    app.ShowHint("暂时无法开始教程，请稍后重试；也可从咖啡谱或 GM 启动。");
                }
            }
            UpdateNotice();
        }

        private void Close()
        {
            if (dialog != null) dialog.SetActive(false);
        }

        private void UpdateNotice()
        {
            if (notice == null) return;
            bool visible = pending.Count > 0 && !IsOpen;
            notice.SetActive(visible);
            if (visible)
                notification.text = (pending.Peek() ? "已获得拉花设备" : "已获得精粹设备")
                    + " · 简要教程已排队\n关闭咖啡谱 / 设备面板，结束当前操作后可选择学习。";
        }

        private void OnEnable()
        {
            if (ui != null) ui.SetActive(true);
            UpdateNotice();
        }

        private void OnDisable()
        {
            // Keep the queue and subscription while disabled so successful purchases are not lost.
            Close();
            if (ui != null) ui.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (ui != null) Destroy(ui);
        }

        private void EnsureUI()
        {
            if (ui != null) return;
            font = Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF");
            ui = new GameObject("EquipmentTutorialPromptCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32761;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            notice = Panel(ui.transform, "PurchaseNotice", new Color(.12f, .1f, .085f, .96f), false);
            var nr = (RectTransform)notice.transform;
            nr.anchorMin = nr.anchorMax = new Vector2(.5f, 1);
            nr.pivot = new Vector2(.5f, 1);
            nr.anchoredPosition = new Vector2(0, -16);
            nr.sizeDelta = new Vector2(1040, 94);
            var noticeGroup = notice.AddComponent<CanvasGroup>();
            noticeGroup.blocksRaycasts = false;
            noticeGroup.interactable = false;
            notification = Text(notice.transform, "Message", new Vector2(1000, 86), Vector2.zero, 25);

            dialog = Panel(ui.transform, "QuestionShade", new Color(0, 0, 0, .68f), true);
            var dr = (RectTransform)dialog.transform;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = dr.offsetMax = Vector2.zero;
            var group = dialog.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            var card = Panel(dialog.transform, "QuestionCard", new Color(.12f, .1f, .085f, 1), true);
            ((RectTransform)card.transform).sizeDelta = new Vector2(900, 470);
            question = Text(card.transform, "Question", new Vector2(820, 260), new Vector2(0, 60), 32);
            MakeButton(card.transform, "LearnNow", "现在学习", new Vector2(-210, -150), () => Choose(true));
            MakeButton(card.transform, "Later", "以后再说", new Vector2(210, -150), () => Choose(false));
            dialog.SetActive(false);
            notice.SetActive(false);
            ui.SetActive(isActiveAndEnabled);
        }

        private static GameObject Panel(Transform parent, string name, Color color, bool blocksRaycasts)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksRaycasts;
            return go;
        }

        private TMP_Text Text(Transform parent, string name, Vector2 size, Vector2 position, float pointSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = pointSize;
            text.color = new Color(1, .94f, .8f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        private void MakeButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var go = Panel(parent, name, new Color(.31f, .22f, .15f), true);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(360, 70);
            rect.anchoredPosition = position;
            Text(go.transform, "Label", new Vector2(340, 62), Vector2.zero, 28).text = label;
            go.AddComponent<Button>().onClick.AddListener(action);
        }
    }
}
