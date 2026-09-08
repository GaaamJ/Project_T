using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread;

namespace ProjectT.DebugTools
{
    // 임시 검증용 컴포넌트. BuffPanelUI/HoldGaugeUI 동작을 눈으로 확인하기 위해
    // 키 입력 또는 auto-sequence 로 ThreadBuffHolder 상태를 직접 조작한다.
    // 검증이 끝나면 이 컴포넌트/스크립트는 제거해도 무방하다.
    //
    // 이 프로젝트는 New Input System 을 사용(activeInputHandler=1) — Keyboard.current 로 조회.
    //
    // 키 매핑:
    //   1 → Red 획득
    //   2 → Blue 획득
    //   3 → Gold 획득
    //   0 → Consume (즉시 소모)
    //
    // autoSequence=true 로 두면 Start 시점에 자동 시나리오를 실행:
    //   - Acquire(Red)
    //   - 짧은 duration 을 위해 임시로 buffDuration 값을 낮게 설정할 필요가 있음(인스펙터에서 조정)
    public class DebugBuffTester : MonoBehaviour
    {
        [SerializeField] ThreadBuffHolder holder;
        [SerializeField] bool autoSequence;
        [SerializeField] float autoStepDelay = 1.5f;

        void Start()
        {
            if (autoSequence) StartCoroutine(RunAutoSequence());
        }

        IEnumerator RunAutoSequence()
        {
            if (holder == null) yield break;

            // 시퀀스 시작 전 짧게 대기 — 첫 프레임 UI 초기화 여유를 준다.
            yield return new WaitForSeconds(0.2f);
            Debug.Log("[DebugBuffTester] Acquire(Red)");
            holder.Acquire(ThreadType.Red);

            yield return new WaitForSeconds(autoStepDelay);
            Debug.Log("[DebugBuffTester] Acquire(Blue) - Replace 케이스");
            holder.Acquire(ThreadType.Blue);

            yield return new WaitForSeconds(autoStepDelay);
            Debug.Log("[DebugBuffTester] Consume() - 소모");
            holder.Consume();

            yield return new WaitForSeconds(autoStepDelay);
            Debug.Log("[DebugBuffTester] Acquire(Gold) - 재획득");
            holder.Acquire(ThreadType.Gold);
        }

        void Update()
        {
            if (holder == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) holder.Acquire(ThreadType.Red);
            else if (kb.digit2Key.wasPressedThisFrame) holder.Acquire(ThreadType.Blue);
            else if (kb.digit3Key.wasPressedThisFrame) holder.Acquire(ThreadType.Gold);
            else if (kb.digit0Key.wasPressedThisFrame) holder.Consume();
        }
    }
}
