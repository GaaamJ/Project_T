using UnityEngine;

/// <summary>
/// 문/출구 GameObject에 부착. IsTrigger 콜라이더가 필요합니다.
/// Inspector에서 targetSpawnPoint를 지정하면 Player가 진입 시 해당 위치로 이동합니다.
/// </summary>
public class RoomTransitionTrigger : MonoBehaviour
{
    [SerializeField] Transform targetSpawnPoint;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.transform.position = targetSpawnPoint.position;
    }
}
