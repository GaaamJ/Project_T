using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ProjectT.Thread;
// 네임스페이스와 클래스 이름이 동일(ProjectT.Coordinate.Coordinate)해 CS0118 회피를 위해 alias 사용.
using Coord = ProjectT.Coordinate.Coordinate;

namespace ProjectT.NPC
{
    // Umia AI. 상태: Explore(배회) → SeekYarn(실타래 수집) → SeekCoordinate(좌표 바인드).
    // 맵 지식(스폰 위치·좌표 위치)은 시야 + 벽 Linecast로 발견하며 JSON으로 영구 저장.
    [RequireComponent(typeof(Rigidbody2D))]
    public class UmiaBrain : MonoBehaviour
    {
        // ── 인스펙터 ─────────────────────────────────────────────────────
        [Header("이동")]
        [SerializeField] float speed = 6f;
        [SerializeField] float waypointArrivalDist = 0.4f;
        [SerializeField] float waypointTimeout = 3f;
        [SerializeField] Vector2 wanderMin = new Vector2(-50f, 0f);
        [SerializeField] Vector2 wanderMax = new Vector2(50f, 40f);

        [Header("시야")]
        [SerializeField] float visionRadius = 5f;
        [SerializeField] LayerMask wallMask;

        [Header("상호작용")]
        [SerializeField] float interactRange = 1.2f;

        [Header("참조")]
        [SerializeField] ThreadBuffHolder buffHolder;
        [SerializeField] Player.PlayerInteract playerInteract;

        // ── 상태 머신 ─────────────────────────────────────────────────────
        enum State { Explore, SeekYarn, SeekCoordinate }
        State state = State.Explore;

        Rigidbody2D rb;
        Vector2 currentWaypoint;
        float waypointTimer;

        // ── 맵 지식 ───────────────────────────────────────────────────────
        List<Vector2> knownSpawnPoints = new List<Vector2>();
        List<Vector2> knownCoordinates = new List<Vector2>();
        string savePath;
        const float MergeRadius = 0.5f;

        [Serializable] struct V2S { public float x, y; public V2S(Vector2 v) { x = v.x; y = v.y; } public Vector2 V() => new Vector2(x, y); }
        [Serializable] class SaveData { public List<V2S> sp = new List<V2S>(); public List<V2S> co = new List<V2S>(); }

        // ── 라이프사이클 ──────────────────────────────────────────────────
        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            savePath = Path.Combine(Application.persistentDataPath, "umia_knowledge.json");
            LoadKnowledge();
            PickWanderWaypoint();
        }

        void FixedUpdate()
        {
            ScanVision();

            switch (state)
            {
                case State.Explore:        DoExplore();        break;
                case State.SeekYarn:       DoSeekYarn();       break;
                case State.SeekCoordinate: DoSeekCoordinate(); break;
            }
        }

        // ── 시야 스캔 ─────────────────────────────────────────────────────
        void ScanVision()
        {
            // 풀네임: Yarn Spinner 패키지가 최상위 Yarn 네임스페이스를 점유해 CS0118 회피.
            var yarns = FindObjectsByType<ProjectT.Thread.Yarn>(FindObjectsSortMode.None);
            foreach (var yarn in yarns)
                if (CanSee(yarn.transform.position))
                    AddSpawnPoint(yarn.transform.position);

            var coords = FindObjectsByType<Coord>(FindObjectsSortMode.None);
            foreach (var coord in coords)
                if (CanSee(coord.transform.position))
                    AddCoordinate(coord.transform.position);
        }

        // ── Explore ───────────────────────────────────────────────────────
        void DoExplore()
        {
            MoveToward(currentWaypoint);
            waypointTimer += Time.fixedDeltaTime;

            if (ReachedWaypoint() || TimedOut())
                PickWanderWaypoint();

            if (!buffHolder.HasBuff && knownSpawnPoints.Count > 0)
            { EnterState(State.SeekYarn); return; }

            if (buffHolder.HasBuff && HasKnownInactiveCoord())
            { EnterState(State.SeekCoordinate); return; }
        }

        // ── SeekYarn ──────────────────────────────────────────────────────
        void DoSeekYarn()
        {
            if (buffHolder.HasBuff) { EnterState(State.SeekCoordinate); return; }

            MoveToward(currentWaypoint);
            waypointTimer += Time.fixedDeltaTime;

            var yarn = FindVisibleYarn();
            if (yarn != null)
            {
                float d = Vector2.Distance(rb.position, (Vector2)yarn.transform.position);
                if (d <= interactRange)
                {
                    yarn.TakeHit(buffHolder);
                    return;
                }
                SetWaypoint(yarn.transform.position);
                return;
            }

            if (ReachedWaypoint() || TimedOut())
                if (!PickSpawnWaypoint()) EnterState(State.Explore);
        }

        // ── SeekCoordinate ────────────────────────────────────────────────
        void DoSeekCoordinate()
        {
            if (!buffHolder.HasBuff) { EnterState(State.SeekYarn); return; }

            MoveToward(currentWaypoint);
            waypointTimer += Time.fixedDeltaTime;

            var coord = FindVisibleInactiveCoord();
            if (coord != null)
            {
                float d = Vector2.Distance(rb.position, (Vector2)coord.transform.position);
                if (d <= interactRange)
                {
                    coord.TryBind(buffHolder);
                    return;
                }
                SetWaypoint(coord.transform.position);
                return;
            }

            if (ReachedWaypoint() || TimedOut())
                if (!PickCoordWaypoint()) EnterState(State.Explore);
        }

