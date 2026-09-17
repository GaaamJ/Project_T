using UnityEngine;
using Yarn.Unity;

public class DialogueBackgroundController : MonoBehaviour, IAlphaControllable
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] DialogueRunner dialogueRunner;

    void Awake()
    {
        canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        dialogueRunner.AddCommandHandler<float>("SetBgAlpha", SetAlpha);
    }

    void OnDisable()
    {
        dialogueRunner.RemoveCommandHandler("SetBgAlpha");
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
    }
}
