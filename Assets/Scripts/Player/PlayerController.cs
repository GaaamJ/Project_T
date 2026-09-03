using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread;
using ProjectT.Coordinate;

namespace ProjectT.Player
{
    // 이슈 #15: 플레이어의 이동/공격/상호작용을 하나의 컴포넌트로 묶는다.
    // 아직 캐릭터 컨트롤러 구조가 확정되지 않았기 때문에, 별도 상태 머신이나 InputAction 애셋 없이
    // 최소한의 조작 루프만 검증할 수 있는 "테스트용" 조작기로 유지한다.
    //
    // 이동: Rigidbody2D.linearVelocity 로 처리한다. Transform 이동은 Collider와의 상호작용을 깨뜨리므로 사용하지 않음.
    // 공격: 커서 방향 Raycast → Yarn.TakeHit(buffHolder). 실은 Trigger 콜라이더일 수 있으므로 useTriggers=true.
    // 상호작용: 주변 OverlapCircle로 Coordinate 검출 → TryBind(buffHolder). Coordinate 콜라이더도 Trigger 가능성 있음.
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

        void Update()
        {
            // 이 프로젝트는 새 InputSystem 패키지를 사용한다 (BuffLoopDebugger 참고).
            // Update 에서 입력을 받고, 이동은 FixedUpdate 에서 처리 → 물리 스텝과 동기화.
            HandleAttackInput();
            HandleInteractInput();
        }

        void FixedUpdate()
        {
            // 이동은 물리 스텝 주기에서 linearVelocity 로 처리해야 Collider 관통을 방지할 수 있다.
            HandleMovement();
        }

        void HandleMovement()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            // WASD를 8방향 정규화 벡터로 합성. 대각선이 √2 배 빨라지는 것을 방지하기 위해 normalize.
            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed) dir.y -= 1f;
            if (kb.aKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed) dir.x += 1f;

            if (dir.sqrMagnitude > 1f) dir.Normalize();

            rb.linearVelocity = dir * speed;
        }

        void HandleAttackInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            var cam = Camera.main;
            // Camera.main 은 MainCamera 태그가 붙은 카메라를 반환한다. 씬에 없다면 공격 처리 자체가 무의미.
            if (cam == null) return;

            // 마우스 화면 좌표를 월드 좌표로 변환. 카메라는 z=-10 부근이므로 XY 평면 기준으로 방향만 뽑아낸다.
            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

            Vector2 origin = transform.position;
            Vector2 dir = ((Vector2)worldPos - origin);
            // 커서가 플레이어와 완전히 겹치면 방향이 0 이 되어 Raycast 결과가 예측 불가 → 조용히 skip.
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            int hitCount = Physics2D.Raycast(origin, dir, attackFilter, attackHits, attackRange);
            // 여러 개 맞으면 가장 가까운 대상 하나만 처리. Raycast 결과는 이미 거리 순 정렬(Unity 문서 기준).
            for (int i = 0; i < hitCount; i++)
            {
                var hit = attackHits[i];
                if (hit.collider == null) continue;

                // Yarn 은 자기 자신 컴포넌트로 TakeHit 를 처리한다.
                // GetComponentInParent 를 쓰는 이유: 콜라이더가 Yarn 루트가 아닌 자식 오브젝트에 붙어 있는 경우도 커버.
                // 풀네임(ProjectT.Thread.Yarn) 사용: Yarn Spinner 패키지가 최상위 'Yarn' 네임스페이스를 점유해
                // 짧은 이름을 쓰면 CS0118 (namespace used like a type) 이 발생하기 때문.
                var yarn = hit.collider.GetComponentInParent<ProjectT.Thread.Yarn>();
                if (yarn != null)
                {
                    yarn.TakeHit(buffHolder);
                    return;
                }
            }
        }

        void HandleInteractInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.eKey.wasPressedThisFrame) return;

            Vector2 origin = transform.position;
            int hitCount = Physics2D.OverlapCircle(origin, interactRange, interactFilter, interactHits);

            // 감지된 첫 Coordinate 에만 바인딩 시도. 여러 개가 겹치는 케이스는 현 스코프 밖.
            for (int i = 0; i < hitCount; i++)
            {
                var col = interactHits[i];
                if (col == null) continue;

                var coordinate = col.GetComponentInParent<Coordinate.Coordinate>();
                if (coordinate == null) continue;

                bool result = coordinate.TryBind(buffHolder);
                // 상호작용 결과는 이슈 요구대로 콘솔에 남긴다 — 아직 UI 피드백이 없기 때문.
                Debug.Log($"[PlayerController] Interact TryBind: {result}");
                return;
            }
        }

        void OnDrawGizmosSelected()
        {
            // 인스펙터에서 선택했을 때 공격/상호작용 범위를 시각화해 튜닝을 돕는다.
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
