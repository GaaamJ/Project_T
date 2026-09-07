using UnityEngine;

public class RoomTransitionTrigger : MonoBehaviour
{
    [SerializeField] Transform targetSpawnPoint;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.transform.position = targetSpawnPoint.position;
    }
}
