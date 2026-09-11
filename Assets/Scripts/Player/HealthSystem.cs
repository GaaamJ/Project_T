using System;
using UnityEngine;

namespace ProjectT.Player
{
    // 체력 상태를 소유하는 별도 컴포넌트.
    // 왜 별도 컴포넌트인가:
    // - Player/Umia 모두 최대 체력을 공유해야 하므로, 한쪽에 소유시키면 다른 쪽이 인스펙터에서
    //   상대 오브젝트를 찾아 참조해야 한다. 별도 매니저 오브젝트로 두면 양쪽 다 대칭적으로 참조하고
    //   책임이 명확해진다.
    // - 회복(PlayerRecovery)·피격(Umia)·실패 트리거(StageManager) 모두 이 컴포넌트를 참조해
    //   체력 변화를 관찰/변경한다.
    //
    // 이벤트 노출로 UI/사운드/이펙트 등이 나중에 붙을 수 있도록 준비하되, 이번 세션에서는 UI 없음.
    public class HealthSystem : MonoBehaviour
    {
        [Tooltip("최대 체력(칸). 변경 시 인스펙터에서만 조정.")]
        [SerializeField] int maxHealth = 2;

        [Header("Debug")]
        [Tooltip("임시 검증용. 활성화 시 H 키로 TakeDamage(1)를 호출한다. 최종 빌드에서는 끈다.")]
        [SerializeField] bool enableDebugHotkey = true;

        int _currentHealth;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => _currentHealth;
        public bool IsFull => _currentHealth >= maxHealth;
        public bool IsDead => _currentHealth <= 0;

        // (current, max) 두 값을 모두 넘겨서 구독자가 별도 조회 없이 화면/디버그 표시에 사용할 수 있게 한다.
        public event Action<int, int> OnHealthChanged;
        // 피격으로 체력이 감소했을 때만 발화 — 회복(Heal)이나 리셋(ResetHealth)에서는 발화하지 않는다.
        // PlayerRecovery가 "우미아 피격" 취소 조건 훅으로 사용한다.
        public event Action OnDamaged;
        public event Action OnDied;

        void Awake()
        {
            _currentHealth = maxHealth;
            Debug.Log($"[HealthSystem] Awake initialized → {_currentHealth}/{maxHealth}");
        }

        void Start()
        {
            // 초기 UI/디버그 표시가 최초 상태를 한 번 받아갈 수 있도록 Start에서 한 번 발화.
            // Awake에서 발화하면 구독자가 아직 이벤트를 붙이기 전이라 놓칠 수 있음.
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        void Update()
        {
            if (!enableDebugHotkey) return;

            // H키는 임시 검증 용도. New Input System을 쓰는 프로젝트라 UnityEngine.Input이 아닌 Keyboard.current를 사용.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.hKey.wasPressedThisFrame)
            {
                Debug.Log("[HealthSystem] (Debug H) TakeDamage(1)");
                TakeDamage(1);
            }
        }

        // 피격 시 호출. 죽음(0 도달) 시 OnDied만 발화하고, 실제 스테이지 실패 처리는
        // StageManager가 이벤트를 구독해 TriggerStageFail을 호출하도록 위임 —
        // HealthSystem이 StageManager에 직접 의존하지 않도록.
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            if (IsDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            Debug.Log($"[HealthSystem] TakeDamage({amount}) → {_currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            OnDamaged?.Invoke();

            if (_currentHealth <= 0)
            {
                Debug.Log("[HealthSystem] Died");
                OnDied?.Invoke();
            }
        }

        // 회복 성공 시 호출. 최대치를 넘지 않도록 클램프.
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            if (IsFull) return;

            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
            Debug.Log($"[HealthSystem] Heal({amount}) → {_currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        // 스테이지 실패/재시작 시 호출. 최대 체력으로 즉시 복구.
        public void ResetHealth()
        {
            _currentHealth = maxHealth;
            Debug.Log($"[HealthSystem] ResetHealth → {_currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        // 인스펙터에서 우클릭 → Take Damage(1)로 실행 가능. 개발 편의 목적.
        [ContextMenu("Debug: Take Damage (1)")]
        void DebugTakeDamage()
        {
            TakeDamage(1);
        }

        [ContextMenu("Debug: Heal (1)")]
        void DebugHeal()
        {
            Heal(1);
        }

        [ContextMenu("Debug: Reset Health")]
        void DebugReset()
        {
            ResetHealth();
        }
    }
}
