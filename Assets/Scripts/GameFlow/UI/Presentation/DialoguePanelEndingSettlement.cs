using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed partial class DialoguePanel
    {
        private bool endingSettlementMode, endingSettlementBound;
        public string SettlementText => endingSettlementMode && dialogueText != null ? dialogueText.text : string.Empty;
        public bool IsEndingSettlement => endingSettlementMode;
        public int SettlementPageCount => dialogueText != null ? dialogueText.textInfo.pageCount : 0;

        /// <summary>Embedded ResultPanel view: reuse the real prefab, typography and TMP pagination.
        /// Does not open a narrative session, acquire a pause owner, append history or settle facts.</summary>
        public void ShowEndingSettlement(string text)
        {
            if(IsPlaying)throw new System.InvalidOperationException("An active dialogue cannot become an ending settlement view.");
            endingSettlementMode=true;
            if(!endingSettlementBound)
            {
                if(continueButton!=null)continueButton.onClick.AddListener(AdvanceEndingSettlement);
                if(dialogueBodyButton!=null)dialogueBodyButton.onClick.AddListener(AdvanceEndingSettlement);
                endingSettlementBound=true;
            }
            // Preserve DialogueBox and all its authored art/text children. Only its scene/portrait
            // siblings and the full-screen raycast blocker are hidden in this embedded instance.
            for(int i=0;i<transform.childCount;i++)
            {
                var child=transform.GetChild(i);
                child.gameObject.SetActive(child==artworkRoot || child==dialogueBox || dialogueBox.IsChildOf(child));
            }
            if(artworkRoot!=null)
                for(int i=0;i<artworkRoot.childCount;i++)
                {
                    var child=artworkRoot.GetChild(i);
                    child.gameObject.SetActive(child==dialogueBox || dialogueBox.IsChildOf(child));
                }
            if(canvasGroup!=null){canvasGroup.alpha=1f;canvasGroup.blocksRaycasts=canvasGroup.interactable=true;}
            if(speakerText!=null)speakerText.text="终章";
            if(skipButton!=null)skipButton.gameObject.SetActive(false);
            if(cancelButton!=null)cancelButton.gameObject.SetActive(false);
            if(passiveHintText!=null)passiveHintText.gameObject.SetActive(false);
            if(hintText!=null)hintText.gameObject.SetActive(false);
            if(dialogueBox!=null){dialogueBox.anchoredPosition=boxPosition;}
            script=new DialogueScript(new[]{new DialogueLine(DialogueSpeaker.Narrator,text??string.Empty)},isPreview:true);
            activeLines.Clear();activeLines.Add(script[0]);LineIndex=0;
            dialogueText.richText=false;dialogueText.overflowMode=TextOverflowModes.Page;
            dialogueText.text=text??string.Empty;
            Canvas.ForceUpdateCanvases();dialogueText.ForceMeshUpdate();
            ShowEndingPage(1);
        }
        private void ShowEndingPage(int number)
        {
            ShowPage(number);
            dialogueText.maxVisibleCharacters=int.MaxValue;
            if(continueButton!=null){continueButton.gameObject.SetActive(number<dialogueText.textInfo.pageCount);continueButton.interactable=true;}
        }
        public void AdvanceEndingSettlement()
        {
            if(!endingSettlementMode||lastAdvanceFrame==Time.frameCount)return;
            lastAdvanceFrame=Time.frameCount;
            if(page<dialogueText.textInfo.pageCount)ShowEndingPage(page+1);
        }
        private void UpdateEndingSettlement()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null&&(keyboard.spaceKey.wasPressedThisFrame||keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame))AdvanceEndingSettlement();
        }
    }
}
