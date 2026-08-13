using Unity.Cinemachine;
using UnityEngine;

public class UnlockedDoor : MonoBehaviour
{
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private CinemachineCamera targetCam; // 씬 전환 시 카메라 전환을 위해 추가


    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = destinationPoint.position;
            RoomCameraManager.Instance.SwitchTo(targetCam);
        }
    }
}