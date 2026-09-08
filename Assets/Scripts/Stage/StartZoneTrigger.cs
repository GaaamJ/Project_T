using UnityEngine;

namespace ProjectT.Stage
{
    // 시작 구역의 트리거 콜라이더. 플레이어가 이 구역을 벗어난 순간을 감지해
    // StageManager에 알린다. Player 컴포넌트에 직접 코드를 넣지 않는 이유는
    // "시작 구역 개념" 자체가 스테이지 시스템에 속하는 관심사이기 때문.
    [RequireComponent(typeof(Collider2D))]
    public class StartZoneTrigger : MonoBehaviour
    {
        // 인스펙터에서 명시 연결. 씬에 StageManager가 1개뿐이더라도
        // 참조를 명시하는 편이 실행 순서/누락 실수를 잡기 쉬움.
        [SerializeField] StageManager stageManager;

        void OnTriggerExit2D(Collider2D other)
        {
            // 자식 콜라이더가 트리거를 발화시키는 경우에도 루트 Rigidbody의 태그로 필터링.
            // attachedRigidbody가 없으면 other.gameObject의 태그로 폴백 — 단순 트리거 오브젝트도 지원.
            var target = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!target.CompareTag("Player")) return;

            if (stageManager != null)
                stageManager.OnPlayerLeftStartZone();
        }
    }
}
