using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SilentSprint.Cutscenes
{
    public class CutsceneDialogueManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI contentText;

        public void SetDialogue(string fullLine)
        {
            if (dialogPanel != null && !dialogPanel.activeSelf)
            {
                dialogPanel.SetActive(true);
            }

            string speaker = "";
            string content = fullLine;

            if (fullLine.Contains(":"))
            {
                string[] parts = fullLine.Split(new char[] { ':' }, 2);
                speaker = parts[0].Trim();
                content = parts[1].Trim();
            }

            if (speakerText != null) speakerText.text = speaker;
            if (contentText != null) contentText.text = content;
        }

        public void HideDialogue()
        {
            if (dialogPanel != null)
            {
                dialogPanel.SetActive(false);
            }
        }
    }
}