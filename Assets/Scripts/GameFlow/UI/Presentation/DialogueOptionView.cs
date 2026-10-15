using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Artist-authored UGUI option template. Layout groups derive height from wrapped TMP text.</summary>
    public sealed class DialogueOptionView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image surface;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text requirement;
        [SerializeField] private Color enabledInk = new Color(.30f, .12f, .045f);
        [SerializeField] private Color disabledInk = new Color(.23f, .085f, .04f);
        public Button Button => button;
        public TMP_Text Label => label;
        public void Bind(DialogueOption option, Sprite enabledSprite, Sprite disabledSprite, Action<string> select)
        {
            surface.sprite = option.Enabled ? enabledSprite : disabledSprite;
            surface.type = Image.Type.Sliced;
            surface.color = Color.white;
            label.text = option.Text;
            label.color = option.Enabled ? enabledInk : disabledInk;
            requirement.text = option.RequirementHint;
            requirement.gameObject.SetActive(!option.Enabled && !string.IsNullOrEmpty(option.RequirementHint));
            button.interactable = option.Enabled;
            button.onClick.AddListener(() => select(option.OptionId));
            Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
        }
    }
}
