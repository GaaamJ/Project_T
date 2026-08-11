using UnityEngine;

public class ExitDoor : MonoBehaviour
{
    [SerializeField] private Transform destinationPoint;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            other.transform.position = destinationPoint.position;
    }
}