using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Session;

namespace ProjectT.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] InputActionReference moveAction;
        [SerializeField] float moveSpeed = 3f;
        [Tooltip("대각선에서 키 하나를 뗐을 때 바라보는 방향을 상하좌우로 바꾸기 전 기다리는 시간(초)")]
        [SerializeField] float diagonalReleaseGrace = 0.1f;

        Rigidbody2D body;
        Vector2Int pendingFacing;
        float pendingSince;

        // 8방향. 각 성분은 -1, 0, 1. 멈춰 있으면 마지막 값을 유지한다.
        public Vector2Int Facing { get; private set; } = Vector2Int.down;
        public bool IsMoving { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        void OnEnable()
        {
            moveAction.action.Enable();
        }

        void OnDisable()
        {
            body.linearVelocity = Vector2.zero;
            IsMoving = false;
        }

        void Update()
        {
            Vector2Int direction = IsInputLocked() ? Vector2Int.zero : ToDirection(moveAction.action.ReadValue<Vector2>());
            IsMoving = direction != Vector2Int.zero;
            UpdateFacing(direction);
        }

        void FixedUpdate()
        {
            Vector2Int direction = IsInputLocked() ? Vector2Int.zero : ToDirection(moveAction.action.ReadValue<Vector2>());
            body.linearVelocity = ((Vector2)direction).normalized * moveSpeed;
        }

        void UpdateFacing(Vector2Int direction)
        {
            if (direction == Vector2Int.zero || direction == Facing)
            {
                pendingFacing = Vector2Int.zero;
                return;
            }

            if (!IsReleasedFromDiagonal(direction))
            {
                Facing = direction;
                pendingFacing = Vector2Int.zero;
                return;
            }

            if (direction != pendingFacing)
            {
                pendingFacing = direction;
                pendingSince = Time.time;
            }

            if (Time.time - pendingSince >= diagonalReleaseGrace)
            {
                Facing = direction;
                pendingFacing = Vector2Int.zero;
            }
        }

        bool IsReleasedFromDiagonal(Vector2Int direction)
        {
            bool facingDiagonal = Facing.x != 0 && Facing.y != 0;
            bool directionCardinal = direction.x == 0 || direction.y == 0;
            return facingDiagonal && directionCardinal
                && (direction.x == Facing.x || direction.y == Facing.y);
        }

        static Vector2Int ToDirection(Vector2 input)
        {
            const float deadZone = 0.5f;
            int x = input.x > deadZone ? 1 : input.x < -deadZone ? -1 : 0;
            int y = input.y > deadZone ? 1 : input.y < -deadZone ? -1 : 0;
            return new Vector2Int(x, y);
        }

        static bool IsInputLocked()
        {
            GameSessionManager session = GameSessionManager.Instance;
            return session != null && session.IsInputLocked;
        }
    }
}
