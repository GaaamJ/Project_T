using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueSpeakerImageController : MonoBehaviour, IAlphaControllable, IImageChanger
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image image;

    void Awake()
    {
        canvasGroup.alpha = 0f;
    }

    // Yarn Command 이름은 배경 컨트롤러(SetBgAlpha)와 구분하기 위해 SetSpeakerAlpha로 지정
    [YarnCommand("SetSpeakerAlpha")]
    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }

    public void Reset()
    {
        canvasGroup.alpha = 0f;
    }

    [YarnCommand("ChangeImage")]
    public void ChangeImage(string speaker, string emotion)
    {
        // Resources 폴더 규약: Sprites/{speaker}_{emotion} 형식으로 로드
        var spritePath = $"Sprites/{speaker}_{emotion}";
        var newSprite = Resources.Load<Sprite>(spritePath);

        if (newSprite != null)
        {
            image.sprite = newSprite;
        }
        else
        {
            Debug.LogWarning($"Sprite not found at path: {spritePath}");
        }
    }
}
