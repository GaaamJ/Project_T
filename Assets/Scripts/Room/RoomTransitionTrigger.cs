using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 문/출구 GameObject에 부착. IsTrigger 콜라이더가 필요합니다.
/// Inspector에서 targetSpawnPoint를 지정하면 Player 태그 오브젝트 진입 시 해당 위치로 이동합니다.
/// </summary>
public class RoomTransitionTrigger : MonoBehaviour
{
    [SerializeField] Transform targetSpawnPoint;

    public Vector2? TargetPosition => targetSpawnPoint != null ? (Vector2?)((Vector2)targetSpawnPoint.position) : null;

    // 오브젝트별 마지막 텔레포트 시각 — 진입 즉시 exit 텔레포터로 재전송되는 핑퐁 방지.
    static readonly Dictionary<Collider2D, float> lastTeleportTime = new Dictionary<Collider2D, float>();
    const float TeleportCooldown = 0.5f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        float last;
        if (lastTeleportTime.TryGetValue(other, out last) && Time.time - last < TeleportCooldown) return;
        lastTeleportTime[other] = Time.time;
        other.transform.position = targetSpawnPoint.position;
    }
}
