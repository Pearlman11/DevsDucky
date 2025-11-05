using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Manages the world-space speech bubble text and visibility
    /// </summary>
    public class SpeechBubbleUI : MonoBehaviour
    {
        [SerializeField] private TextMeshPro subtitleText;

        [SerializeField] private GameObject canvasRoot; // root canvas object


        private void Awake()
        {

            if (!subtitleText) subtitleText = GetComponentInChildren<TextMeshPro>(true);
            if (!canvasRoot)
            {
                var canvas = GetComponentInChildren<Canvas>(true);
                if (canvas) canvasRoot = canvas.gameObject;
            }

            if (canvasRoot == null)
            {
                canvasRoot = gameObject;
            }

            if (subtitleText == null)
            {
                subtitleText = GetComponentInChildren<TextMeshPro>();

            }

            // hide it by default
            Clear();
        }

        public void ShowMessage(string text)
        {
            if (!subtitleText || !canvasRoot)
            {
                Debug.LogWarning("SpeechBubbleUI: missing refs; cannot show message.");
                return;
            }
            canvasRoot.SetActive(true);
            subtitleText.text = text;

        }

        public void SetState(string stateMessage)
        {
            ShowMessage($"<i>{stateMessage}</i>");

        }

        public void Clear()
        {
            if (!subtitleText || !canvasRoot) return;
            subtitleText.text = string.Empty;
            canvasRoot.SetActive(false);
        }
    }

}