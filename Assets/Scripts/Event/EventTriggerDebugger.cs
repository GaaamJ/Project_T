using System.Collections;
using UnityEngine;

namespace ProjectT.Event
{
    // Step 4 검증 전용 컴포넌트.
    //
    // 왜 별도 컴포넌트인가:
    // - EncounterManager 는 Step 5 에서 별도로 만들 예정이고, 그것은 실제 프로덕션 진입점이다.
    // - 여기서는 EventRunner 의 계약(요청 → 실행 → _isRunning 거부 → 완료 → 저장) 을
    //   확인하는 것이 목적이므로, 프로덕션 코드에 검증용 흐름을 섞지 않는다.
    // - Step 4 검증이 끝난 뒤 Step 5 에서 EncounterManager 가 도입되면 이 컴포넌트는 제거된다.
    //
    // 시나리오:
    // 1. Start → EventRunner.TryRun(firstEventId)  → Yarn 시작
    // 2. 다음 프레임 → EventRunner.TryRun(secondEventId) → _isRunning 거부 로그
    //    (secondEventId 로 "EventA" 를 쓰는 이유: Yarn Node 없이 오직 거부 로그만 확인하면 충분.
    //     실제로는 조건 판정 이전에 _isRunning 이 우선 거부하므로 Yarn Node 존재 여부 무관.)
    // 3. Yarn 정상 종료 → EventRunner 가 완료 기록 + 저장 로그
    // 4. Play 재시작 → firstEventId 완료 상태로 "거부 (이미 완료)" 로그
    public class EventTriggerDebugger : MonoBehaviour
    {
        [SerializeField] EventRunner runner;
        [SerializeField] string firstEventId = "FirstEncounter";
        [SerializeField] string secondEventId = "EventA";

        void Start()
        {
            if (runner == null)
            {
                Debug.LogWarning("[Step4][EventTriggerDebugger] runner 참조가 비어 있습니다.");
                return;
            }

            // 첫 요청은 즉시. 실제 진행 상태에 따라 실행 시작 로그 또는 "이미 완료" 거부 로그가 뜬다.
            runner.TryRun(firstEventId);

            // 두 번째 요청은 코루틴으로 다음 프레임에 보낸다.
            // 같은 프레임에 연달아 호출해도 되지만, 로그 순서를 시각적으로 분리해 확인하기 위함.
            StartCoroutine(RequestSecondNextFrame());
        }

        IEnumerator RequestSecondNextFrame()
        {
            yield return null;
            if (runner != null)
                runner.TryRun(secondEventId);
        }
    }
}
