using UnityEngine;

public class Node : MonoBehaviour, IDelayInteractable
{
    [SerializeField] private float holdDuration = 2f;
    public float HoldDuration => holdDuration;

    public bool IsActivated { get; private set; }

    public void Interact()
    {
        // 노드가 비활성화되어 있으면 return, 활성화 가능하면 이후 처리
        if (IsActivated) return;
        IsActivated = true;
        NodeManager.Instance.RegisterNodeInteraction(this);
    }

    public void ResetNode()
    {
        IsActivated = false;
    }
}