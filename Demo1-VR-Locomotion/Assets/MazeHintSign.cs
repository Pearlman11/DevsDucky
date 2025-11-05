using UnityEngine;
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Maze/Maze Hint Sign")]
public class MazeHintSign : MonoBehaviour
{
    [TextArea(2, 4)]
    [SerializeField] private string hintText = "Follow the wall that isn't quite straight.";
    [SerializeField] private bool doubleSidedText = false;

    [Header("Visuals")]
    [SerializeField] private Color boardColor = new Color(0.48f, 0.33f, 0.20f);
    [SerializeField] private Color textColor = Color.black;

    // Auto-found at runtime (created by the prefab)
    [SerializeField] private TextMeshPro frontTMP;
    [SerializeField] private TextMeshPro backTMP;
    [SerializeField] private Renderer boardRenderer;

    public void SetHint(string text)
    {
        hintText = text;
        Apply();
    }

    public void SetDoubleSided(bool enabled)
    {
        doubleSidedText = enabled;
        if (backTMP != null) backTMP.gameObject.SetActive(doubleSidedText);
        Apply();
    }

    public void Apply()
    {
        EnsureRefs();
        if (frontTMP != null)
        {

            frontTMP.text = hintText;
            frontTMP.color = textColor;
        }
        if (backTMP != null)
        {
            backTMP.text = hintText;
            backTMP.color = textColor;
            backTMP.gameObject.SetActive(doubleSidedText);
        }
        if (boardRenderer != null)
        {
            var mat = boardRenderer.sharedMaterial;
            if (mat == null) { mat = new Material(Shader.Find("Standard")); boardRenderer.sharedMaterial = mat; }
            mat.color = boardColor;
        }
    }

    private void EnsureRefs()
    {
        if (frontTMP == null || backTMP == null || boardRenderer == null)
        {
            foreach (var tmp in GetComponentsInChildren<TextMeshPro>(true))
            {
                if (tmp.name.Contains("Front")) frontTMP = tmp;
                else if (tmp.name.Contains("Back")) backTMP = tmp;
            }
            var boards = GetComponentsInChildren<Renderer>(true);
            foreach (var r in boards)
            {
                if (r.name == "Board") { boardRenderer = r; break; }
            }
        }
    }

    private void Awake()  { Apply(); }
    private void OnValidate() { Apply(); }
}