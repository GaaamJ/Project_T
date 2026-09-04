using UnityEngine;

/// <summary>
/// 문/출구 GameObject에 부착. IsTrigger 콜라이더가 필요합니다.
/// Inspector에서 targetSpawnPoint를 지정하면 Player가 진입 시 해당 위치로 이동합니다.
/// NPC(Umia 등)는 "Player" 태그를 가져도 Player 레이어(7)가 아니면 무시한다.
/// </summary>
public class RoomTransitionTrigger : MonoBehaviour
{
    // "Player" 레이어 인덱스. NPC가 "Player" 태그를 공유해도 방 전환이 발동되지 않도록 레이어로 추가 구분.
    const int PlayerLayer = 7;

    [SerializeField] Transform targetSpawnPoint;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (other.gameObject.layer != PlayerLayer) return;
        other.transform.position = targetSpawnPoint.position;
    }
}
