using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectT.Player
{
    // 이동만 담당. 공격/상호작용은 각각 PlayerAttack / PlayerInteract 로 분리됨.
    // 이동은 Rigidbody2D.linearVelocity 로 처리 — Transform 이동은 Collider 관통 문제가 있음.
    // InputSystem_Actions 인스턴스는 각 컴포넌트가 독립적으로 소유한다.
    // 하나의 액션 애셋 인스턴스를 공유하지 않는 이유: 컴포넌트별로 Enable/Disable
    // 라이프사이클을 독립적으로 관리할 수 있고, 서로의 활성 상태에 의존하지 않게 하기 위함.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float speed = 6f;

        Rigidbody2D rb;
        InputSystem_Actions actions;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            actions = new InputSystem_Actions();
        }

        void OnEnable()
        {
            actions.Player.Enable();
        }

        void OnDisable()
        {
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
    }
}
