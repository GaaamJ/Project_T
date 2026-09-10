using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

[RequireComponent(typeof(Image))]
public class StandingCGChanger : MonoBehaviour
{
    Image _charImage;

    void Awake()
    {
        _charImage = GetComponent<Image>();
    }

    [YarnCommand("ChangeImage")]
    public void ChangeImage(string speaker, string emotion)
    {
        var spritePath = $"Sprites/{speaker}_{emotion}";
        var newSprite = Resources.Load<Sprite>(spritePath);

        if (newSprite != null)
        {
            _charImage.sprite = newSprite;
        }
        else
        {
            Debug.LogWarning($"Sprite not found at path: {spritePath}");
        }
    }
}
