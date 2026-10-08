using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    [CreateAssetMenu(menuName = "Spotlight/Dialogue Script", fileName = "DialogueScript")]
    public sealed class DialogueScriptAsset : ScriptableObject, IDialogueScriptSource
    {
        [SerializeField] private string title;
        [SerializeField] private DialogueCharacter leftCharacter;
        [SerializeField] private DialogueCharacter rightCharacter;
        [SerializeField] private DialogueCharacter centerCharacter;
        [SerializeField] private Texture background;
        [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

        public DialogueScript CreateScript() => new DialogueScript(lines, leftCharacter, rightCharacter, title, background, centerCharacter: centerCharacter);
    }
}
