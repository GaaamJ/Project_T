using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

[RequireComponent(typeof(Image))]
public class StandingCGChanger : MonoBehaviour
{
    private Image charImage;

    private void Awake()
    {
        charImage = GetComponent<Image>();
    }

    [YarnCommand("ChangeImage")]
    public void ChangeImage(string speaker, string emotion)
    {
        // Construct the path to the sprite based on character name and emotion
        var spritePath = $"Sprites/{speaker}_{emotion}";

        // Load the sprite from the Resources folder
        var newSprite = Resources.Load<Sprite>(spritePath);

        if (newSprite != null)
        {
            charImage.sprite = newSprite;
        }
        else
        {
            Debug.LogWarning($"Sprite not found at path: {spritePath}");
        }
    }
}
