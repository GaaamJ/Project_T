using UnityEngine;

namespace ProjectT.Umia
{
    // 우미아 동행 상태머신.
    //
    // 상태:
    //   Idle    : 플레이어가 idleDelay초 이상 정지 → 플레이어 주변을 배회
    //   Moving  : 플레이어 이동 감지 → 뒤따르기(c-1) + 가끔 앞지르기(c-3)
    //
    // 설계 결정:
    // - 플레이어 Rigidbody2D.linearVelocity로 이동/정지를 판정. Transform 델타보다
    //   물리 스텝과 동기화가 명확하고, 정지 시 sqrMagnitude가 0에 수렴해 임계값 판정이 안정적.
    // - 우미아는 Rigidbody2D 없이 transform.position을 직접 조작(현재 씬 구조 유지).
    // - _lastMoveDir은 "실제로 움직였을 때만" 갱신 — 정지 시 방향이 사라지면 뒤 위치가 없어져
    //   우미아가 플레이어에 겹치는 문제를 방지.
    // - 상태 전환은 매 FixedUpdate에서 판정. 코루틴은 B케이스 지연/과도현상용으로만 사용
    //   (짧고 명확한 시퀀스라 코루틴이 상태머신 본체보다 표현이 단순).
    // - 플레이어 참조는 Inspector 직결 — 전역 접근/Find 계열보다 안전.
    public class UmiaFollow : MonoBehaviour
    {
        enum UmiaState { Idle, Moving }

        [Header("References")]
        [SerializeField] Transform player;

        [Header("Follow (c-1)")]
        [SerializeField] float followDistance = 1.2f;      // 플레이어 뒤 거리
        [SerializeField] float smoothTime = 0.2f;          // 일반 따라오기 응답성
        [SerializeField] float catchUpSmoothTime = 0.05f;       // B케이스 빠른 따라붙기
        [SerializeField] float catchUpTriggerDistance = 3f;  // B케이스: 이 거리 이상 멀어지면 뛰어오기
        [SerializeField] float moveThreshold = 0.05f;         // 이 속도 미만이면 정지 취급

        [Header("Idle / Wander")]
        [SerializeField] float idleDelay = 0.5f;           // 이 시간 이상 정지해야 Idle 진입
        [SerializeField] float wanderSpeed = 0.8f;         // 배회 SmoothDamp 시간(smoothTime 대체)

        [Header("Overtake (c-3)")]
        [SerializeField] float overtakeDistance = 1.5f;    // 앞으로 나가는 거리

        // --- 런타임 상태 ---
        // 탑뷰 기본값: 시작 직후 방향 갱신 전에도 "아래를 뒤"로 삼아 초기 위치가 어색하지 않게.
        Vector2 _lastMoveDir = Vector2.down;
        Vector2 _velocity;                  // SmoothDamp 내부 상태 — 프레임 간 유지 필수
        Rigidbody2D _playerRb;

        UmiaState _state = UmiaState.Moving;
        float _currentSmoothTime;           // Moving 상태에서 실제 사용할 smoothTime (A/B 케이스로 달라짐)

        // Idle 관련
        float _idleTimer;                   // 플레이어가 정지 상태로 있는 누적 시간
        Vector2 _idleBasePos;               // Idle 진입 시점의 플레이어 위치(배회 중심점)
        Vector2 _wanderTarget;              // 현재 배회 목적지
        float _wanderRepickTimer;           // 새 배회 타겟까지 남은 지연 시간

        // Moving / B케이스
        bool _frozen;                       // B케이스 지연 중이면 위치 업데이트 스킵
        bool _catchingUp;                   // B케이스 재개 후 아직 followDistance 안에 못 들어옴

        // c-3 앞지르기
        float _overtakeCheckTimer;          // 다음 c-3 판정까지 남은 시간
        bool _overtaking;
        Vector2 _overtakeTarget;
        float _overtakeElapsed;             // 앞지르기 지속 시간(안전 상한용)

        void Awake()
        {
            if (player != null)
            {
                _playerRb = player.GetComponent<Rigidbody2D>();
            }
            _currentSmoothTime = smoothTime;
            ScheduleNextOvertakeCheck();
        }

        void FixedUpdate()
        {
            if (player == null || _playerRb == null) return;

            // B케이스 지연 중: 플레이어가 catchUpTriggerDistance 이상 멀어지면 뛰어오기 시작.
            if (_frozen)
            {
                Vector2 followTarget = (Vector2)player.position + (-_lastMoveDir * followDistance);
                if (Vector2.Distance((Vector2)transform.position, followTarget) >= catchUpTriggerDistance)
                {
                    _frozen = false;
                    _currentSmoothTime = catchUpSmoothTime;
                    _catchingUp = true;
                    _velocity = Vector2.zero;
                    Debug.Log("[UmiaFollow] B케이스: 뛰어오기 시작");
                }
                else
                {
                    return;
                }
            }

            Vector2 vel = _playerRb.linearVelocity;
            bool isMoving = vel.sqrMagnitude > moveThreshold * moveThreshold;

            // 방향은 실제 이동 중에만 갱신 — 정지 시 방향 잔상 유지.
            if (isMoving)
            {
                _lastMoveDir = vel.normalized;
            }

            UpdateStateMachine(isMoving);
            ApplyMovement();
        }

