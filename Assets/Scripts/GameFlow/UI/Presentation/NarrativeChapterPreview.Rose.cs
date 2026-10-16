using System;

namespace Spotlight.GameFlow
{
    public sealed partial class NarrativeChapterPreview
    {
        /// <summary>Written portrait text only, without guessing the unresolved perception rule.</summary>
        public void PlayRose1009PortraitConstruction(Action completed = null)
        {
            ConfigureRose1009Stage(true);
            Play("rose.1009.s1", 2, null, completed, true);
        }

        private void ConfigureRose1009Stage(bool portrait)
        {
            constructionSeed = debug => debug.SetDate(4);
            constructionCondition = key => key == "dependency:B1009.stage.front" ? !portrait
                : key == "dependency:B1009.stage.portrait" ? portrait
                : throw new InvalidOperationException("Unknown B1009 stage: " + key);
        }

        /// <summary>Pre-order source fragment only; stops before P046 unresolved drink name.</summary>
        public void PlayRose1011Construction(int perception, Action completed = null)
        {
            ConfigureRose1011Stage("pre_order", false, null, null);
            Play("rose.1011.s1", perception, null, completed, true);
        }

        /// <summary>Explicit isolated brewing/knowledge samples; never consumes real cups.</summary>
        public void PlayRose1011PostBrewConstruction(int perception, bool lowGrade,
            bool? wisdomPassed = null, bool? historySpecialization = null, Action completed = null)
        {
            if (wisdomPassed == false)
                throw new InvalidOperationException("B-B03 wisdom failure route is undefined; no failure fixture is playable.");
            ConfigureRose1011Stage("post_brew", lowGrade, wisdomPassed, historySpecialization);
            Play("rose.1011.s1", perception, null, completed, true);
        }

        private void ConfigureRose1011Stage(string stage, bool lowGrade, bool? wisdomPassed, bool? history)
        {
            constructionSeed = debug => debug.SetDate(6);
            constructionCondition = key =>
            {
                if (key == "dependency:B1011.stage.pre_order") return stage == "pre_order";
                if (key == "dependency:B1011.stage.post_brew") return stage == "post_brew";
                if (key == "dependency:B1011.low-grade") return lowGrade;
                if (key == "dependency:B1011.wisdom-passed")
                    return wisdomPassed == true ? true : throw new InvalidOperationException("B-B03 requires an explicit passed preview fixture; real threshold/failure remain undefined.");
                if (key == "dependency:B1011.politics")
                    return history ?? throw new InvalidOperationException("History specialization snapshot unavailable.");
                throw new InvalidOperationException("Unknown B1011 fixture: " + key);
            };
        }

        /// <summary>Only the written P004-P015 fragment; never enters real brewing.</summary>
        public void PlayRose1016Construction(int wisdom, Action completed = null)
        {
            constructionSeed = debug => { debug.SetDate(11); debug.SetAbility("wisdom", wisdom); };
            Play("rose.1016.s1", 2, null, completed, true);
        }

        /// <summary>One isolated source stage, with explicit historical fixtures.</summary>
        public void PlayRose1012Construction(string stage, bool? appreciation = null,
            bool? hatHelped = null, bool? churchCompleted = null, Action completed = null)
        {
            if (stage != "preparation" && stage != "brew_retry" && stage != "post_brew")
                throw new ArgumentException("Expected preparation, brew_retry or post_brew.", nameof(stage));
            if (stage == "post_brew" && (!appreciation.HasValue || !hatHelped.HasValue || churchCompleted != true))
                throw new InvalidOperationException("Post-brew preview requires explicit A/H snapshots and D completed=true; this is not real brewing.");
            constructionSeed = debug => debug.SetDate(7);
            constructionCondition = key =>
            {
                const string prefix = "dependency:B-C02.stage.";
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string requested = key.Substring(prefix.Length);
                    if (requested != "preparation" && requested != "brew_retry" && requested != "post_brew")
                        throw new InvalidOperationException("Unknown B stage: " + key);
                    return requested == stage;
                }
                if (key == "dependency:B-history.snapshot-ready") return stage == "post_brew";
                if (key == "dependency:B-history.D-completed") return churchCompleted ?? throw new InvalidOperationException("D snapshot unavailable.");
                if (key == "dependency:B-history.A") return appreciation ?? throw new InvalidOperationException("A snapshot unavailable.");
                if (key == "dependency:B-history.H") return hatHelped ?? throw new InvalidOperationException("H snapshot unavailable.");
                throw new InvalidOperationException("Unknown B fixture dependency: " + key);
            };
            Play("rose.1012.s1", 2, null, completed, true);
        }
    }
}
