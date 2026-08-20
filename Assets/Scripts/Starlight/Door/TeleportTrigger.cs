using Unity.Cinemachine;
using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private CinemachineCamera targetCam;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = destinationPoint.position;
            RoomCameraManager.Instance.SwitchTo(targetCam);
        }
    }
}
