using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueSpeakerImageController : MonoBehaviour, IAlphaControllable, IImageChangeable
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image image;
    [SerializeField] DialogueRunner dialogueRunner;

    void Awake()
    {
        canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        dialogueRunner.AddCommandHandler<float>("SetSpeakerAlpha", SetAlpha);
        dialogueRunner.AddCommandHandler<string, string>("ChangeImage", ChangeImage);
    }

    void OnDisable()
    {
        dialogueRunner.RemoveCommandHandler("SetSpeakerAlpha");
        dialogueRunner.RemoveCommandHandler("ChangeImage");
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
    }

    public void ChangeImage(string speaker, string emotion)
    {
        var spritePath = $"Sprites/{speaker}_{emotion}";
        var newSprite = Resources.Load<Sprite>(spritePath);
        if (newSprite != null)
            image.sprite = newSprite;
        else
            Debug.LogWarning($"Sprite not found at path: {spritePath}");
    }
}
