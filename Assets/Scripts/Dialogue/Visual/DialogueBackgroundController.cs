using UnityEngine;
using Yarn.Unity;

public class DialogueBackgroundController : MonoBehaviour, IAlphaControllable
{
    [SerializeField] CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup.alpha = 0f;
    }

    // Yarn Command 이름은 화자 이미지 컨트롤러(SetSpeakerAlpha)와 구분하기 위해 SetBgAlpha로 지정
    [YarnCommand("SetBgAlpha")]
    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }

    public void Reset()
    {
        canvasGroup.alpha = 0f;
    }
}
