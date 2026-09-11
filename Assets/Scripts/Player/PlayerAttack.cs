using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread;

namespace ProjectT.Player
{
    // 공격 담당. 커서 방향 Raycast → Yarn.TakeHit.
    // 원래 PlayerController에서 이동/공격/상호작용을 통합했었지만, 각 책임을 분리해 유지보수성을 높였다.
    [RequireComponent(typeof(ThreadBuffHolder))]
    public class PlayerAttack : MonoBehaviour
    {
        [Tooltip("커서 방향으로 뻗는 공격 판정 거리 (월드 유닛)")]
        [SerializeField] float attackRange = 3f;
        [Tooltip("공격에 맞힐 대상 레이어 (Yarn 레이어 지정)")]
        [SerializeField] LayerMask attackMask;

        // 공격 입력이 실제로 수행된 순간 발화. 회복 집중(PlayerRecovery)의 취소 훅으로 쓰인다.
        // 명중 여부와 무관하게 "공격 액션이 트리거됐다"는 사실만 전달한다 —
        // 기획서상 취소 조건은 "공격"(=공격 시도)이지 "공격 명중"이 아니기 때문.
        public event Action OnAttacked;

        ThreadBuffHolder _buffHolder;
        InputSystem_Actions _actions;

        // Raycast 결과 버퍼 — 매 프레임 new 를 피해 GC 압박을 낮추기 위해 재사용한다.
        readonly RaycastHit2D[] _attackHits = new RaycastHit2D[8];

        // ContactFilter2D 는 프로젝트 전역 Physics2D.queriesHitTriggers 설정과 무관하게
        // Trigger 콜라이더를 확실히 포함시키기 위해 useTriggers=true 로 명시적으로 구성한다.
        ContactFilter2D _attackFilter;

        void Awake()
        {
            _buffHolder = GetComponent<ThreadBuffHolder>();
            _actions = new InputSystem_Actions();

            _attackFilter = new ContactFilter2D();
            _attackFilter.SetLayerMask(attackMask);
            _attackFilter.useTriggers = true;
            // ContactFilter2D는 SetLayerMask 후 useLayerMask 를 명시적으로 켜야 마스크가 적용된다.
            _attackFilter.useLayerMask = true;
        }

        void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Attack.performed += OnAttackPerformed;
        }

        void OnDisable()
        {
            _actions.Player.Attack.performed -= OnAttackPerformed;
            _actions.Player.Disable();
        }

        void OnDestroy()
        {
            _actions?.Dispose();
        }

        void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            // 회복 집중 취소 훅. 공격 액션이 발화된 시점 자체가 취소 트리거이므로
            // Raycast/Camera 유효성보다 먼저 발화한다(공격 액션이 감지된 순간에 취소되는 게 맞음).
            OnAttacked?.Invoke();

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

            int hitCount = Physics2D.Raycast(origin, dir, _attackFilter, _attackHits, attackRange);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _attackHits[i];
                if (hit.collider == null) continue;

                // 풀네임(ProjectT.Thread.Yarn): Yarn Spinner 패키지가 최상위 'Yarn' 네임스페이스를 점유해 CS0118 회피.
                var yarn = hit.collider.GetComponentInParent<ProjectT.Thread.Yarn>();
                if (yarn != null)
                {
                    yarn.TakeHit(_buffHolder);
                    return;
                }
            }
        }
    }
}
