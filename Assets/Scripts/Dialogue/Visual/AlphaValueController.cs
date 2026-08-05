using UnityEngine;
using Yarn.Unity;

// Canvas Group의 알파값을 조정하는 클래스
public class AlphaValueController : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private CanvasGroup canvasGroups;

    void OnEnable()
    {
        dialogueRunner.onDialogueStart.AddListener(Show);
        dialogueRunner.onDialogueComplete.AddListener(Hide);
    }

    private void Show() => canvasGroups.alpha = 1;

    private void Hide() => canvasGroups.alpha = 0;

    [YarnCommand("SetAlpha")]
    public void SetAlpha(float alpha)
    {
        canvasGroups.alpha = alpha;
    }
}
