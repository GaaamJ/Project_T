using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread;

namespace ProjectT.Player
{
    // 상호작용 입력 처리 담당. 감지는 CoordinateDetectZone 에게 위임.
    // Interact 입력이 들어오면 DetectZone.CurrentTarget 을 읽어 TryBind 를 호출한다.
    // buffHolder 는 인스펙터에서 직접 연결(폴백 없음) — 자식 오브젝트에 부착될 가능성이 있고
    // 참조 위치를 명시적으로 관리하는 편이 오류를 조기 발견하기 쉽다.
    public class PlayerInteract : MonoBehaviour
    {
        [Tooltip("범위 감지 담당 (플레이어의 자식 오브젝트). 인스펙터에서 직접 연결.")]
        [SerializeField] DetectZone detectZone;

        // Umia AI가 "플레이어가 상호작용 중인 좌표" 여부를 확인하기 위해 사용.
        public Coordinate.Coordinate CurrentInteractTarget => detectZone?.CurrentTarget;

        [Tooltip("소모할 실 버프 홀더. 인스펙터에서 직접 연결.")]
        [SerializeField] ThreadBuffHolder buffHolder;

        InputSystem_Actions actions;

        void Awake()
        {
            actions = new InputSystem_Actions();
        }

        void OnEnable()
        {
            actions.Player.Enable();
            actions.Player.Interact.performed += OnInteractPerformed;
        }

        void OnDisable()
        {
            actions.Player.Interact.performed -= OnInteractPerformed;
            actions.Player.Disable();
        }

        void OnDestroy()
        {
            actions?.Dispose();
        }

        void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
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

            bool result = target.TryBind(buffHolder);
            Debug.Log($"[PlayerInteract] Interact TryBind: {result}");
        }
    }
}
