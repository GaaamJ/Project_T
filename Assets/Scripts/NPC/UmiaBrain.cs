using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectT.NPC
{
    // Umia NPC 행동 브레인.
    // 상태 머신: Wander(배회) → Detect(플레이어 발견) → Approach(접근) → Interact(상호작용 시도) → Wander
    //
    // 물리: Dynamic Rigidbody2D + BoxCollider2D (플레이어와 동일 방식)
    // 벽 감지: Physics2D.Linecast (Wall 레이어 마스크)
    // 웨이포인트: 인스펙터에서 Rect(min/max)로 정의한 맵 경계 안에서 랜덤 좌표 선정
    // 지식: 발견 즉시 JSON으로 persistentDataPath에 저장
    [RequireComponent(typeof(Rigidbody2D))]
    public class UmiaBrain : MonoBehaviour
    {
        // ── 인스펙터 ─────────────────────────────────────────────────
        [Header("Wander")]
        [Tooltip("랜덤 웨이포인트를 뽑을 맵 경계 (월드 좌표 min/max)")]
        [SerializeField] Vector2 wanderMin = new Vector2(-10f, -10f);
        [SerializeField] Vector2 wanderMax = new Vector2(10f, 10f);
        [SerializeField] float wanderSpeed = 2f;
        [SerializeField] float waypointTimeout = 3f;      // 이 시간 안에 도달 못하면 재선정
        [SerializeField] float waypointArrivalDist = 0.3f;

        [Header("Detect")]
        [Tooltip("플레이어를 인식하는 반경")]
        [SerializeField] float detectRadius = 5f;
        [Tooltip("상호작용을 시도하는 반경 (단일 필드)")]
        [SerializeField] float interactRange = 1.2f;
        [Tooltip("Wall 레이어 마스크 — 시야 차단 판정에 사용")]
        [SerializeField] LayerMask wallMask;

        [Header("Approach")]
        [SerializeField] float approachSpeed = 3f;

        [Header("References")]
        [Tooltip("플레이어 Transform — 인스펙터에서 연결")]
        [SerializeField] Transform playerTransform;
        [Tooltip("Umia 자신의 ThreadBuffHolder")]
        [SerializeField] Thread.ThreadBuffHolder buffHolder;

        // ── 내부 상태 ─────────────────────────────────────────────────
        enum State { Wander, Detect, Approach, Interact }
        State currentState = State.Wander;

        Rigidbody2D rb;
        Vector2 currentWaypoint;
        float waypointTimer;

        // 지식 데이터
        [Serializable]
        class KnowledgeEntry
        {
            public string timestamp;
            public string eventType;
            public string detail;
        }

        [Serializable]
        class KnowledgeData
        {
            public List<KnowledgeEntry> entries = new List<KnowledgeEntry>();
        }

        KnowledgeData knowledge = new KnowledgeData();
        string knowledgePath;

        // ── Unity 라이프사이클 ────────────────────────────────────────
        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            knowledgePath = Path.Combine(Application.persistentDataPath, "umia_knowledge.json");
            LoadKnowledge();
            PickNewWaypoint();
        }

        void FixedUpdate()
        {
            switch (currentState)
            {
                case State.Wander:   DoWander();   break;
                case State.Detect:   DoDetect();   break;
                case State.Approach: DoApproach(); break;
                case State.Interact: DoInteract(); break;
            }
        }

        // ── 상태별 로직 ───────────────────────────────────────────────

        void DoWander()
        {
            // 플레이어가 시야에 들어오면 전환
            if (CanSeePlayer())
            {
                TransitionTo(State.Detect);
                return;
            }

            MoveToward(currentWaypoint, wanderSpeed);

            waypointTimer += Time.fixedDeltaTime;
            float dist = Vector2.Distance(rb.position, currentWaypoint);

            // 도착 또는 타임아웃 → 새 웨이포인트
            if (dist < waypointArrivalDist || waypointTimer >= waypointTimeout)
                PickNewWaypoint();
        }

        void DoDetect()
        {
            // 발견 상태에서 플레이어가 사라지면 배회로 복귀
            if (!CanSeePlayer())
            {
                TransitionTo(State.Wander);
                return;
            }

            float dist = Vector2.Distance(rb.position, PlayerPos());
            if (dist <= interactRange)
                TransitionTo(State.Interact);
            else
                TransitionTo(State.Approach);
        }

        void DoApproach()
        {
            if (!CanSeePlayer())
            {
                TransitionTo(State.Wander);
                return;
            }

            float dist = Vector2.Distance(rb.position, PlayerPos());
            if (dist <= interactRange)
            {
                TransitionTo(State.Interact);
                return;
            }

            MoveToward(PlayerPos(), approachSpeed);
        }

        void DoInteract()
        {
            // 멈추고 상호작용 시도
            rb.linearVelocity = Vector2.zero;

            if (buffHolder == null) { TransitionTo(State.Wander); return; }

            // 플레이어 ThreadBuffHolder를 찾아 TryBind 시도
            if (playerTransform != null)
            {
                var playerBuff = playerTransform.GetComponent<Thread.ThreadBuffHolder>();
                if (playerBuff != null && playerBuff.HasBuff)
                {
                    // Coordinate를 찾아 TryBind
                    var coords = FindObjectsByType<Coordinate.Coordinate>(FindObjectsSortMode.None);
                    foreach (var coord in coords)
                    {
                        float coordDist = Vector2.Distance(rb.position, (Vector2)coord.transform.position);
                        if (coordDist <= interactRange)
                        {
                            bool bound = coord.TryBind(playerBuff);
                            if (bound)
                            {
                                RecordKnowledge("CoordinateBound", $"Coord={coord.name} type={coord.BoundType}");
                                // TryBind 성공 후 다음 프레임에 상태 재평가
                                StartCoroutine(ReEvaluateNextFrame());
                                return;
                            }
                        }
                    }
                }
            }

            // 범위 밖이면 다시 Detect로
            if (!CanSeePlayer() || Vector2.Distance(rb.position, PlayerPos()) > interactRange * 1.5f)
                TransitionTo(State.Detect);
        }

        IEnumerator ReEvaluateNextFrame()
        {
            yield return null; // 다음 프레임
            TransitionTo(State.Detect);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        void TransitionTo(State next)
        {
            currentState = next;
            if (next == State.Wander)
                PickNewWaypoint();
        }

        void PickNewWaypoint()
        {
            currentWaypoint = new Vector2(
                UnityEngine.Random.Range(wanderMin.x, wanderMax.x),
                UnityEngine.Random.Range(wanderMin.y, wanderMax.y)
            );
            waypointTimer = 0f;
        }

        void MoveToward(Vector2 target, float speed)
        {
            Vector2 dir = (target - rb.position).normalized;
            rb.linearVelocity = dir * speed;
        }

        Vector2 PlayerPos()
        {
            if (playerTransform == null) return rb.position;
            return playerTransform.position;
        }

        bool CanSeePlayer()
        {
            if (playerTransform == null) return false;
            Vector2 origin = rb.position;
            Vector2 target = PlayerPos();
            float dist = Vector2.Distance(origin, target);
            if (dist > detectRadius) return false;

            // Wall 레이어로 Linecast — 벽에 막히면 false
            RaycastHit2D hit = Physics2D.Linecast(origin, target, wallMask);
            return hit.collider == null;
        }

        // ── 지식 저장 ─────────────────────────────────────────────────

        void RecordKnowledge(string eventType, string detail)
        {
            var entry = new KnowledgeEntry
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                eventType = eventType,
                detail = detail
            };
            knowledge.entries.Add(entry);
            SaveKnowledge();
            Debug.Log($"[UmiaBrain] Knowledge recorded: {eventType} — {detail}");
        }

        void SaveKnowledge()
        {
            try
            {
                string json = JsonUtility.ToJson(knowledge, true);
                File.WriteAllText(knowledgePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UmiaBrain] Failed to save knowledge: {e.Message}");
            }
        }

        void LoadKnowledge()
        {
            try
            {
                if (File.Exists(knowledgePath))
                {
                    string json = File.ReadAllText(knowledgePath);
                    knowledge = JsonUtility.FromJson<KnowledgeData>(json) ?? new KnowledgeData();
                    Debug.Log($"[UmiaBrain] Loaded {knowledge.entries.Count} knowledge entries.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UmiaBrain] Failed to load knowledge: {e.Message}");
                knowledge = new KnowledgeData();
            }
        }

        [ContextMenu("Clear Knowledge")]
        void ClearKnowledge()
        {
            knowledge = new KnowledgeData();
            SaveKnowledge();
            Debug.Log("[UmiaBrain] Knowledge cleared.");
        }

        // ── 에디터 기즈모 ─────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            // 탐지 반경
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRadius);

            // 상호작용 반경
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);

            // 웨이포인트 범위 Rect
            Gizmos.color = Color.green;
            Vector2 center = (wanderMin + wanderMax) * 0.5f;
            Vector2 size = wanderMax - wanderMin;
            Gizmos.DrawWireCube(center, size);

            // 현재 웨이포인트 목표
            if (Application.isPlaying && currentState == State.Wander)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(currentWaypoint, 0.2f);
                Gizmos.DrawLine(transform.position, currentWaypoint);
            }
        }
    }
}
