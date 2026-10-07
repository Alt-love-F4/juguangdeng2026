using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed class NarrativePresenter : INarrativePresenter, INarrativePresentationDrain, IDisposable
    {
        private readonly NarrativeConfiguration configuration;
        private readonly Func<string, string> resolve;
        private readonly NarrativePresentationUI ui;
        private readonly PresentationResources resources;
        private readonly Queue<NarrativeSetupParamsDefinition> effects = new Queue<NarrativeSetupParamsDefinition>();
        private Action completion;
        private Action drainCompleted;
        private Coroutine routine;
        private NarrativeEventRequest request;
        private bool draining, disposed, skipRequested, allowSkip, choosing;
        private int pageIndex;
        private bool choicesEverShown, continueEverShown, finishPresentation;
        private readonly List<string> pages = new List<string>();

        public bool IsPresenting => completion != null || draining || effects.Count > 0;
        public event Action<NarrativeEventRequest> Started;
        public event Action<NarrativeEventRequest> Finished;
        public event Func<string, bool> ChoiceSelected;
        public Func<string, NarrativeConditionContext, bool> ChoiceUnlocked { get; set; }

        public NarrativePresenter(NarrativeConfiguration configuration = null, Func<string, string> textResolver = null)
        {
            this.configuration = configuration;
            resolve = textResolver ?? (key => key ?? string.Empty);
            ui = NarrativePresentationUI.Create();
            resources = new PresentationResources(ui);
            ui.Escape += OnEscape;
        }

        public void Play(NarrativeEventRequest incoming, Action completed)
        {
            if (disposed || IsPresenting) throw new InvalidOperationException("NCS presenter requires a single live session.");
            request = incoming;
            completion = completed ?? throw new ArgumentNullException(nameof(completed));
            Started?.Invoke(incoming);
            routine = ui.StartCoroutine(PlayEvent());
        }

        private IEnumerator PlayEvent()
        {
            NarrativeSetupParamsDefinition setup = FindSetup(string.IsNullOrWhiteSpace(request.Event.setupKey) ? request.Event.presentationKey : request.Event.setupKey);
            yield return Sequence(request.Event, setup);
            Action callback = completion;
            completion = null;
            routine = null;
            resources.RestoreCamera();
            ui.Hide();
            Finished?.Invoke(request);
            callback?.Invoke();
        }

        private NarrativeSetupParamsDefinition FindSetup(string key)
            => configuration?.setups?.Find(item => item != null && item.setupKey == key);

        private static string TemplateForPresentationClass(string presentationClass, string fallback)
        {
            switch ((presentationClass ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "conversation": return "Tpl_TwoPersonTalk";
                case "reading": return "Tpl_Narration";
                case "notice": return "Tpl_WorldBroadcast";
                case "cutscene": return "Tpl_CutScene";
                default: return fallback;
            }
        }

        private IEnumerator Sequence(NarrativeEventDefinition definition, NarrativeSetupParamsDefinition setup)
        {
            string templateName = setup?.tplName ?? definition.setupType;
            if (!string.IsNullOrWhiteSpace(definition.presentationClass)) templateName = TemplateForPresentationClass(definition.presentationClass, templateName);
            NarrativeTemplateDefinition template = configuration?.templates?.Find(item => item != null && item.tplName == templateName);
            Dictionary<string, string> slots = PresentationSlots.Parse(setup);
            PresentationSlots.ApplyEventProperties(slots, definition);
            string Get(string key, string fallback = "") => slots.TryGetValue(key, out string value) ? value : fallback;
            skipRequested = false;
            allowSkip = string.Equals(template?.interruptType, "playback", StringComparison.OrdinalIgnoreCase) && (setup?.skipAble ?? template?.defaultSkip ?? false);
            ui.Show(resolve(Get("titleKey", definition.titleText)));
            pageIndex = 0; choicesEverShown = false; continueEverShown = false; finishPresentation = false; pages.Clear();
            string[] atoms = PresentationSlots.Sequence(template?.atomSeq);
            if (atoms.Length == 0)
            {
                if (setup != null || !string.IsNullOrWhiteSpace(definition.setupType))
                    Report("Missing or empty template: " + (setup?.tplName ?? definition.setupType));
                atoms = new[] { "A_NARRATE" };
            }
            foreach (string atom in atoms)
            {
                // A choice is never bypassed by playback skip or auto advance.
                string name = atom.Trim().ToUpperInvariant();
                if (skipRequested && name != "A_CHOICE") continue;
                while (ui.IsSuspended) yield return null;
                if (finishPresentation) break;
                switch (name)
                {
                    case "A_DIALOG":
                    case "A_NARRATE":
                        string lines = Get("lines", Get("textKey"));
                        IList<string> keys = string.IsNullOrWhiteSpace(lines) ? definition.contentKeys : lines.Split('|');
                        pages.Clear();
                        if (keys != null) foreach (string key in keys) pages.Add(resolve(key.Trim()));
                        if (pages.Count > 0) yield return DialoguePages(setup?.autoAdvance ?? false);
                        break;
                    case "A_BG": resources.Background(Get("bg")); break;
                    case "A_PORTRAIT":
                        yield return resources.Portraits(Get("a", Get("portrait")), Get("b"), Get("posA", "left"), Get("posB", "right"), PresentationSlots.Number(Get("fade")));
                        break;
                    case "A_CHOICE":
                        string opts = Get("opts");
                        if (!string.IsNullOrWhiteSpace(opts)) yield return Choice(opts);
                        break;
                    case "A_WAIT":
                        ui.ResetSubmit();
                        if (bool.TryParse(Get("untilClick", "false"), out bool click) && click)
                            while (!skipRequested && !ui.ConsumeSubmit()) yield return null;
                        else yield return Wait(PresentationSlots.Number(Get("duration")));
                        break;
                    case "A_CAMERA":
                        yield return resources.Camera(PresentationSlots.Number(Get("zoom"), 1f), PresentationSlots.Number(Get("shake")), PresentationSlots.Number(Get("duration"), .3f), () => skipRequested);
                        break;
                    case "A_SFX":
                        yield return Wait(PresentationSlots.Number(Get("delay")));
                        if (!skipRequested) resources.Sfx(Get("sfx"), PresentationSlots.Number(Get("volume"), 1f));
                        break;
                    case "A_BGM":
                        yield return resources.Bgm(Get("bgm"), PresentationSlots.Number(Get("volume"), 1f), PresentationSlots.Number(Get("fadeIn")), PresentationSlots.Number(Get("fadeOut")), () => skipRequested);
                        break;
                    case "A_FX": yield return resources.Fx(Get("fx"), PresentationSlots.Number(Get("duration"), 1f), () => skipRequested); break;
                    case "A_FADE": yield return ui.Fade(Get("type", "out"), PresentationSlots.Number(Get("duration"), .3f), () => skipRequested); break;
                    case "A_BAR_HINT":
                        ui.ShowHint(resolve(Get("textKey", Get("barKey"))) + "  " + Get("delta"));
                        yield return Wait(PresentationSlots.Number(Get("duration"), 1f));
                        break;
                    default: Report("Unsupported atom: " + atom); break;
                }
            }
        }

        private IEnumerator DialoguePages(bool autoAdvance)
        {
            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
            while (!skipRequested && !finishPresentation)
            {
                if (pageIndex >= pages.Count) yield break;
                ui.ShowLine(pages[pageIndex]);
                ui.ResetSubmit();
                yield return ui.Reveal(() => skipRequested);
                bool lastPage = pageIndex == pages.Count - 1;
                bool showContinue = lastPage && (continueEverShown || !HasChoicesAfterCurrent());
                if (showContinue) continueEverShown = true;
                bool choicesFollow = lastPage && HasChoicesAfterCurrent();
                ui.SetNavigation(pageIndex > 0, !lastPage || choicesFollow, showContinue,
                    () => { if (pageIndex > 0) pageIndex--; },
                    () => { if (pageIndex < pages.Count - 1) pageIndex++; else if (choicesFollow) pageIndex = pages.Count; },
                    () => finishPresentation = true);
                if (autoAdvance && !choicesEverShown && !continueEverShown) yield return Wait(.7f);
                else while (!skipRequested && !finishPresentation && !ui.ConsumeSubmit()) yield return null;
                if (finishPresentation || skipRequested) yield break;
                pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count);
                if (pageIndex >= pages.Count) yield break;
            }
        }

        private string ChoiceRequirement(string choiceId)
        {
            List<NarrativeConditionDefinition> requirements = configuration?.conditions?.FindAll(c => c != null && c.ownerType == "choice" && c.ownerId == choiceId) ?? new List<NarrativeConditionDefinition>();
            if (requirements.Count == 0) return "条件未满足";
            List<string> descriptions = new List<string>();
            foreach (NarrativeConditionDefinition condition in requirements)
            {
                NarrativeBarDefinition bar = configuration.bars.Find(b => b != null && b.barKey == condition.target);
                string label = bar != null ? resolve(bar.nameText) : condition.target;
                descriptions.Add(label + " " + condition.op + " " + condition.value);
            }
            return string.Join(" / ", descriptions);
        }

        private bool HasChoicesAfterCurrent()
        {
            NarrativeSetupParamsDefinition setup = FindSetup(request.Event.setupKey);
            if (setup == null) return false;
            foreach (string assignment in (setup.slotValues ?? string.Empty).Split(','))
            {
                int equals = assignment.IndexOf('=');
                if (equals > 0 && assignment.Substring(0, equals).Trim() == "opts" && !string.IsNullOrWhiteSpace(assignment.Substring(equals + 1))) return true;
            }
            return false;
        }

        private IEnumerator Choice(string group)
        {
            choosing = true;
            ui.ResetSubmit();
            List<NarrativeChoiceDefinition> choices = configuration?.choices?.FindAll(item => item != null && item.groupKey == group) ?? new List<NarrativeChoiceDefinition>();
            choices.Sort((a, b) => a.seq.CompareTo(b.seq));
            if (choices.Count == 0)
            {
                Report("Choice group has no choices: " + group);
                // Fail closed: the request stays active/saveable instead of applying unchosen results.
                while (choosing) yield return null;
            }
            bool selected = false;
            foreach (NarrativeChoiceDefinition choice in choices)
                if (request.SelectedChoices != null && Contains(request.SelectedChoices, choice.choiceId)) selected = true;
            pageIndex = pages.Count;
            choicesEverShown = true;
            ui.ShowChoices(choices, resolve, id => ChoiceUnlocked?.Invoke(id, request.Context) ?? false, ChoiceRequirement,
                request.SelectedChoices, id =>
            {
                if (selected || ChoiceSelected == null || !ChoiceSelected.Invoke(id)) return;
                selected = true;
            });
            if (!selected) while (!selected) yield return null;
            choicesEverShown = true;
            continueEverShown = true;
            choosing = false;
            ui.ResetSubmit();
            ui.SetNavigation(false, false, true, null, null,
                () => finishPresentation = true);
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++) if (values[i] == value) return true;
            return false;
        }

        private IEnumerator Wait(float duration)
        {
            float elapsed = 0;
            while (elapsed < Mathf.Max(0, duration) && !skipRequested)
            {
                if (!ui.IsSuspended) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void OnEscape()
        {
            if (!IsPresenting || choosing) return;
            if (allowSkip) skipRequested = true;
            // Progressive presentation never skips the event; it only reveals the current line.
            else ui.RevealImmediately();
        }

        public void ExecuteEffect(NarrativeEffectNotification notification)
        {
            if (notification?.Result == null) return;
            if (notification.Result.resultType == NarrativeResultType.PlaySetup)
            {
                NarrativeSetupParamsDefinition setup = FindSetup(notification.Target);
                if (setup == null) Report("Missing setup: " + notification.Target);
                else if (ContainsChoice(setup)) Report("PLAY_SETUP cannot contain choices; use a narrative event for a choice transaction: " + setup.setupKey);
                else effects.Enqueue(setup);
            }
            else if (notification.Result.resultType == NarrativeResultType.BarHint)
                ui.ShowHint(resolve(notification.Target) + "  " + notification.Value);
            else if (notification.Result.resultType == NarrativeResultType.SpawnNpc || notification.Result.resultType == NarrativeResultType.DespawnNpc)
                resources.Npc(notification.Target, notification.Value, notification.Result.resultType == NarrativeResultType.SpawnNpc);
        }

        private bool ContainsChoice(NarrativeSetupParamsDefinition setup)
        {
            NarrativeTemplateDefinition template = configuration?.templates?.Find(item => item != null && item.tplName == setup.tplName);
            foreach (string atom in PresentationSlots.Sequence(template?.atomSeq))
                if (string.Equals(atom, "A_CHOICE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(PresentationSlots.Parse(setup).TryGetValue("opts", out string group) ? group : null)) return true;
            return false;
        }

        public void SetSuspended(bool suspended) => ui.SetSuspended(suspended);

        public void SyncNpcLocations(IReadOnlyList<NarrativeStringValue> locations)
        {
            if (locations == null) return;
            foreach (NarrativeStringValue value in locations) if (value != null) resources.Npc(value.key, value.value, true);
        }

        public void Drain(Action completed)
        {
            if (completion != null || draining) throw new InvalidOperationException("NCS drain cannot overlap a presentation.");
            if (effects.Count == 0) { completed?.Invoke(); return; }
            draining = true;
            drainCompleted = completed;
            routine = ui.StartCoroutine(DrainEffects());
        }

        private IEnumerator DrainEffects()
        {
            while (effects.Count > 0)
            {
                NarrativeSetupParamsDefinition setup = effects.Dequeue();
                yield return Sequence(new NarrativeEventDefinition { eventId = "setup:" + setup.setupKey, setupKey = setup.setupKey }, setup);
            }
            resources.RestoreCamera();
            ui.Hide();
            draining = false;
            routine = null;
            Action callback = drainCompleted;
            drainCompleted = null;
            callback?.Invoke();
        }

        private void Report(string message)
        {
            Debug.LogError("[NCS][Presentation] " + message);
            ui.ShowHint(message);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ui != null) ui.StopAllCoroutines();
            completion = drainCompleted = null;
            effects.Clear();
            resources.Dispose();
            if (ui != null) ui.Dispose();
        }
    }
}
