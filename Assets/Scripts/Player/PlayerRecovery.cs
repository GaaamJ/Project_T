using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectT.Player
{
    // V 홀드 회복 집중.
    // 왜 별도 컴포넌트인가:
    // - PlayerMovement/PlayerAttack 같은 기존 조작 컴포넌트가 각자 InputSystem_Actions 인스턴스를
    //   독립적으로 소유하는 프로젝트 규칙을 그대로 따른다.
    // - HealthSystem은 상태만 소유(피격/회복/리셋)하고, "언제 회복이 시작·취소되는가"의 정책은
    //   PlayerRecovery가 담당한다. 이렇게 하면 나중에 회복 방해 구역 같은 조건이 추가돼도
    //   HealthSystem을 건드리지 않고 여기서만 확장할 수 있다.
    //
    // 취소 조건 (기획서 규칙):
    //   - 이동(Move 입력) — Move 액션 값 사용
    //   - 공격 — PlayerAttack이 발화하는 OnAttacked 이벤트를 구독해 취소 훅
    //   - 우미아 피격 — HealthSystem.OnDamaged 이벤트를 구독해 취소 훅
    //   - 버튼 해제 — Recover 액션 canceled
    //   - 패리 — 패리 시스템 미구현. 훅 슬롯(NotifyParried)만 노출하고 아직 연결하지 않음.
    //
    // 최대 체력에서 V 입력 시 집중 자체가 시작되지 않는다(기획서 규칙).
    // 취소 후 재집중 시 진행도는 0에서 다시 시작(_progress를 다시 0으로 세팅).
    public class PlayerRecovery : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] HealthSystem healthSystem;
        [Tooltip("공격 이벤트를 발화하는 PlayerAttack. 취소 조건 훅.")]
        [SerializeField] PlayerAttack playerAttack;

        [Header("Config")]
        [Tooltip("집중 완료까지 필요한 시간(초). 기획서 임시 수치.")]
        [SerializeField] float focusDuration = 2f;
        [Tooltip("Move 입력이 이 값 이상이면 '움직임'으로 판정한다.")]
        [SerializeField] float moveThreshold = 0.1f;

        InputSystem_Actions _actions;

        // 집중 상태 머신: Idle → Focusing → (완료 시 Idle 복귀)
        // 취소는 Focusing → Idle로 즉시 전이.
        bool _isFocusing;
        float _progress;

        // 외부 관측용 이벤트 — UI/사운드/이펙트 훅 슬롯. 이번 세션에서는 UI 없이 로그로만 확인.
        public event Action OnFocusStarted;
        // (progress 0~1) 진행도 갱신. 매 프레임 발화.
        public event Action<float> OnFocusProgress;
        public event Action OnFocusCanceled;
        public event Action OnFocusCompleted;

        public bool IsFocusing => _isFocusing;
        public float Progress => _progress;

        void Awake()
        {
            _actions = new InputSystem_Actions();
        }

        void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Recover.canceled += OnRecoverReleased;

            if (playerAttack != null)
                playerAttack.OnAttacked += HandleAttacked;

            if (healthSystem != null)
                healthSystem.OnDamaged += HandleDamaged;
        }

        void OnDisable()
        {
            _actions.Player.Recover.canceled -= OnRecoverReleased;
            _actions.Player.Disable();

            if (playerAttack != null)
                playerAttack.OnAttacked -= HandleAttacked;

            if (healthSystem != null)
                healthSystem.OnDamaged -= HandleDamaged;
        }

        void OnDestroy()
        {
            // InputActionAsset은 IDisposable — 도메인 리로드/씬 전환 시 leak 방지 위해 명시적 해제.
            _actions?.Dispose();
        }

        void Update()
        {
            if (healthSystem == null) return;

            bool recoverHeld = _actions.Player.Recover.IsPressed();
            Vector2 moveInput = _actions.Player.Move.ReadValue<Vector2>();
            bool isMoving = moveInput.sqrMagnitude > moveThreshold * moveThreshold;

            if (!_isFocusing)
            {
                // 시작 조건: 버튼 유지 + Move 입력 없음 + 최대 체력 아님.
                // 최대 체력에서는 집중 자체를 시작하지 않는다(기획서 결정).
                if (recoverHeld && !isMoving && !healthSystem.IsFull)
                {
                    StartFocus();
                }
                return;
            }

            // Focusing 중 취소 판정 순서:
            //   1) 버튼 해제 (canceled 콜백에서도 처리하지만, Update 폴링으로도 방어)
            //   2) 이동 감지
            if (!recoverHeld)
            {
                CancelFocus("button released");
                return;
            }
            if (isMoving)
            {
                CancelFocus("moved");
                return;
            }

            _progress += Time.deltaTime;
            OnFocusProgress?.Invoke(Mathf.Clamp01(_progress / focusDuration));

            if (_progress >= focusDuration)
            {
                CompleteFocus();
            }
        }

        void StartFocus()
        {
            _isFocusing = true;
            _progress = 0f;
            Debug.Log("[PlayerRecovery] Focus started");
            OnFocusStarted?.Invoke();
            OnFocusProgress?.Invoke(0f);
        }

        // 취소 사유는 로그로만 남긴다. 재집중 시 _progress를 0으로 리셋하는 게 기획서 규칙.
        void CancelFocus(string reason)
        {
            if (!_isFocusing) return;
            _isFocusing = false;
            _progress = 0f;
            Debug.Log($"[PlayerRecovery] Focus canceled ({reason})");
            OnFocusCanceled?.Invoke();
        }

        void CompleteFocus()
        {
            _isFocusing = false;
            _progress = 0f;
            Debug.Log("[PlayerRecovery] Focus completed → Heal(1)");
            OnFocusCompleted?.Invoke();
            healthSystem?.Heal(1);
        }

        void OnRecoverReleased(InputAction.CallbackContext ctx)
        {
            // 버튼 해제는 Update에서도 감지되지만, 콜백으로 잡으면 반응이 한 프레임 빨라진다.
            CancelFocus("button released");
        }

        void HandleAttacked()
        {
            CancelFocus("attacked");
        }

        void HandleDamaged()
        {
            CancelFocus("umia damaged");
        }

        // 패리 시스템이 나중에 연결될 때 사용할 훅. 지금은 어디에서도 호출되지 않는다.
        // 호출부 연결 지점만 남겨 두고, 실제 배선은 패리 시스템 구현 이슈에서 처리한다.
        public void NotifyParried()
        {
            CancelFocus("parried");
        }
    }
}
