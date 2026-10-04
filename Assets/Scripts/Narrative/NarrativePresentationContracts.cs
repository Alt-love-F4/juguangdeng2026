namespace Spotlight.Narrative
{
    public enum NarrativeAtomType
    {
        A_BG, A_PORTRAIT, A_DIALOG, A_NARRATE, A_CHOICE, A_WAIT,
        A_CAMERA, A_SFX, A_BGM, A_FX, A_FADE, A_BAR_HINT
    }

    public static class NarrativePresentationTemplates
    {
        public static readonly string[] Suggested =
        {
            "Tpl_TwoPersonTalk", "Tpl_MonoTalk", "Tpl_Narration", "Tpl_WorldBroadcast",
            "Tpl_LetterRead", "Tpl_Timeskip", "Tpl_CutScene", "Tpl_ChoiceOnly", "Tpl_BarMilestone"
        };
    }
}
