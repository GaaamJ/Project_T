using UnityEngine;

public class EntryDoor : MonoBehaviour, ISymbolReactor
{
    [SerializeField] private NodeManager nodeManager; // static 대신 직접 참조 (별빛 #5)
    [SerializeField] private Transform destinationPoint;

    private bool isOpen;

    void OnEnable()
    {
        nodeManager.OnSymbolSubmitted += OnSymbolResult;
    }

    void OnDisable()
    {
        nodeManager.OnSymbolSubmitted -= OnSymbolResult;
    }

    public void OnSymbolResult(bool success)
    {
        if (!success) return;
        isOpen = true;
        OnDoorOpened();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen || !other.CompareTag("Player")) return;
        other.transform.position = destinationPoint.position;
        isOpen = false;
    }

    private void OnDoorOpened()
    {
        // TODO: 문 열림 애니메이션/이펙트
    }
}