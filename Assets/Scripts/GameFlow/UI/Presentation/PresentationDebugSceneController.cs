using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Scene-owned preview harness; never starts gameplay, advances the calendar or writes saves.</summary>
    public sealed class PresentationDebugSceneController : MonoBehaviour
    {
        public const string DebugTag = "presentation-debug";
        public const string ScenePath = "Assets/Scenes/PresentationDebugScene.unity";
        public const string DefaultHint = "按键/按钮可随时切换演出。Esc 关闭 · Backspace 重播 · H 收起说明 · F9 切换暂停时间。";

        public enum Preview { DateChange, Letter, Dialogue, PreBusiness, CafeOpening, BusinessEnd, CityMap, PreNight, HomeNight, DayEnd, Introduction, Effects }

        [Header("Scene bindings")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text activeText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private CanvasGroup helpGroup;
        [Header("Hard-coded test data (not game state)")]
        [SerializeField] private string previousDate = "王国纪元 670年10月6日";
        [SerializeField] private string currentDate = "王国纪元 670年10月7日";
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private UIPanel currentPanel;
        private Coroutine pendingEffectCue;
        private bool alternateBackdrop;
        private int requestVersion;
        private int startedFrame;
        private float originalTimeScale;
        private bool hasTimeOwnership;
        private bool samplePaused;
        private bool hasLastPreview;
        private Preview lastPreview;

        public UIPanel CurrentPanel => currentPanel;
        public bool HelpVisible => helpGroup == null || helpGroup.alpha > 0f;
        public bool IsSamplePaused => samplePaused;

        private void OnEnable()
        {
            originalTimeScale = Time.timeScale;
            hasTimeOwnership = true;
            SetStatus("待机", "无");
            SetHint(DefaultHint);
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            // Single owner for launch/stop; each actual panel continues to handle its own dialogue/letter input.
            if (keyboard.escapeKey.wasPressedThisFrame) { StopCurrent(); return; }
            if (keyboard.hKey.wasPressedThisFrame) { ToggleHelp(); return; }
            if (keyboard.f9Key.wasPressedThisFrame) { ToggleTimePause(); return; }
            if (keyboard.f12Key.wasPressedThisFrame) { ResetHelp(); return; }
            if (keyboard.backspaceKey.wasPressedThisFrame) { ReplayLast(); return; }
            if (keyboard.qKey.wasPressedThisFrame) PlayDateChange();
            else if (keyboard.wKey.wasPressedThisFrame) PlayLetter();
            else if (keyboard.eKey.wasPressedThisFrame) PlayDialogue();
            else if (keyboard.rKey.wasPressedThisFrame) PlayPreBusiness();
            else if (keyboard.tKey.wasPressedThisFrame) PlayCafeOpening();
            else if (keyboard.yKey.wasPressedThisFrame) PlayBusinessEnd();
            else if (keyboard.uKey.wasPressedThisFrame) PlayCityMap();
            else if (keyboard.iKey.wasPressedThisFrame) PlayPreNight();
            else if (keyboard.oKey.wasPressedThisFrame) PlayHomeNight();
            else if (keyboard.pKey.wasPressedThisFrame) PlayDayEnd();
            else if (keyboard.fKey.wasPressedThisFrame) PlayIntroduction();
            else if (keyboard.gKey.wasPressedThisFrame) PlayEffects();
            else if (keyboard.digit1Key.wasPressedThisFrame) PreviewEnter();
            else if (keyboard.digit2Key.wasPressedThisFrame) PreviewStartle();
            else if (keyboard.digit3Key.wasPressedThisFrame) PreviewShakeLight();
            else if (keyboard.digit4Key.wasPressedThisFrame) PreviewShakeHeavy();
            else if (keyboard.digit5Key.wasPressedThisFrame) PreviewFlash();
            else if (keyboard.digit6Key.wasPressedThisFrame) PreviewBlack();
            else if (keyboard.digit7Key.wasPressedThisFrame) PreviewSwap();
            else if (keyboard.digit8Key.wasPressedThisFrame) PreviewZoom();
            else if (keyboard.digit9Key.wasPressedThisFrame) PreviewResetCamera();
            else if (keyboard.digit0Key.wasPressedThisFrame) PreviewExit();
        }

        public void PlayDateChange() => PlayPreview(Preview.DateChange);
        public void PlayLetter() => PlayPreview(Preview.Letter);
        public void PlayDialogue() => PlayPreview(Preview.Dialogue);
        public void PlayPreBusiness() => PlayPreview(Preview.PreBusiness);
        public void PlayCafeOpening() => PlayPreview(Preview.CafeOpening);
        public void PlayBusinessEnd() => PlayPreview(Preview.BusinessEnd);
        public void PlayCityMap() => PlayPreview(Preview.CityMap);
        public void PlayPreNight() => PlayPreview(Preview.PreNight);
        public void PlayHomeNight() => PlayPreview(Preview.HomeNight);
        public void PlayDayEnd() => PlayPreview(Preview.DayEnd);
        public void PlayIntroduction() => PlayPreview(Preview.Introduction);
        public void PlayEffects() => PlayPreview(Preview.Effects);
        public void PreviewEnter() => PreviewEffectCue(1);
        public void PreviewStartle() => PreviewEffectCue(2);
        public void PreviewShakeLight() => PreviewEffectCue(3);
        public void PreviewShakeHeavy() => PreviewEffectCue(4);
        public void PreviewFlash() => PreviewEffectCue(5);
        public void PreviewBlack() => PreviewEffectCue(6);
        public void PreviewSwap() => PreviewEffectCue(7);
        public void PreviewZoom() => PreviewEffectCue(8);
        public void PreviewResetCamera() => PreviewEffectCue(9);
        public void PreviewExit() => PreviewEffectCue(0);

        private void PreviewEffectCue(int cue)
        {
            if (!(currentPanel is DialoguePanel panel) || !panel.IsPlaying) PlayEffects();
            if (pendingEffectCue != null) StopCoroutine(pendingEffectCue);
            pendingEffectCue = StartCoroutine(EffectCueWhenReady(cue, requestVersion));
        }
        private IEnumerator EffectCueWhenReady(int cue, int version)
        {
            yield return null;
            DialoguePanel panel = currentPanel as DialoguePanel;
            while (version == requestVersion && panel != null && panel.IsPlaying && !panel.IsReady) yield return null;
            pendingEffectCue = null;
            if (version != requestVersion || panel == null || !panel.IsPlaying) yield break;
            switch (cue)
            {
                case 1: panel.PlayEffect(PresentationEffectIds.EnterShadowWalk, "left", true); break;
                case 2: panel.PlayEffect(PresentationEffectIds.AccentShakeStartle, "right"); break;
                case 3: panel.PlayEffect(PresentationEffectIds.ShakeLight); break;
                case 4: panel.PlayEffect(PresentationEffectIds.ShakeHeavy); break;
                case 5: panel.PlayEffect(PresentationEffectIds.FlashWhite); break;
                case 6:
                    panel.PlayFade("transition", .6f, () =>
                    {
                        alternateBackdrop = !alternateBackdrop;
                        panel.SetBackgroundTint(alternateBackdrop ? new Color(.45f, .5f, .65f) : Color.white);
                    }); break;
                case 7: panel.SwapPortraits(); break;
                case 8: panel.PlayCameraCue(1.08f, 0f, .35f, new Vector2(35f, -8f)); break;
                case 9: panel.PlayCameraCue(); break;
                case 0: panel.PlayEffect(PresentationEffectIds.PortraitFadeOut, "right", true); break;
            }
            SetHint("特效 " + cue + " 已触发。1~0 独立测试；角色/画面/镜头可并行，重震屏优先于轻震屏。");
        }

        /// <summary>Add new hard-coded presentation samples here, not in a new scene or gameplay bootstrap.</summary>
        public void PlayPreview(Preview preview)
        {
            StopCurrent();
            lastPreview = preview;
            hasLastPreview = true;
            int version = ++requestVersion;
            startedFrame = Time.frameCount;
            try
            {
                switch (preview)
                {
                    case Preview.DateChange:
                        var date = Open<DateChangePanel>();
                        date.SetDates(previousDate, currentDate);
                        date.Play(() => Complete(version));
                        break;
                    case Preview.Letter: Open<FirstDayLetterPanel>().Play(() => Complete(version)); break;
                    case Preview.Dialogue: PlayDialogueSample(DialoguePreviewDialogue.Create(), version); break;
                    case Preview.Effects: PlayDialogueSample(PresentationEffectsPreview.Create(), version); break;
                    case Preview.Introduction: PlayDialogueSample(new LubanOpeningDialogueSource().CreateScript(), version); break;
                    case Preview.PreBusiness: Open<PreBusinessPanel>().Play(() => Complete(version)); break;
                    case Preview.CafeOpening: Open<CafeOpeningPanel>().Play(() => Complete(version)); break;
                    case Preview.BusinessEnd: Open<BusinessEndPanel>().Play(() => Complete(version)); break;
                    case Preview.CityMap: Open<CityMapPanel>().Play(() => Complete(version)); break;
                    case Preview.PreNight: Open<PreNightPanel>().Play(() => Complete(version)); break;
                    case Preview.HomeNight: Open<HomeNightPanel>().Play(() => Complete(version)); break;
                    case Preview.DayEnd: Open<DayEndPanel>().Play(() => Complete(version)); break;
                    default: throw new ArgumentOutOfRangeException(nameof(preview));
                }
                SetStatus("播放中", preview.ToString());
                SetHint(preview == Preview.Letter ? "点击信封 → 阅读 → 点击署名。面板右下角可跳过；Esc 取消。"
                    : preview == Preview.Dialogue || preview == Preview.Introduction || preview == Preview.Effects ? "空格/回车/面板继续按钮：先补全文字，再换页/换句。Esc 取消。"
                    : preview == Preview.DateChange ? "日期动效自动完成；F9 可验证 timeScale=0 时仍正常播放。"
                    : "点击实际 Panel 的继续按钮，或空格/回车确认。Esc 取消；其他快捷键切换演出。");
            }
            catch (Exception error)
            {
                StopCurrent();
                SetStatus("失败", preview.ToString());
                SetHint(error.Message);
                Debug.LogException(error, this);
            }
        }

        private T Open<T>() where T : UIPanel
        {
            T panel = UIKit.OpenPanel<T>(UILevel.Pop, tag: DebugTag, cachePolicy: PanelCachePolicy.Transient);
            currentPanel = panel;
            return panel;
        }

        private void PlayDialogueSample(DialogueScript script, int version)
            => Open<DialoguePanel>().Play(script, () => Complete(version), () =>
            {
                if (version != requestVersion) return;
                currentPanel = null;
                SetStatus("已取消", lastPreview.ToString());
                SetHint(DefaultHint);
            });

        private void Complete(int version)
        {
            if (version != requestVersion) return;
            UIPanel panel = currentPanel;
            currentPanel = null;
            // Transient previews are destroyed on close; no debug panel/cache survives a scene change.
            if (panel != null) UIKit.ClosePanel(panel);
            SetStatus("已完成", lastPreview.ToString());
            SetHint(DefaultHint);
        }

        public void StopCurrent()
        {
            requestVersion++;
            if (pendingEffectCue != null) { StopCoroutine(pendingEffectCue); pendingEffectCue = null; }
            currentPanel = null;
            alternateBackdrop = false;
            // Only close the panels belonging to this scene, never a game-owned dialogue or another tag.
            UIKit.ClosePanelsByTag(DebugTag);
            SetStatus("待机", "无");
            SetHint(DefaultHint);
        }

        public void ReplayLast()
        {
            if (hasLastPreview) PlayPreview(lastPreview);
        }

        public void ContinueCurrent()
        {
            if (Time.frameCount == startedFrame || currentPanel == null) return;
            if (currentPanel is DayFlowPresentationPanel day) day.Continue();
            else if (currentPanel is FirstDayLetterPanel letter)
            {
                if (letter.Stage == FirstDayLetterPanel.LetterStage.Envelope) letter.OpenEnvelope();
                else if (letter.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature) letter.ConfirmSignature();
            }
            // DialoguePanel owns its keyboard input to preserve reveal/advance and same-frame deduplication.
        }

        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (!enableKeyboardShortcuts || keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) ContinueCurrent();
        }

        public void ToggleHelp()
        {
            if (helpGroup == null) return;
            bool show = !HelpVisible;
            helpGroup.alpha = show ? 1f : 0f;
            helpGroup.interactable = helpGroup.blocksRaycasts = show;
        }

        public void ResetHelp()
        {
            if (!HelpVisible) ToggleHelp();
            SetHint(DefaultHint);
        }

        public void ToggleTimePause()
        {
            samplePaused = !samplePaused;
            Time.timeScale = samplePaused ? 0f : originalTimeScale;
            SetHint("调试 timeScale = " + Time.timeScale + "。演出使用非缩放时间；再次 F9 恢复。");
        }

        private void OnDisable()
        {
            StopCurrent();
            if (hasTimeOwnership) Time.timeScale = originalTimeScale;
            hasTimeOwnership = samplePaused = false;
        }

        private void SetStatus(string status, string active)
        {
            if (statusText != null) statusText.text = "状态 / " + status;
            if (activeText != null) activeText.text = "当前 / " + active;
        }
        private void SetHint(string value) { if (hintText != null) hintText.text = value; }
    }
}
