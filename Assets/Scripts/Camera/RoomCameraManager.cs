using UnityEngine;
using Unity.Cinemachine;

public class RoomCameraManager : MonoBehaviour
{
    public static RoomCameraManager Instance { get; private set; }

    void Awake() => Instance = this;

    public void SwitchTo(CinemachineCamera targetCam)
    {
        // 씬의 모든 CinemachineCamera를 찾아서, 목표만 활성화
        var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in allCams)
        {
            cam.Priority.Value = (cam == targetCam) ? 10 : 0;
        }
    }
}