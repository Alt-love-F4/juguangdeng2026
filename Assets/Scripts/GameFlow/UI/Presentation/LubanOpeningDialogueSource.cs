using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public sealed class LubanOpeningDialogueSource : IDialogueScriptSource
    {
        private readonly string presentationId;
        private readonly NarrativeConfiguration configuration;

        public LubanOpeningDialogueSource(string presentationId = "first_day_cafe", NarrativeConfiguration configuration = null)
        {
            this.presentationId = presentationId;
            this.configuration = configuration ?? LubanNarrativeLoader.Load();
        }

        public DialogueScript CreateScript()
        {
            NarrativeOpeningPresentationDefinition presentation = configuration.openingPresentations
                .FirstOrDefault(item => item.presentationId == presentationId);
            if (presentation == null) throw new InvalidOperationException("Missing Luban opening presentation: " + presentationId);

            DialogueVisualsAsset visuals = Resources.Load<DialogueVisualsAsset>(presentation.visualsResource);
            if (visuals == null) throw new InvalidOperationException("Missing opening visuals resource: " + presentation.visualsResource);
            var lines = new List<DialogueLine>();
            foreach (NarrativeOpeningLineDefinition line in configuration.openingLines
                         .Where(item => item.presentationId == presentationId).OrderBy(item => item.seq))
            {
                string text = Resolve(line.textKey);
                DialogueSpeaker speaker = (DialogueSpeaker)Enum.Parse(typeof(DialogueSpeaker), line.speaker, true);
                DialogueGesture gesture = (DialogueGesture)Enum.Parse(typeof(DialogueGesture), line.gesture, true);
                lines.Add(new DialogueLine(speaker, text, gesture: gesture, autoAdvanceSeconds: line.autoAdvanceSeconds));
            }

            return new DialogueScript(lines,
                new DialogueCharacter(Resolve(presentation.leftNameKey), visuals.LeftPortrait),
                new DialogueCharacter(Resolve(presentation.rightNameKey), visuals.RightPortrait),
                Resolve(presentation.titleKey), visuals.Background);
        }

        private string Resolve(string key)
        {
            NarrativeTextDefinition text = configuration.texts.FirstOrDefault(item => item.key == key);
            return text?.content ?? throw new InvalidOperationException("Missing opening text key: " + key);
        }
    }
}
