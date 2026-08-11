using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Node : MonoBehaviour, IDelayInteractable
{
    [SerializeField] private float holdDuration = 2f;
    public float HoldDuration => holdDuration;

    public bool IsSelected { get; private set; }

    private SpriteRenderer sR;
    private NodeManager myManager; // 싱글톤 대신 직접 참조 (별빛 #5)

    private void Awake()
    {
        sR = GetComponent<SpriteRenderer>();
    }

    // NodeManager가 Awake 시점에 자식 Node들에게 호출해줌
    public void SetManager(NodeManager manager)
    {
        myManager = manager;
    }

    public void Interact()
    {
        myManager.TryRegisterNode(this);
    }

    public void ResetNode()
    {
        Deselect();
    }

    public void Select()
    {
        IsSelected = true;
        sR.flipY = true;
    }

    public void Deselect()
    {
        IsSelected = false;
        sR.flipY = false;
    }
}