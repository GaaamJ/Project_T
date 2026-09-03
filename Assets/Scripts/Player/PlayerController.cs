using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread;
using ProjectT.Coordinate;

namespace ProjectT.Player
{
    // 이슈 #15: 플레이어의 이동/공격/상호작용을 하나의 컴포넌트로 묶는다.
    // 입력은 InputSystem_Actions 자동 생성 클래스를 통해 InputAction 애셋과 연결한다.
    // 이동: Rigidbody2D.linearVelocity 로 처리 (Transform 이동은 Collider 관통 문제).
    // 공격: Attack.performed 콜백 → 커서 방향 Raycast → Yarn.TakeHit. useTriggers=true.
    // 상호작용: Interact.performed 콜백 (Hold 완료 시점) → OverlapCircle → Coordinate.TryBind.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ThreadBuffHolder))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float speed = 6f;

        [Header("Attack")]
        [Tooltip("커서 방향으로 뻗는 공격 판정 거리 (월드 유닛)")]
        [SerializeField] float attackRange = 3f;
        [Tooltip("공격에 맞힐 대상 레이어 (Yarn 레이어 지정)")]
        [SerializeField] LayerMask attackMask;

        [Header("Interact")]
        [Tooltip("상호작용 판정 반경 (플레이어 중심)")]
        [SerializeField] float interactRange = 1.5f;
        [Tooltip("상호작용 대상 레이어 (Coordinate 레이어 지정)")]
        [SerializeField] LayerMask interactMask;

        Rigidbody2D rb;
        ThreadBuffHolder buffHolder;
        InputSystem_Actions actions;

        // Raycast/Overlap 결과 버퍼 — 매 프레임 new 를 피해 GC 압박을 낮추기 위해 재사용한다.
        readonly RaycastHit2D[] attackHits = new RaycastHit2D[8];
        readonly Collider2D[] interactHits = new Collider2D[8];

        // ContactFilter2D 는 프로젝트 전역 Physics2D.queriesHitTriggers 설정과 무관하게
        // Trigger 콜라이더를 확실히 포함시키기 위해 useTriggers=true 로 명시적으로 구성한다.
        ContactFilter2D attackFilter;
        ContactFilter2D interactFilter;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            buffHolder = GetComponent<ThreadBuffHolder>();
            actions = new InputSystem_Actions();

            attackFilter = new ContactFilter2D();
            attackFilter.SetLayerMask(attackMask);
            attackFilter.useTriggers = true;
            // ContactFilter2D는 SetLayerMask 후 useLayerMask 를 명시적으로 켜야 마스크가 적용된다.
            attackFilter.useLayerMask = true;

            interactFilter = new ContactFilter2D();
            interactFilter.SetLayerMask(interactMask);
            interactFilter.useTriggers = true;
            interactFilter.useLayerMask = true;
        }

        void OnEnable()
        {
            actions.Player.Enable();
            actions.Player.Attack.performed += OnAttackPerformed;
            actions.Player.Interact.performed += OnInteractPerformed;
        }

        void OnDisable()
        {
            actions.Player.Attack.performed -= OnAttackPerformed;
            actions.Player.Interact.performed -= OnInteractPerformed;
            actions.Player.Disable();
        }

        void OnDestroy()
        {
            // InputActionAsset 은 IDisposable — 명시적으로 해제해야 도메인 리로드/씬 전환 시 leak 방지.
            actions?.Dispose();
        }

        void FixedUpdate()
        {
            // Move는 Value 액션이라 매 프레임 폴링이 자연스럽다. 이동은 물리 스텝에서 처리해야 Collider 관통 방지.
            Vector2 dir = actions.Player.Move.ReadValue<Vector2>();
            // Move 액션의 2DVector composite는 이미 정규화된 값을 반환하지만, 방어적으로 클램프.
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            rb.linearVelocity = dir * speed;
        }

        void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            var cam = Camera.main;
            if (cam == null) return;

            // 마우스 위치는 Attack 액션이 아니라 별도 조회 — 커서 위치 자체는 어떤 액션에도 바인딩되어 있지 않음.
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

            Vector2 origin = transform.position;
            Vector2 dir = ((Vector2)worldPos - origin);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            int hitCount = Physics2D.Raycast(origin, dir, attackFilter, attackHits, attackRange);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = attackHits[i];
                if (hit.collider == null) continue;

                // 풀네임(ProjectT.Thread.Yarn): Yarn Spinner 패키지가 최상위 'Yarn' 네임스페이스를 점유해 CS0118 회피.
                var yarn = hit.collider.GetComponentInParent<ProjectT.Thread.Yarn>();
                if (yarn != null)
                {
                    yarn.TakeHit(buffHolder);
                    return;
                }
            }
        }

        void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            Vector2 origin = transform.position;
            int hitCount = Physics2D.OverlapCircle(origin, interactRange, interactFilter, interactHits);

            for (int i = 0; i < hitCount; i++)
            {
                var col = interactHits[i];
                if (col == null) continue;

                var coordinate = col.GetComponentInParent<Coordinate.Coordinate>();
                if (coordinate == null) continue;

                bool result = coordinate.TryBind(buffHolder);
                Debug.Log($"[PlayerController] Interact TryBind: {result}");
                return;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
