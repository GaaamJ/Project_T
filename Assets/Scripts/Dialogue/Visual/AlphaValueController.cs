using UnityEngine;
using Yarn.Unity;

public class AlphaValueController : MonoBehaviour
{
    [SerializeField] DialogueRunner dialogueRunner;
    [SerializeField] CanvasGroup canvasGroup;

    void OnEnable()
    {
        dialogueRunner.onDialogueStart.AddListener(Show);
        dialogueRunner.onDialogueComplete.AddListener(Hide);
    }

    void OnDisable()
    {
        dialogueRunner.onDialogueStart.RemoveListener(Show);
        dialogueRunner.onDialogueComplete.RemoveListener(Hide);
    }

    void Show() => canvasGroup.alpha = 1;

    void Hide() => canvasGroup.alpha = 0;

    [YarnCommand("SetAlpha")]
    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }
}
