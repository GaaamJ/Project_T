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

        [Tooltip("피격 후 무적 지속 시간(초). 이 시간 동안 추가 피격은 무시된다.")]
        [SerializeField] float invincibilityDuration = 0.5f;

        [Header("Debug")]
        [Tooltip("임시 검증용. 활성화 시 H 키로 TakeDamage(1)를 호출한다. 최종 빌드에서는 끈다.")]
        [SerializeField] bool enableDebugHotkey = true;

        int _currentHealth;
        // 남은 무적 시간. 0보다 크면 무적 상태. Update에서 Time.deltaTime으로 감소.
        // 왜 Coroutine이 아닌 Update인가: 이미 Update가 있어 추가 오버헤드가 없고,
        // ResetHealth 등에서 즉시 강제 해제할 때 코루틴 취소 관리가 필요 없어 상태 일관성 유지에 유리.
        float _invincibilityRemaining;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => _currentHealth;
        public bool IsFull => _currentHealth >= maxHealth;
        public bool IsDead => _currentHealth <= 0;
        public bool IsInvincible => _invincibilityRemaining > 0f;

        // (current, max) 두 값을 모두 넘겨서 구독자가 별도 조회 없이 화면/디버그 표시에 사용할 수 있게 한다.
        public event Action<int, int> OnHealthChanged;
        // 피격으로 체력이 감소했을 때만 발화 — 회복(Heal)이나 리셋(ResetHealth)에서는 발화하지 않는다.
        // PlayerRecovery가 "우미아 피격" 취소 조건 훅으로 사용한다.
        public event Action OnDamaged;
        public event Action OnDied;
        // 무적 시작/종료 이벤트. UI 깜빡임 등 시각 표현이 붙을 수 있도록 노출.
        // ResetHealth로 인한 강제 해제 시에도 OnInvincibilityEnded는 발화한다 (구독자가 상태 동기화 가능하도록).
        public event Action OnInvincibilityStarted;
        public event Action OnInvincibilityEnded;

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
            // 무적 타이머 감소. 매 프레임 실행되지만 IsInvincible=false일 때는 조건 분기로 즉시 스킵.
            // 포즈 기능이 없으므로 Time.deltaTime을 그대로 사용 (Time.unscaledDeltaTime 아님).
            if (_invincibilityRemaining > 0f)
            {
                _invincibilityRemaining -= Time.deltaTime;
                if (_invincibilityRemaining <= 0f)
                {
                    _invincibilityRemaining = 0f;
                    Debug.Log("[HealthSystem] Invincibility ended (timer)");
                    OnInvincibilityEnded?.Invoke();
                }
            }

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
            // IsDead 체크가 무적 체크보다 앞선다:
            // 이미 죽은 상태에서는 무적 여부와 무관하게 무시하고, "무적으로 차단" 로그가 사후에 남지 않도록.
            if (IsDead) return;
            if (IsInvincible)
            {
                Debug.Log($"[HealthSystem] TakeDamage({amount}) blocked by invincibility (remaining={_invincibilityRemaining:F2}s)");
                return;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            Debug.Log($"[HealthSystem] TakeDamage({amount}) → {_currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            OnDamaged?.Invoke();

            if (_currentHealth <= 0)
            {
                Debug.Log("[HealthSystem] Died");
                OnDied?.Invoke();
                // 죽은 순간에는 무적을 시작하지 않는다 — 이미 IsDead가 후속 TakeDamage를 막고,
                // 사망 후 무적 상태가 남으면 ResetHealth 이후에도 의도치 않은 무적으로 남을 수 있다.
                return;
            }

            // 정상 피격 후에만 무적 시작. duration이 0 이하로 설정된 경우 무적 미적용.
            if (invincibilityDuration > 0f)
            {
                _invincibilityRemaining = invincibilityDuration;
                Debug.Log($"[HealthSystem] Invincibility started ({invincibilityDuration:F2}s)");
                OnInvincibilityStarted?.Invoke();
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
        // 무적 상태도 함께 강제 해제한다 — 실패→재시작 직후에도 이전 무적이 남아
        // "재시작 직후 피격이 차단되는" 상태를 방지하기 위해.
        public void ResetHealth()
        {
            _currentHealth = maxHealth;
            Debug.Log($"[HealthSystem] ResetHealth → {_currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);

            // 진행 중이던 무적을 즉시 해제. 구독자가 상태를 동기화할 수 있도록 Ended 이벤트도 발화.
            if (_invincibilityRemaining > 0f)
            {
                _invincibilityRemaining = 0f;
                Debug.Log("[HealthSystem] Invincibility ended (forced by ResetHealth)");
                OnInvincibilityEnded?.Invoke();
            }
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
