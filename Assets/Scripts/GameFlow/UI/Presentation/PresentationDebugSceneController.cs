using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YokiFrame;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Scene-owned preview harness; never starts gameplay, advances the calendar or writes saves.</summary>
    public sealed class PresentationDebugSceneController : MonoBehaviour
    {
        public const string DebugTag = "presentation-debug";
        public const string ScenePath = "Assets/Scenes/PresentationDebugScene.unity";
        public const string DefaultHint = "按键/按钮可随时切换演出。Esc 关闭 · Backspace 重播 · H 收起说明 · F9 切换暂停时间。";

        public enum Preview { DateChange, Letter, Dialogue, PreBusiness, CafeOpening, BusinessEnd, CityMap, PreNight, HomeNight, DayEnd, Introduction, Effects, Reading, Notice, CutScene, HardcodedDialogue, HardcodedLetter, HardcodedOptions, Details, LongLoading, ShortLoading, GameOver, OpeningPrologue }

        [Header("Scene bindings")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text activeText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private CanvasGroup helpGroup;
        [Header("Hard-coded test data (not game state)")]
        [SerializeField] private string previousDate = "王国纪元 670年10月6日";
        [SerializeField] private string currentDate = "王国纪元 670年10月7日";
        [SerializeField] private bool enableKeyboardShortcuts = true;

        [Header("NCS content selection (read-only; no event settlement)")]
        [SerializeField] private string openingPresentationId = NarrativePresentationDataAdapter.DefaultOpeningPresentationId;
        [SerializeField] private string letterBodyKey = NarrativePresentationDataAdapter.DefaultFirstDayLetterBodyKey;
        [SerializeField] private string letterSignatureKey = NarrativePresentationDataAdapter.DefaultFirstDayLetterSignatureKey;

        [Header("Isolated preview context (never saved)")]
        [SerializeField] private bool previewAllPages;
        [SerializeField] private bool previewUnlockChoices;
        [SerializeField] private int previewAttributeValue = 3;
        [SerializeField] private string previewPlayerName = "馆长";
        [SerializeField] private bool hideHelpDuringPlayback = true;

        private PresentationDebugSession configuredSession;
        private bool restoreHelpAfterPlayback;
        private UIPanel currentPanel;
        private Coroutine pendingEffectCue;
        private Coroutine loadingPreviewRoutine;
        private bool alternateBackdrop;
        private int requestVersion;
        private int startedFrame;
        private float originalTimeScale;
        private bool hasTimeOwnership;
        private bool samplePaused;
        private bool hasLastPreview;
        private Preview lastPreview;
        private readonly PresentationDebugDialogueMenu dialogueMenu = new PresentationDebugDialogueMenu();
        private string selectedDialogueId;

        public bool IsDialogueMenuOpen => dialogueMenu.IsOpen;
        public string SelectedDialogueId => selectedDialogueId;

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
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (currentPanel is OpeningProloguePanel && ((OpeningProloguePanel)currentPanel).IsPlaying) return;
                StopCurrent();
                return;
            }
            if (dialogueMenu.IsOpen) return;
            if (keyboard.hKey.wasPressedThisFrame) { ToggleHelp(); return; }
            if (keyboard.f9Key.wasPressedThisFrame) { ToggleTimePause(); return; }
            if (keyboard.f12Key.wasPressedThisFrame) { ResetHelp(); return; }
            if (keyboard.backspaceKey.wasPressedThisFrame) { ReplayLast(); return; }
            bool draftPreview = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (keyboard.qKey.wasPressedThisFrame) PlayDateChange();
            else if (keyboard.wKey.wasPressedThisFrame) { if (draftPreview) PlayHardcodedLetter(); else PlayLetter(); }
            else if (keyboard.eKey.wasPressedThisFrame) { if (draftPreview) PlayHardcodedDialogue(); else PlayDialogue(); }
            else if (keyboard.cKey.wasPressedThisFrame) PlayDialogueOptions();
            else if (keyboard.vKey.wasPressedThisFrame) PlayReading();
            else if (keyboard.bKey.wasPressedThisFrame) PlayNotice();
            else if (keyboard.nKey.wasPressedThisFrame) PlayCutScene();
            else if (keyboard.rKey.wasPressedThisFrame) PlayPreBusiness();
            else if (keyboard.tKey.wasPressedThisFrame) PlayCafeOpening();
            else if (keyboard.yKey.wasPressedThisFrame) PlayBusinessEnd();
            else if (keyboard.uKey.wasPressedThisFrame) PlayCityMap();
            else if (keyboard.iKey.wasPressedThisFrame) PlayPreNight();
            else if (keyboard.oKey.wasPressedThisFrame) PlayHomeNight();
            else if (keyboard.pKey.wasPressedThisFrame) PlayDayEnd();
            else if (keyboard.fKey.wasPressedThisFrame) PlayIntroduction();
            else if (keyboard.zKey.wasPressedThisFrame) PlayOpeningPrologue();
            else if (keyboard.gKey.wasPressedThisFrame) PlayEffects();
            else if (keyboard.lKey.wasPressedThisFrame) PlayLongLoading();
            else if (keyboard.kKey.wasPressedThisFrame) PlayShortLoading();
            else if (keyboard.mKey.wasPressedThisFrame) PlayDetails();
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

        public void PlayGameOver() => PlayPreview(Preview.GameOver);
        public void PlayOpeningPrologue() => PlayPreview(Preview.OpeningPrologue);
        public void PlayLongLoading() => PlayPreview(Preview.LongLoading);
        public void PlayShortLoading() => PlayPreview(Preview.ShortLoading);
        public void PlayDetails() => PlayPreview(Preview.Details);
        public void PlayDateChange() => PlayPreview(Preview.DateChange);
        public void PlayLetter() => PlayPreview(Preview.Letter);
        public void PlayDialogue() => ShowDialogueMenu();
        // Keep serialized UnityEvent method names, but no longer depend on removed sample characters.
        public void PlayHardcodedDialogue() => ShowDialogueMenu();
        public void PlayDialogueOptions() => ShowDialogueMenu();
        public void PlayHardcodedLetter() => PlayPreview(Preview.HardcodedLetter);
        public void PlayReading() => PlayPreview(Preview.Reading);
        public void PlayNotice() => PlayPreview(Preview.Notice);
        public void PlayCutScene() => PlayPreview(Preview.CutScene);
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
            selectedDialogueId = null;
            lastPreview = preview;
            hasLastPreview = true;
            int version = ++requestVersion;
            startedFrame = Time.frameCount;
            try
            {
                // Panels own their preview data; even letter samples must not start the live NCS runtime.
                switch (preview)
                {
                    case Preview.GameOver:
                        currentPanel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop,
                            new ResultPanelData(null, "游戏结束", "每日交付已完成。你的选择让这段旅程迎来了结局。", () => Complete(version), "返回主菜单"),
                            tag: DebugTag, cachePolicy: PanelCachePolicy.Transient);
                        break;
                    case Preview.OpeningPrologue:
                        Open<OpeningProloguePanel>().Play(() => Complete(version));
                        break;
                    case Preview.DateChange:
                        var date = Open<DateChangePanel>();
                        date.SetDates(previousDate, currentDate);
                        date.Play(() => Complete(version));
                        break;
                    case Preview.Letter:
                        var letter = Open<FirstDayLetterPanel>();
                        // 与正式开场一致，默认预览硬编码互动信件。
                        letter.Play(() => Complete(version));
                        break;
                    case Preview.Dialogue:
                    case Preview.HardcodedDialogue:
                    case Preview.HardcodedOptions:
                    case Preview.Effects:
                    case Preview.Introduction:
                        selectedDialogueId = "opening:" + openingPresentationId;
                        StartConfiguredSession(selectedDialogueId, version);
                        break;
                    case Preview.HardcodedLetter: Open<FirstDayLetterPanel>().Play(() => Complete(version)); break;
                    case Preview.Reading: PlayReadingSample(version); break;
                    case Preview.Notice: PlayNoticeSample(version); break;
                    case Preview.CutScene: PlayCutSceneSample(version); break;
                    case Preview.PreBusiness: Open<PreBusinessPanel>().Play(() => Complete(version)); break;
                    case Preview.CafeOpening: Open<CafeOpeningPanel>().Play(() => Complete(version)); break;
                    case Preview.BusinessEnd: Open<BusinessEndPanel>().Play(() => Complete(version)); break;
                    case Preview.CityMap: Open<CityMapPanel>().Play(() => Complete(version)); break;
                    case Preview.PreNight: Open<PreNightPanel>().Play(() => Complete(version)); break;
                    case Preview.HomeNight: Open<HomeNightPanel>().Play(() => Complete(version)); break;
                    case Preview.DayEnd: Open<DayEndPanel>().Play(() => Complete(version)); break;
                    case Preview.LongLoading:
                        var longLoading = Open<LoadingPanel>();
                        longLoading.BeginLoading();
                        longLoading.SetTip("调试加载：咖啡杯、横条和百分比同步变化；不会加载场景。");
                        loadingPreviewRoutine = StartCoroutine(LongLoadingSample(longLoading, version));
                        break;
                    case Preview.ShortLoading:
                        var shortLoading = Open<ShortLoadingPanel>();
                        loadingPreviewRoutine = StartCoroutine(ShortLoadingSample(shortLoading, version));
                        break;
                    case Preview.Details:
                    var details = Open<DetailsPanel>();
                    details.Refresh(DetailsPanelData.CreatePlaceholder());
                    details.Preview(() => Complete(version));
                    break;
                    default: throw new ArgumentOutOfRangeException(nameof(preview));
                }
                SetStatus("播放中", preview.ToString());
                SetHint(preview == Preview.Letter || preview == Preview.HardcodedLetter ? "点击信封 → 阅读 → 点击署名。面板右下角可跳过；Esc 取消。"
                    : preview == Preview.HardcodedOptions ? "C：选项对话（8 句）；点击 SKIP 快速逐条播放，到选项处停止；空格/回车补全文字。灰色选项不可用；仅演示，不结算 NCS。Esc 取消。"
                    : preview == Preview.Dialogue || preview == Preview.HardcodedDialogue || preview == Preview.Introduction || preview == Preview.Effects ? "空格/回车/面板继续按钮：先补全文字，再换页/换句。Esc 取消。"
                    : preview == Preview.Details ? "M：详情面板；点击页签切换，悬停属性/专精查看说明。关闭按钮或窗口外可关闭；全部为占位。"
                    : preview == Preview.Reading ? "V：阅读；空格/回车补全文字 → 下一行/页，← 或上一页返回；Esc 取消。"
                    : preview == Preview.Notice ? "B：通知演出；空格/回车或确认按钮关闭；Esc 取消。"
                    : preview == Preview.CutScene ? "N：视频过场；当前无视频时显示占位；长按 Enter 跳过。Esc 取消。"
                    : preview == Preview.LongLoading ? "L：长加载，0% → 100% 后自动结束。空格/回车提前完成；F9 验证暂停；H 收起侧栏；Esc 取消。"
                    : preview == Preview.ShortLoading ? "K：右上 → 左下覆盖，咖啡抖动 2 秒后同方向消失。空格/回车提前结束；F9 验证暂停；H 收起侧栏；Esc 取消。"
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

        private IEnumerator LongLoadingSample(LoadingPanel panel, int version)
        {
            yield return null;
            while (version == requestVersion && panel != null && panel.State == PanelState.Opening)
                yield return null;
            float elapsed = 0f;
            // Hold both endpoints so the dark and fully lit cup can be inspected.
            while (version == requestVersion && panel != null && elapsed < 5.65f)
            {
                elapsed += Time.unscaledDeltaTime;
                panel.SetProgress(Mathf.Clamp01((elapsed - .65f) / 4f));
                yield return null;
            }
            loadingPreviewRoutine = null;
            if (version == requestVersion && panel != null)
                panel.EndLoading(() => Complete(version));
        }

        private IEnumerator ShortLoadingSample(ShortLoadingPanel panel, int version)
        {
            yield return null;
            while (version == requestVersion && panel != null && !panel.IsCovered)
                yield return null;
            if (version != requestVersion || panel == null) yield break;
            SetStatus("已遮挡 / 模拟加载", "ShortLoading");
            float elapsed = 0f;
            while (version == requestVersion && panel != null && elapsed < 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            loadingPreviewRoutine = null;
            if (version == requestVersion && panel != null)
                panel.EndLoading(() => Complete(version));
        }

        private T Open<T>() where T : UIPanel
        {
            T panel = UIKit.OpenPanel<T>(UILevel.Pop, tag: DebugTag, cachePolicy: PanelCachePolicy.Transient);
            currentPanel = panel;
            return panel;
        }

        public void ShowDialogueMenu()
        {
            StopCurrent();
            try
            {
                // Discover generated entries without starting the live runtime; results execute only in a selected sandbox sequence.
                var configuration = LubanNarrativeLoader.Load();
                var entries = new List<PresentationDebugCatalog.Entry>(PresentationDebugCatalog.ListSequences(configuration));
                var knownIds = new HashSet<string>(entries.ConvertAll(entry => entry.Id), StringComparer.Ordinal);
                foreach (var entry in PresentationDebugCatalog.List(configuration))
                    if (knownIds.Add(entry.Id)) entries.Add(entry);
                var ids = new List<string>(entries.Count);
                var labels = new List<string>(entries.Count);
                foreach (var entry in entries) { ids.Add(entry.Id); labels.Add(entry.Label); }
                dialogueMenu.Show(transform, statusText != null ? statusText.font : TMP_Settings.defaultFontAsset,
                    ids, labels, PlayConfiguredDialogue, StopCurrent);
                SetStatus("选择连续演出", "配表入口 / " + entries.Count);
                SetHint("自动读取已生成配表。选择入口后连续播放，选项与检定沿用正式调度；结果只作用于临时预览状态，不影响正式游戏或存档。");
            }
            catch (Exception error) { ReportDialogueError(error); }
        }

        public void PlayConfiguredDialogue(string id)
        {
            StopCurrent();
            selectedDialogueId = id;
            lastPreview = Preview.Dialogue;
            hasLastPreview = true;
            int version = ++requestVersion;
            startedFrame = Time.frameCount;
            try
            {
                StartConfiguredSession(id, version);
                if (version != requestVersion || configuredSession == null || !configuredSession.IsPresenting) return;
                SetStatus("播放中", configuredSession.CurrentEventId ?? id);
                SetHint("隔离预览：H 显示控制台，Esc 停止整段，Backspace 从入口重播。连续演出跟随选项/检定跳转；所有结果只作用于临时状态。" +
                    (previewAllPages ? " 显示全部条件页。" : " 按预览状态筛页。") +
                    (previewUnlockChoices ? " 选项全解锁。" : " 按预览状态锁定选项。"));
            }
            catch (Exception error) { ReportDialogueError(error); }
        }

        private void StartConfiguredSession(string id, int version)
        {
            configuredSession = new PresentationDebugSession(LubanNarrativeLoader.Load(), DebugTag,
                previewAllPages, previewUnlockChoices, previewAttributeValue, previewPlayerName,
                panel => { if (version == requestVersion) currentPanel = panel; },
                reason => { if (version == requestVersion) { StopCurrent(); SetStatus("已停止", id); SetHint("演出已停止：" + reason); } },
                followEventChain: id.StartsWith("sequence:", StringComparison.Ordinal));
            configuredSession.EventStarted += eventId => { if (version == requestVersion) SetStatus("连续演出中", eventId); };
            if (hideHelpDuringPlayback && HelpVisible)
            {
                restoreHelpAfterPlayback = true;
                ToggleHelp();
            }
            configuredSession.Play(id, () => Complete(version));
        }

        private void RestoreHelp()
        {
            if (!restoreHelpAfterPlayback) return;
            restoreHelpAfterPlayback = false;
            if (!HelpVisible) ToggleHelp();
        }

        private void ReportDialogueError(Exception error)
        {
            StopCurrent();
            SetStatus("失败", selectedDialogueId ?? "配表对话");
            SetHint(error.Message);
            Debug.LogException(error, this);
        }

        private void PlayReadingSample(int version)
        {
            Texture background = Resources.Load<Texture2D>("Art/Gameplay/DayFlow/phase_business_transition");
            var character = NarrativeCharacterResolver.CreateDialogueCharacter("adele");
            var pages = new[]
            {
                new ReadingPage("清晨的街道", new[]
                {
                    new ReadingLine("雨后的石板路还留着水光。", "旁白"),
                    new ReadingLine("咖啡馆的门牌在风里轻轻摇晃。", "旁白")
                }, background, pageId: "debug-reading-01", seq: 1),
                new ReadingPage("一张便笺", new[]
                {
                    new ReadingLine("请在午后之前，去旧城区找我。", character.DisplayName,
                        portrait: character.Portrait, kind: ReadingLineKind.Dialogue),
                    new ReadingLine("落款只有一个熟悉的字母。", "旁白")
                }, pageId: "debug-reading-02", seq: 2)
            };
            Open<ReadingPanel>().Play(new ReadingScript(pages, "阅读演出测试"), () => Complete(version), () => Cancelled(version));
        }

        private void PlayNoticeSample(int version)
        {
            Texture background = Resources.Load<Texture2D>("Art/Gameplay/DayFlow/phase_business_transition");
            var notice = new NoticeData("旧城区的风向发生了变化。今天的情报交换窗口已开启。",
                title: "事件通知", time: "王国纪元 670年10月7日 · 08:30", image: background,
                hasProgress: true, progressValue: .62f, progressLabel: "街区舆情：62%（仅展示提示）", noticeId: "debug-notice-01");
            Open<NoticePanel>().Play(notice, () => Complete(version), () => Cancelled(version));
        }

        private void PlayCutSceneSample(int version)
        {
            var cutScene = new CutSceneData(title: "过场演出测试", videoPath: string.Empty, previewOnly: true);
            Open<CutScenePanel>().Play(cutScene, () => Complete(version), () => Cancelled(version));
        }

        private void Cancelled(int version)
        {
            if (version != requestVersion) return;
            currentPanel = null;
            RestoreHelp();
            SetStatus("已取消", selectedDialogueId ?? lastPreview.ToString());
            SetHint(DefaultHint);
        }

        private void TryApplyNcsLetter(FirstDayLetterPanel panel)
        {
            try
            {
                NarrativeRuntime runtime = NarrativeRuntime.Instance;
                bool resolved;
                string error;
                FirstDayLetterContent content;
                if (runtime != null && runtime.IsReady)
                    resolved = NarrativePresentationDataAdapter.TryResolveFirstDayLetter(runtime, letterBodyKey, letterSignatureKey, null, out content, out error);
                else
                    resolved = NarrativePresentationDataAdapter.TryResolveFirstDayLetter(LubanNarrativeLoader.Load(), letterBodyKey, letterSignatureKey, null, out content, out error);
                if (resolved) panel.SetContent(content);
                else Debug.LogWarning("[PresentationDebug] " + error + "; using panel draft fallback.", this);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[PresentationDebug] NCS 信件使用 Panel 草稿：" + error.Message, this);
            }
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
            RestoreHelp();
            SetStatus("已完成", selectedDialogueId ?? lastPreview.ToString());
            SetHint(DefaultHint);
        }

        public void StopCurrent()
        {
            dialogueMenu.Close();
            requestVersion++;
            configuredSession?.Dispose();
            configuredSession = null;
            RestoreHelp();
            if (loadingPreviewRoutine != null) { StopCoroutine(loadingPreviewRoutine); loadingPreviewRoutine = null; }
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
            if (!hasLastPreview) return;
            if (!string.IsNullOrEmpty(selectedDialogueId)) PlayConfiguredDialogue(selectedDialogueId);
            else PlayPreview(lastPreview);
        }

        public void ContinueCurrent()
        {
            if (Time.frameCount == startedFrame || currentPanel == null) return;
            if (currentPanel is LoadingPanel || currentPanel is ShortLoadingPanel)
            {
                if (loadingPreviewRoutine != null) { StopCoroutine(loadingPreviewRoutine); loadingPreviewRoutine = null; }
                int version = requestVersion;
                if (currentPanel is LoadingPanel longLoading) longLoading.EndLoading(() => Complete(version));
                else if (currentPanel is ShortLoadingPanel shortLoading) shortLoading.EndLoading(() => Complete(version));
            }
            else if (currentPanel is DayFlowPresentationPanel day) day.Continue();
            else if (currentPanel is FirstDayLetterPanel letter)
            {
                if (letter.Stage == FirstDayLetterPanel.LetterStage.Envelope) letter.OpenEnvelope();
                else if (letter.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature) letter.ConfirmSignature();
            }
            else if (currentPanel is OpeningProloguePanel prologue) prologue.Continue();
            // Dialogue/Reading/Notice panels own their keyboard input to preserve reveal/advance and same-frame deduplication.
        }

        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (!enableKeyboardShortcuts || keyboard == null || dialogueMenu.IsOpen) return;
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
