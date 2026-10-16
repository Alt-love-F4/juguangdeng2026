using System;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    public sealed partial class NarrativeChapterPreview
    {
        private bool ReadConstructionPageCondition(string key, bool construction)
        {
            if (construction && key.StartsWith("not:", StringComparison.Ordinal))
                return !ReadConstructionPageCondition(key.Substring(4), true);
            if (construction && EventId == "church.1104.s1" && key.StartsWith("checkpointStage:", StringComparison.Ordinal))
                return constructionCondition?.Invoke(key) ?? throw new InvalidOperationException("Missing stage fixture: " + key);
            if (construction && key.StartsWith("dependency:", StringComparison.Ordinal))
                return constructionCondition?.Invoke(key) ?? throw new InvalidOperationException("Missing construction fixture: " + key);
            return session.Controller.ReadPresentationCondition(key);
        }

        /// <summary>Silently reconstruct the current stage up to a reachable source page; later choices remain interactive.</summary>
        public void PlayChurchLongPageConstruction(string pageId, string fee, string route, string night,
            int perception = 2, int wisdom = 3, bool? revolution = null, Action completed = null)
        {
            if (disposed || session != null) throw new InvalidOperationException("Create a fresh chapter preview for each run.");
            var pagePreview = new ChurchLongPageConstruction(LubanNarrativeLoader.Load(), pageId, fee, route, night,
                perception, wisdom, revolution);
            constructionStarted = _ =>
            {
                foreach (string choice in pagePreview.PreviousChoices)
                    if (!session.Controller.SelectChoice(choice))
                        throw new InvalidOperationException("Previous-stage preview choice rejected: " + choice);
            };
            constructionPlayback = pagePreview.Playback;
            PlayChurchLongConstruction(pagePreview.Stage, perception, wisdom, revolution, () =>
            {
                if (!pagePreview.Reached)
                {
                    Dispose();
                    throw new InvalidOperationException("Preview target was not reached: " + pageId);
                }
                completed?.Invoke();
            });
        }

        /// <summary>One isolated text stage. Does not simulate business or commit chapter results.</summary>
        public void PlayChurchLongConstruction(string stage, int perception = 2, int wisdom = 3,
            bool? revolution = null, Action completed = null)
        {
            if (stage != "morning" && stage != "tour" && stage != "night")
                throw new ArgumentException("Expected morning, tour or night.", nameof(stage));
            if (stage == "night" && !revolution.HasValue)
                throw new InvalidOperationException("Night construction requires an explicit revolution fixture; unknown is not false.");
            constructionSeed = debug =>
            {
                debug.SetDate(30);
                debug.SetAbility("perception", perception);
                debug.SetAbility("wisdom", wisdom);
            };
            constructionCondition = key =>
            {
                const string stagePrefix = "checkpointStage:";
                if (key.StartsWith(stagePrefix, StringComparison.Ordinal))
                    return key.Substring(stagePrefix.Length) == (stage == "morning" ? "0" : stage == "tour" ? "1" : "2");
                if (key == "dependency:E-B03.revolution")
                    return revolution ?? throw new InvalidOperationException("Revolution fixture unavailable.");
                throw new InvalidOperationException("Unknown church construction dependency: " + key);
            };
            Play("church.1104.s1", perception, null, completed, true);
        }
    }
}
