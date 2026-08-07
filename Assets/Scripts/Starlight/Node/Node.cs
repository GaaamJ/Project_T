using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Node : MonoBehaviour, IDelayInteractable
{
    [SerializeField] private float holdDuration = 2f;
    public float HoldDuration => holdDuration;

    public bool IsSelected { get; private set; }

    private SpriteRenderer sR;

    private void Awake()
    {
        sR = GetComponent<SpriteRenderer>();
    }

    public void Interact()
    {
        NodeManager.Instance.TryRegisterNode(this);
    }

    public void ResetNode()
    {
        Deselect();
    }

    // 단순하게 flipY지만, 추후에 애니메이션이나 다른 시각적 효과를 추가할 수 있음
    public void Select()
    {
        IsSelected = true;
        sR.flipY = true;
    }

    public void Deselect()
    {
        Debug.Log($"{name} Deselect 호출됨");
        IsSelected = false;
        sR.flipY = false;
    }
}