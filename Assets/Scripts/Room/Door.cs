using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] Collider2D doorCollider;
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite openSprite;

    SpriteRenderer _spriteRenderer;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        SetClosed();
    }

    void SetClosed()
    {
        if (doorCollider) doorCollider.enabled = true;
        if (_spriteRenderer && closedSprite) _spriteRenderer.sprite = closedSprite;
    }

    public void Open()
    {
        if (doorCollider) doorCollider.enabled = false;
        if (_spriteRenderer && openSprite) _spriteRenderer.sprite = openSprite;
    }
}