        // ── 상태 전환 ─────────────────────────────────────────────────────
        void EnterState(State next)
        {
            state = next;
            switch (next)
            {
                case State.Explore:        PickWanderWaypoint(); break;
                case State.SeekYarn:       if (!PickSpawnWaypoint()) state = State.Explore; break;
                case State.SeekCoordinate: if (!PickCoordWaypoint()) state = State.Explore; break;
            }
        }

        // ── 타겟 탐색 ─────────────────────────────────────────────────────
        ProjectT.Thread.Yarn FindVisibleYarn()
        {
            var yarns = FindObjectsByType<ProjectT.Thread.Yarn>(FindObjectsSortMode.None);
            foreach (var y in yarns)
                if (CanSee(y.transform.position)) return y;
            return null;
        }

        Coord FindVisibleInactiveCoord()
        {
            Coord playerTarget = playerInteract != null ? playerInteract.CurrentInteractTarget : null;
            var coords = FindObjectsByType<Coord>(FindObjectsSortMode.None);
            foreach (var c in coords)
            {
                if (c.IsActive || c == playerTarget) continue;
                if (CanSee(c.transform.position)) return c;
            }
            return null;
        }

        bool HasKnownInactiveCoord()
        {
            Coord playerTarget = playerInteract != null ? playerInteract.CurrentInteractTarget : null;
            var coords = FindObjectsByType<Coord>(FindObjectsSortMode.None);
            foreach (var c in coords)
            {
                if (c.IsActive || c == playerTarget) continue;
                foreach (var k in knownCoordinates)
                    if (Vector2.Distance(k, c.transform.position) < MergeRadius) return true;
            }
            return false;
        }

        // ── 웨이포인트 선택 (랜덤 — 항상 최선 아님) ─────────────────────
        bool PickSpawnWaypoint()
        {
            if (knownSpawnPoints.Count == 0) return false;
            SetWaypoint(knownSpawnPoints[UnityEngine.Random.Range(0, knownSpawnPoints.Count)]);
            return true;
        }

        bool PickCoordWaypoint()
        {
            Coord playerTarget = playerInteract != null ? playerInteract.CurrentInteractTarget : null;
            var candidates = new List<Vector2>();
            var coords = FindObjectsByType<Coord>(FindObjectsSortMode.None);
            foreach (var c in coords)
            {
                if (c.IsActive || c == playerTarget) continue;
                foreach (var k in knownCoordinates)
                {
                    if (Vector2.Distance(k, c.transform.position) < MergeRadius)
                    { candidates.Add(c.transform.position); break; }
                }
            }
            if (candidates.Count == 0) return false;
            SetWaypoint(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            return true;
        }

        void PickWanderWaypoint() => SetWaypoint(new Vector2(
            UnityEngine.Random.Range(wanderMin.x, wanderMax.x),
            UnityEngine.Random.Range(wanderMin.y, wanderMax.y)));

        void SetWaypoint(Vector2 pos) { currentWaypoint = pos; waypointTimer = 0f; }

        // ── 이동 / 판정 ───────────────────────────────────────────────────
        void MoveToward(Vector2 target) => rb.linearVelocity = (target - rb.position).normalized * speed;
        bool ReachedWaypoint() => Vector2.Distance(rb.position, currentWaypoint) < waypointArrivalDist;
        bool TimedOut() => waypointTimer >= waypointTimeout;

        // ── 시야 판정 ─────────────────────────────────────────────────────
        bool CanSee(Vector2 pos)
        {
            if (Vector2.Distance(rb.position, pos) > visionRadius) return false;
            return Physics2D.Linecast(rb.position, pos, wallMask).collider == null;
        }

        // ── 지식 관리 ─────────────────────────────────────────────────────
        void AddSpawnPoint(Vector2 pos)
        {
            foreach (var p in knownSpawnPoints)
                if (Vector2.Distance(p, pos) < MergeRadius) return;
            knownSpawnPoints.Add(pos);
            SaveKnowledge();
        }

        void AddCoordinate(Vector2 pos)
        {
            foreach (var p in knownCoordinates)
                if (Vector2.Distance(p, pos) < MergeRadius) return;
            knownCoordinates.Add(pos);
            SaveKnowledge();
        }

        void SaveKnowledge()
        {
            var d = new SaveData();
            foreach (var p in knownSpawnPoints) d.sp.Add(new V2S(p));
            foreach (var p in knownCoordinates) d.co.Add(new V2S(p));
            try { File.WriteAllText(savePath, JsonUtility.ToJson(d, true)); }
            catch (Exception e) { Debug.LogError($"[UmiaBrain] Save failed: {e.Message}"); }
        }

        void LoadKnowledge()
        {
            try
            {
                if (!File.Exists(savePath)) return;
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
                if (d == null) return;
                foreach (var p in d.sp) knownSpawnPoints.Add(p.V());
                foreach (var p in d.co) knownCoordinates.Add(p.V());
                Debug.Log($"[UmiaBrain] Loaded: {knownSpawnPoints.Count} spawn pts, {knownCoordinates.Count} coords.");
            }
            catch (Exception e) { Debug.LogError($"[UmiaBrain] Load failed: {e.Message}"); }
        }

        [ContextMenu("Clear Knowledge")]
        void ClearKnowledge()
        {
            knownSpawnPoints.Clear();
            knownCoordinates.Clear();
            SaveKnowledge();
            Debug.Log("[UmiaBrain] Knowledge cleared.");
        }

        // ── 에디터 기즈모 ─────────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, visionRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube((wanderMin + wanderMax) * 0.5f, wanderMax - wanderMin);
            if (Application.isPlaying)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(currentWaypoint, 0.2f);
                Gizmos.DrawLine(transform.position, currentWaypoint);
            }
        }
    }
}
