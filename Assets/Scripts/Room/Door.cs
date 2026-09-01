using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] Collider2D doorCollider;
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite openSprite;

    SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        SetClosed();
    }

    void SetClosed()
    {
        if (doorCollider) doorCollider.enabled = true;
        if (spriteRenderer && closedSprite) spriteRenderer.sprite = closedSprite;
    }

    public void Open()
    {
        if (doorCollider) doorCollider.enabled = false;
        if (spriteRenderer && openSprite) spriteRenderer.sprite = openSprite;
    }
}