        // === 상태 전이 ===
        void UpdateStateMachine(bool playerIsMoving)
        {
            switch (_state)
            {
                case UmiaState.Moving:
                    if (!playerIsMoving)
                    {
                        // 플레이어 정지 지속 시간 누적. idleDelay 넘으면 Idle 진입.
                        _idleTimer += Time.fixedDeltaTime;
                        if (_idleTimer >= idleDelay)
                        {
                            EnterIdle();
                        }
                    }
                    else
                    {
                        _idleTimer = 0f;
                        UpdateOvertake();  // Moving 지속 중 c-3 판정
                    }
                    break;

                case UmiaState.Idle:
                    if (playerIsMoving)
                    {
                        EnterMoving();
                    }
                    else
                    {
                        // 배회 타겟 재선정 타이머
                        _wanderRepickTimer -= Time.fixedDeltaTime;
                        float distSq = ((Vector2)transform.position - _wanderTarget).sqrMagnitude;
                        if (_wanderRepickTimer <= 0f || distSq < 0.01f) // 0.1f^2
                        {
                            PickNewWanderTarget();
                        }
                    }
                    break;
            }
        }

        void EnterIdle()
        {
            _state = UmiaState.Idle;
            _idleBasePos = player.position;
            // 앞지르기/캐치업 상태는 Idle 진입 시 정리.
            _overtaking = false;
            _catchingUp = false;
            _currentSmoothTime = smoothTime;
            PickNewWanderTarget();
        }

        void EnterMoving()
        {
            _state = UmiaState.Moving;
            _idleTimer = 0f;

            // Idle → Moving 전환 시 단 한 번 70/30 roll.
            bool caseB = Random.value < 0.3f;
            if (caseB)
            {
                Debug.Log("[UmiaFollow] MoveStart B (지연) — 3유닛 멀어지면 뛰어옴");
                _frozen = true;
                _velocity = Vector2.zero;
            }
            else
            {
                Debug.Log("[UmiaFollow] MoveStart A (즉시)");
                _currentSmoothTime = smoothTime;
                _catchingUp = false;
            }

            // Moving 진입 직후부터 c-3 판정 타이머 시작.
            ScheduleNextOvertakeCheck();
        }

        // === 배회 ===
        void PickNewWanderTarget()
        {
            // 반지름 2유닛 원 안에서 랜덤 배회 — 일정 방향에 치우치지 않고 자연스럽게 어슬렁거리는 느낌.
            _wanderTarget = _idleBasePos + Random.insideUnitCircle * 2f;
            _wanderRepickTimer = Random.Range(1.5f, 3.0f);
        }

        // === c-3 앞지르기 판정/갱신 ===
        void ScheduleNextOvertakeCheck()
        {
            _overtakeCheckTimer = Random.Range(10f, 20f);
        }

        void UpdateOvertake()
        {
            if (_overtaking)
            {
                _overtakeElapsed += Time.fixedDeltaTime;
                float distSq = ((Vector2)transform.position - _overtakeTarget).sqrMagnitude;
                // 타겟 도달(0.2f) 또는 2초 경과 시 종료. 순간이동 없이 SmoothDamp로 자연 복귀.
                if (distSq < 0.04f || _overtakeElapsed >= 2f)
                {
                    _overtaking = false;
                }
            }
            else
            {
                _overtakeCheckTimer -= Time.fixedDeltaTime;
                if (_overtakeCheckTimer <= 0f)
                {
                    // 10~20초 주기마다 30% 확률로 발동.
                    if (Random.value < 0.3f)
                    {
                        StartOvertake();
                    }
                    ScheduleNextOvertakeCheck();
                }
            }
        }

        void StartOvertake()
        {
            _overtaking = true;
            _overtakeElapsed = 0f;
            _overtakeTarget = (Vector2)player.position + _lastMoveDir * overtakeDistance;
        }

        // === 실제 이동 적용 ===
        void ApplyMovement()
        {
            Vector2 target;
            float useSmoothTime;

            if (_state == UmiaState.Idle)
            {
                target = _wanderTarget;
                useSmoothTime = wanderSpeed;
            }
            else // Moving
            {
                if (_overtaking)
                {
                    // c-3 발동 중에는 앞 타겟을 계속 갱신(플레이어가 계속 이동 중이므로).
                    _overtakeTarget = (Vector2)player.position + _lastMoveDir * overtakeDistance;
                    target = _overtakeTarget;
                    useSmoothTime = smoothTime;
                }
                else
                {
                    // c-1: 플레이어 뒤 followDistance.
                    target = (Vector2)player.position + (-_lastMoveDir * followDistance);
                    useSmoothTime = _currentSmoothTime;

                    // B케이스 캐치업 판정: followDistance 안으로 들어오면 일반 smoothTime으로 복구.
                    if (_catchingUp)
                    {
                        float distToTargetSq = ((Vector2)transform.position - target).sqrMagnitude;
                        if (distToTargetSq < followDistance * followDistance)
                        {
                            _catchingUp = false;
                            _currentSmoothTime = smoothTime;
                            useSmoothTime = smoothTime;
                        }
                    }
                }
            }

            Vector2 current = transform.position;
            Vector2 next = Vector2.SmoothDamp(current, target, ref _velocity, useSmoothTime,
                                              Mathf.Infinity, Time.fixedDeltaTime);
            // z 유지 — 스프라이트 정렬/카메라 세팅에 영향 주지 않도록.
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        // === 테스트용 컨텍스트 메뉴 ===
        // 인스펙터의 컴포넌트 우클릭 → 아래 항목 선택으로 강제 발동.
        [ContextMenu("Test: Force B Case")]
        void ForceBCase()
        {
            Debug.Log("[UmiaFollow] ContextMenu: Force B Case");
            _frozen = true;
            _velocity = Vector2.zero;
        }

        [ContextMenu("Test: Force Overtake")]
        void ForceOvertake()
        {
            Debug.Log("[UmiaFollow] ContextMenu: Force Overtake");
            _state = UmiaState.Moving;
            StartOvertake();
        }
    }
}
