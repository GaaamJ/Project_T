using UnityEngine;

namespace ProjectT.Umia
{
    // 우미아 기본 동행 시스템.
    // 플레이어 이동 방향의 "바로 뒤"에서 SmoothDamp로 부드럽게 따라온다.
    // 이번 스코프: 상태머신(배회/앞지르기 등)은 구현하지 않음.
    //
    // 설계 결정:
    // - 플레이어의 Rigidbody2D.linearVelocity를 참조해 이동 방향을 추적한다.
    //   (Transform 델타 계산 대비: 물리 스텝과 동기화되고, 정지 시 델타=0 헷갈림 없음)
    // - 우미아는 Rigidbody2D가 없으므로 transform.position을 직접 수정한다.
    //   (Rigidbody2D가 추가된다면 rb.MovePosition으로 전환 필요.)
    // - 플레이어 참조는 Inspector에서 직접 연결한다.
    //   전역 접근/Find 계열보다 명시적이고 씬 재구성에 안전하다.
    public class UmiaFollow : MonoBehaviour
    {
        [SerializeField] Transform player;             // Inspector에서 연결
        [SerializeField] float followDistance = 1.2f;  // 플레이어 뒤 거리
        [SerializeField] float smoothTime = 0.2f;      // 반응 속도 (낮을수록 빠름)
        [SerializeField] float moveThreshold = 0.05f;  // 이 속도 미만이면 정지로 간주

        // 탑뷰 2D 기본값: 아래쪽을 "뒤"로 간주 — 시작 직후 방향 갱신 전 위치가 어색하지 않도록.
        Vector2 _lastMoveDir = Vector2.down;
        Vector2 _velocity;   // SmoothDamp 내부 상태 — 프레임 간 유지 필요
        Rigidbody2D _playerRb;

        void Awake()
        {
            if (player != null)
            {
                _playerRb = player.GetComponent<Rigidbody2D>();
            }
        }

        void FixedUpdate()
        {
            if (player == null || _playerRb == null) return;

            // 플레이어 이동 방향 추적: 실질적으로 움직일 때만 lastMoveDir 갱신.
            // 정지 상태에서 lastMoveDir을 초기화하면 뒤 위치가 사라져 우미아가 겹침.
            Vector2 vel = _playerRb.linearVelocity;
            if (vel.sqrMagnitude > moveThreshold * moveThreshold)
            {
                _lastMoveDir = vel.normalized;
            }

            // 타겟 = 플레이어 위치에서 이동 방향의 반대(뒤)로 followDistance만큼.
            Vector2 playerPos = player.position;
            Vector2 target = playerPos + (-_lastMoveDir * followDistance);

            // SmoothDamp: 감쇠 이동. 물리 스텝(FixedUpdate)에서 호출하므로 Time.deltaTime 대신
            // Time.fixedDeltaTime을 명시적으로 전달해 프레임레이트 독립성 보장.
            Vector2 current = transform.position;
            Vector2 next = Vector2.SmoothDamp(current, target, ref _velocity, smoothTime,
                                              Mathf.Infinity, Time.fixedDeltaTime);
            // z는 유지 — 스프라이트 정렬/카메라 세팅에 영향 주지 않도록.
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }
}
