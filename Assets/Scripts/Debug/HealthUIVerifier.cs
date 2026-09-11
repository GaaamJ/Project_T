using System.Collections;
using UnityEngine;
using ProjectT.Player;

namespace ProjectT.DebugTools
{
    // 체력 UI 검증용 임시 컴포넌트.
    // MCP 스크린샷 흐름에서는 Game View 포커스가 없어 InputSystem이 반응하지 않으므로,
    // 여기서 API를 직접 호출해 각 UI 상태를 자동 재생한다.
    // 시나리오는 전이(Enter/Exit) 기반 이벤트 훅이 아닌, HealthSystem/PlayerRecovery의
    // 공용 이벤트를 시각화하는 목적이므로 UI 로직과는 결합하지 않는다.
    // 폴리싱 세션 완료 후 이 파일은 제거한다.
    [DisallowMultipleComponent]
    public class HealthUIVerifier : MonoBehaviour
    {
        [SerializeField] HealthSystem healthSystem;

        [Tooltip("자동 재생할 시나리오. 인스펙터에서 선택.")]
        [SerializeField] Scenario scenario = Scenario.DamageOnly;
        [SerializeField] float initialDelay = 0.5f;

        // PlayerRecovery는 InputSystem 액션에 묶여 있어 스크립트에서 강제로 회복 진행을
        // 트리거할 훅이 없다. 검증에는 HealthSystem.Heal(1)을 직접 호출해 회복 완료 시 UI
        // 페이드 로직을 확인한다. 회복 도중 UI 표현(50→100 스윕, 색전환)은 실제 Play 테스트에서 사람이 확인한다.
        public enum Scenario
        {
            None,
            DamageOnly,         // 시작 → TakeDamage(1)
            DamageThenHeal,     // 피격 → 잠시 후 Heal(1) → 페이드아웃
            DamageThenFail,     // 피격 → 잠시 후 다시 피격(0으로) → 실패 리셋
            DamageThenReset,    // 피격 → 잠시 후 ResetHealth (실패 없이 Reset만)
        }

        void Start()
        {
            if (healthSystem == null) return;
            if (scenario == Scenario.None) return;
            StartCoroutine(RunScenario());
        }

        IEnumerator RunScenario()
        {
            yield return new WaitForSeconds(initialDelay);
            Debug.Log($"[HealthUIVerifier] Step 1: TakeDamage → 1/2");
            healthSystem.TakeDamage(1);

            if (scenario == Scenario.DamageOnly) yield break;

            yield return new WaitForSeconds(2f);

            switch (scenario)
            {
                case Scenario.DamageThenHeal:
                    Debug.Log("[HealthUIVerifier] Step 2: Heal(1) → 2/2 → fade out");
                    healthSystem.Heal(1);
                    break;
                case Scenario.DamageThenFail:
                    Debug.Log("[HealthUIVerifier] Step 2: TakeDamage → 0/2 → fail");
                    healthSystem.TakeDamage(1);
                    break;
                case Scenario.DamageThenReset:
                    Debug.Log("[HealthUIVerifier] Step 2: ResetHealth");
                    healthSystem.ResetHealth();
                    break;
            }
        }

        [ContextMenu("Damage Once")]
        void DamageOnce()
        {
            if (healthSystem != null) healthSystem.TakeDamage(1);
        }

        [ContextMenu("Heal Once")]
        void HealOnce()
        {
            if (healthSystem != null) healthSystem.Heal(1);
        }

        [ContextMenu("Reset Health")]
        void ResetOnce()
        {
            if (healthSystem != null) healthSystem.ResetHealth();
        }
    }
}
