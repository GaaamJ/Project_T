using UnityEngine;

public class EntryDoor : MonoBehaviour, ISymbolReactor
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Transform destinationPoint;

    private bool isOpen;

    void OnEnable()
    {
        NodeManager.OnSymbolSubmitted += OnSymbolResult;
    }

    void OnDisable()
    {
        NodeManager.OnSymbolSubmitted -= OnSymbolResult;
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