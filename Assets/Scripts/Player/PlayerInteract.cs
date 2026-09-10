using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread;

namespace ProjectT.Player
{
    // 상호작용 입력 처리 담당. 감지는 CoordinateDetectZone 에게 위임.
    // Interact 입력이 들어오면 DetectZone.CurrentTarget 을 읽어 TryBind 를 호출한다.
    // buffHolder 는 인스펙터에서 직접 연결(폴백 없음) — 자식 오브젝트에 부착될 가능성이 있고
    // 참조 위치를 명시적으로 관리하는 편이 오류를 조기 발견하기 쉽다.
    //
    // Interact 는 Input System 상에서 Hold 인터랙션을 사용한다.
    // - started  : 버튼이 눌리는 순간 (Hold 게이지 시작 신호로 활용)
    // - performed: Hold 시간이 채워져 실제 상호작용이 발생
    // - canceled : Hold 도중 손을 뗐거나 성공 후 릴리즈
    // 게이지 UI는 started/canceled/performed 를 구독해 시작·취소를 판단한다.
    public class PlayerInteract : MonoBehaviour
    {
        [Tooltip("범위 감지 담당 (플레이어의 자식 오브젝트). 인스펙터에서 직접 연결.")]
        [SerializeField] DetectZone detectZone;

        [Tooltip("소모할 실 버프 홀더. 인스펙터에서 직접 연결.")]
        [SerializeField] ThreadBuffHolder buffHolder;

        InputSystem_Actions _actions;

        // 다른 시스템(HoldGaugeUI 등)이 감지 대상 여부를 판단할 수 있도록 노출.
        public DetectZone DetectZone => detectZone;

        // Hold 게이지 UI 가 시작·취소·완료 시점을 알 수 있도록 이벤트로 노출.
        // Coordinate 근처(=DetectZone.CurrentTarget != null)일 때만 게이지가 의미가 있으므로
        // 실제 표시 여부 판단은 구독자(UI) 쪽에서 CurrentTarget 을 조회해 결정한다.
        public event Action OnInteractHoldStarted;
        public event Action OnInteractHoldCanceled;
        public event Action OnInteractHoldPerformed;

        void Awake()
        {
            _actions = new InputSystem_Actions();
        }

        void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Interact.started += OnInteractStarted;
            _actions.Player.Interact.performed += OnInteractPerformed;
            _actions.Player.Interact.canceled += OnInteractCanceled;
        }

        void OnDisable()
        {
            _actions.Player.Interact.started -= OnInteractStarted;
            _actions.Player.Interact.performed -= OnInteractPerformed;
            _actions.Player.Interact.canceled -= OnInteractCanceled;
            _actions.Player.Disable();
        }

        void OnDestroy()
        {
            _actions?.Dispose();
        }

        // started 는 버튼이 눌린 즉시 발생 → 게이지 UI가 표시를 시작하기 위한 신호로 사용.
        // 이 시점의 CurrentTarget 이 null 이어도 이벤트는 그대로 발행한다 —
        // 대상 판단은 구독자가 자체적으로 하도록 위임(관심사 분리).
        void OnInteractStarted(InputAction.CallbackContext ctx)
        {
            OnInteractHoldStarted?.Invoke();
        }

        void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            OnInteractHoldPerformed?.Invoke();

            if (detectZone == null)
            {
                Debug.LogWarning("[PlayerInteract] detectZone 참조가 비어 있습니다.");
                return;
            }
            if (buffHolder == null)
            {
                Debug.LogWarning("[PlayerInteract] buffHolder 참조가 비어 있습니다.");
                return;
            }

            var target = detectZone.CurrentTarget;
            if (target == null) return;

            target.TryBind(buffHolder);
        }

        // Hold 도중 릴리즈 또는 Hold 성공 후 릴리즈 시 모두 발생.
        // UI 관점에서는 두 경우 모두 "게이지 표시 종료"로 처리하면 되므로 하나의 이벤트로 통합.
        void OnInteractCanceled(InputAction.CallbackContext ctx)
        {
            OnInteractHoldCanceled?.Invoke();
        }
    }
}
