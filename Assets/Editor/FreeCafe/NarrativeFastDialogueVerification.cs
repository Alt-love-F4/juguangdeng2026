using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Manual functional probe in the existing shared scene, using the real presenter/controller transaction.</summary>
    public static class NarrativeFastDialogueVerification
    {
        [MenuItem("Tools/Narrative/Walkthrough/Verify Instant Dialogue Settlement")]
        private static void Begin()
        {
            var host = UnityEngine.Object.FindFirstObjectByType<PresentationDebugSceneController>();
            if (!EditorApplication.isPlaying || host == null || SceneManager.GetActiveScene().name != "PresentationDebugScene")
            { Debug.LogWarning("请在既有 PresentationDebugScene 的 Play Mode 中运行此核对。"); return; }
            host.StopCurrent();
            host.StartCoroutine(Verify());
        }
        [Serializable] private sealed class Result
        {
            public bool passed, selected, lockedRejected, duplicateRejected, tailCompleted;
            public int before, afterSelection, after, selections, renderedPages;
            public string error;
            public List<string> changes = new List<string>();
        }
        private static IEnumerator Verify()
        {
            var result = new Result();
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "verify.skill", min = 0, max = 20, initValue = 3 });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "verify.talk", atomSeq = "A_DIALOG" });
            config.events.Add(new NarrativeEventDefinition { eventId = "verify.root", presentationKey = "verify", presentationClass = "conversation", setupType = "verify.talk" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "verify", presentationClass = "conversation" });
            foreach (string page in new[] { "before", "choose", "tail" })
            {
                int seq = page == "before" ? 1 : page == "choose" ? 2 : 3;
                config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition {
                    presentationKey = "verify", pageId = page, seq = seq, optionGroupKey = page == "choose" ? "verify.q" : null });
                for (int i = 0; i < 30; i++) config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition {
                    pageId = page, lineId = page + i, seq = i, textKey = "核对台词 " + page + " / " + i, side = "narrator" });
            }
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "verify.yes", groupKey = "verify.q", textKey = "选择后真实加两点", resultOwnerId = "verify.reward", seq = 1 });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "verify.locked", groupKey = "verify.q", textKey = "不可选", seq = 2 });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "verify.closed" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "verify.lock", ownerType = "choice", ownerId = "verify.locked", groupIndex = 1, condType = "BOOL_EQ", target = "verify.closed", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "verify.plus", eventId = "verify.reward", resultType = NarrativeResultType.BarAdd, target = "verify.skill", value = "2" });
            var presenter = new NarrativePresenter(config) { SkipDialogueText = true };
            var core = new NarrativeController(config, presenter);
            presenter.ChoiceSelected += core.SelectChoice;
            presenter.ChoiceUnlocked = (id, context) => core.EvaluateConditions("choice", id, context);
            presenter.PageCondition = core.ReadPresentationCondition;
            presenter.ConversationPlayback = (request, data, done) => {
                result.renderedPages++;
                return NarrativeConversationEventPlayback.PlayPreview(request, data, done);
            };
            core.BarChanged += (key, before, after) => result.changes.Add(key + ":" + before + "->" + after);
            core.EventCompleted += id => result.tailCompleted = id == "verify.root";
            Action<string, string> failed = (id, error) => result.error = id + ":" + error;
            core.EventFailed += failed;
            core.State.TryGetBar("verify.skill", out result.before);
            try
            {
                core.QueueEvent("verify.root");
                float deadline = Time.realtimeSinceStartup + 12f;
                while (!result.tailCompleted && string.IsNullOrEmpty(result.error) && Time.realtimeSinceStartup < deadline)
                {
                    var panel = DialoguePanel.Active;
                    if (panel != null && panel.IsPlaying)
                    {
                        panel.FastForwardToChoiceOrComplete();
                        if (panel.IsChoosing)
                        {
                            result.lockedRejected = !panel.SelectOption("verify.locked");
                            result.selected = panel.SelectOption("verify.yes");
                            if (result.selected) result.selections++;
                            core.State.TryGetBar("verify.skill", out result.afterSelection);
                            result.duplicateRejected = !panel.SelectOption("verify.yes");
                        }
                    }
                    yield return null;
                }
                core.State.TryGetBar("verify.skill", out result.after);
                result.passed = result.before == 3 && result.afterSelection == 3 && result.after == 5 && result.selected && result.selections == 1
                    && result.lockedRejected && result.duplicateRejected && result.renderedPages == 1 && result.tailCompleted && string.IsNullOrEmpty(result.error);
            }
            finally
            {
                core.EventFailed -= failed;
                presenter.Dispose();
                core.UnbindValueProviders();
                DialoguePlayback.Stop();
                string directory = Path.GetFullPath("Library/NarrativeWalkthrough");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "instant-settlement.json"), JsonUtility.ToJson(result, true));
                Debug.Log("Instant dialogue settlement: " + JsonUtility.ToJson(result));
            }
        }
    }
}
