using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Pathfinding;
using ProjectT.Thread;
// 네임스페이스와 클래스 이름이 동일(ProjectT.Coordinate.Coordinate)해 CS0118 회피를 위해 alias 사용.
using Coord = ProjectT.Coordinate.Coordinate;
// System.IO.Path 와 Pathfinding.Path 가 둘 다 in-scope 이므로 A* Path 를 명시적 alias로 고정.
using AstarPathType = Pathfinding.Path;

namespace ProjectT.NPC
{
    // Umia AI. 상태: Explore(배회) → SeekYarn(실타래 수집) → SeekCoordinate(좌표 바인드).
    // 맵 지식(스폰 위치·좌표 위치)은 시야 + 벽 Linecast로 발견하며 JSON으로 영구 저장.
    // 이동은 A* Pathfinding Project 기반. 맵 구조(외길·갈림길·막다른 길) 인식 후 DFS 방식으로 탐색.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Seeker))]
    public class UmiaBrain : MonoBehaviour
    {
        // ── 인스펙터 ─────────────────────────────────────────────────────
        [Header("이동")]
        [SerializeField] float speed = 6f;
        [SerializeField] float waypointArrivalDist = 0.4f;
        [SerializeField] float waypointTimeout = 4f;
        // 알려진 스폰포인트를 모두 돌았을 때, 이 시간(초) 탐험 후 재방문 허용.
        [SerializeField] float wanderRetryTimeout = 15f;

        [Header("경로 추종")]
        // 목적지 도달 여부와 무관하게 이 주기로 경로 재계산 — 동적 장애물/타겟 이동 대응.
        [SerializeField] float repathInterval = 0.5f;

        [Header("탐색")]
        // coarse grid 해상도 — visitedCells / PickFrontierWaypoint 폴백에 사용.
        [SerializeField] float cellSize = 2f;
        // Frontier 폴백에서만 사용하는 방향 관성 가중치.
        [SerializeField] float directionInertiaWeight = 0.5f;

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
        Seeker seeker;
        Vector2 currentWaypoint;
        float waypointTimer;
        float wanderTimer;

        // 이번 SeekYarn 사이클에서 이미 방문한 스폰포인트 인덱스 — 반복 방지.
        readonly HashSet<int> triedSpawnIndices = new HashSet<int>();

        // ── 경로 상태 ─────────────────────────────────────────────────────
        AstarPathType currentPath;
        int pathNodeIdx;
        float repathTimer;
        bool pathPending;

        // ── Frontier 상태 ─────────────────────────────────────────────────
        // Dictionary인 이유: HashSet도 되지만 향후 셀별 메타(마지막 방문시각 등) 확장 여지 남김.
        readonly Dictionary<Vector2Int, bool> visitedCells = new Dictionary<Vector2Int, bool>();
        Vector2 lastMoveDir;
        // 이전 탐색 결정 지점 위치 — "온 방향"(cameFrom) 계산에 사용.
        Vector2 prevDecisionPos;
        // 텔레포터 타일맵 캐시 — GetCardinalExits에서 시야 조건 체크에 사용.
        UnityEngine.Tilemaps.Tilemap teleportTilemap;

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
            seeker = GetComponent<Seeker>();
            // Path 심볼이 Pathfinding.Path와 System.IO.Path 두 곳에 존재 — 명시적으로 System.IO.Path 사용.
            savePath = System.IO.Path.Combine(Application.persistentDataPath, "umia_knowledge.json");
            LoadKnowledge();

            foreach (var tm in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
                if (tm.gameObject.name == "Teleport Shadow") { teleportTilemap = tm; break; }

            // GridGraph의 erodeIterations를 강제로 세팅.
            // 이유: Umia 콜라이더가 벽에 붙은 노드를 경로로 잡으면 콜라이더가 벽에 걸려 이동이 정지된다.
            // erode=1로 벽에서 1노드 여유를 확보하면 경로가 벽에서 자연스럽게 떨어져 이동이 매끄러워진다.
            // Runtime에 재스캔이 필요한 이유: 씬 저장된 GridGraph에는 erode=0으로 남아있을 수 있고,
            // 에디터 도메인 리로드 방식으로는 안정적으로 세팅되지 않는 케이스가 있어 Play 시작 시 확실히 반영.
            EnsureGridErosion();
        }

        void EnsureGridErosion()
        {
            const int targetErode = 1;
            var astar = AstarPath.active;
            if (astar == null || astar.data == null) return;
            var graph = astar.data.gridGraph;
            if (graph == null) return;
            if (graph.erodeIterations != targetErode)
            {
                int before = graph.erodeIterations;
                graph.erodeIterations = targetErode;
                astar.Scan();
                Debug.Log($"[UmiaBrain] GridGraph erodeIterations {before} -> {targetErode}, rescanned.");
            }
            PostProcessWalkability(graph);
        }

        // A* 스캔 후 보정: Floor/텔레포터 타일이 없는 노드를 nonwalkable로 마킹.
        // A* 그리드는 직사각형이라 맵 바깥 void 영역도 walkable로 잡힌다 — 이를 타일맵 기준으로 보정.
        void PostProcessWalkability(Pathfinding.GridGraph graph)
        {
            UnityEngine.Tilemaps.Tilemap floorMap = null;
            foreach (var tm in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
                if (tm.gameObject.name == "Floor") { floorMap = tm; break; }
            var teleportMap = teleportTilemap;
            if (floorMap == null) { Debug.LogWarning("[UmiaBrain] Floor tilemap not found — walkability post-process skipped."); return; }

            int fixed_ = 0;
            for (int z = 0; z < graph.depth; z++)
                for (int x = 0; x < graph.width; x++)
                {
                    var node = graph.GetNode(x, z) as Pathfinding.GridNode;
                    if (node == null || !node.Walkable) continue;
                    Vector3 worldPos = (Vector3)node.position;
                    Vector3Int cell = floorMap.WorldToCell(worldPos);
                    bool onFloor = floorMap.HasTile(cell);
                    bool onTeleport = teleportMap != null && teleportMap.HasTile(cell);
                    if (!onFloor && !onTeleport) { node.Walkable = false; fixed_++; }
                }
            Debug.Log($"[UmiaBrain] PostProcessWalkability: {fixed_} void nodes → nonwalkable.");
        }

        void Start()
        {
            // AstarPath Scan 완료 이후에 첫 경로 요청을 시작하기 위해 Start에서 초기화.
            MarkVisited(rb.position);
            prevDecisionPos = rb.position;
            PickExploreWaypoint();
        }

        void FixedUpdate()
        {
            MarkVisited(rb.position);
            ScanVision();

            repathTimer += Time.fixedDeltaTime;
            if (!pathPending && repathTimer >= repathInterval)
            {
                repathTimer = 0f;
                RequestPath(currentWaypoint);
            }

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
            MarkVisionCells();

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
            FollowCurrentPath();
            waypointTimer += Time.fixedDeltaTime;
            wanderTimer += Time.fixedDeltaTime;

            if (ReachedWaypoint() || TimedOut())
                PickExploreWaypoint();

            // 방문 안 한 스폰포인트가 있으면 즉시 수거 시도.
            if (!buffHolder.HasBuff && HasUntriedSpawnPoint())
            { EnterState(State.SeekYarn); return; }

            // 모든 스폰포인트를 돌았지만 yarn이 없었음 — wanderRetryTimeout 탐험 후 재방문.
            if (!buffHolder.HasBuff && knownSpawnPoints.Count > 0 && wanderTimer >= wanderRetryTimeout)
            { wanderTimer = 0f; triedSpawnIndices.Clear(); EnterState(State.SeekYarn); return; }

            if (buffHolder.HasBuff && HasKnownInactiveCoord())
            { EnterState(State.SeekCoordinate); return; }
        }

        // ── SeekYarn ──────────────────────────────────────────────────────
        void DoSeekYarn()
        {
            if (buffHolder.HasBuff)
            {
                triedSpawnIndices.Clear();
                EnterState(State.SeekCoordinate);
                return;
            }

            FollowCurrentPath();
            waypointTimer += Time.fixedDeltaTime;

            // 시야 내 실타래 감지 → 접근 후 수집
            var yarn = FindVisibleYarn();
            if (yarn != null)
            {
                float d = Vector2.Distance(rb.position, (Vector2)yarn.transform.position);
                if (d <= interactRange)
                {
                    yarn.TakeHit(buffHolder);
                    triedSpawnIndices.Clear();
                    return;
                }
                // 매 프레임 SetWaypoint를 호출하면 currentPath가 계속 null로 리셋되어
                // Umia가 경로를 받자마자 다시 멈추는 지지직 현상이 생긴다.
                // 목표 위치가 크게 바뀔 때만 경로 재요청.
                if (Vector2.Distance(currentWaypoint, (Vector2)yarn.transform.position) > 0.5f)
                    SetWaypoint(yarn.transform.position);
                return;
            }

            // 목적지 도착 또는 타임아웃 → 이 스폰포인트엔 yarn 없음, 다른 곳 시도
            if (ReachedWaypoint() || TimedOut())
            {
                // 방문한 것으로 기록 후 다른 스폰포인트 선택
                MarkCurrentWaypointAsTried();
                if (!PickUntriedSpawnWaypoint())
                {
                    // 알려진 스폰포인트를 모두 돌았는데 못 찾음 → 탐험하며 대기.
                    // tried는 유지 — Explore 중 새 스폰포인트 발견 시 자동으로 SeekYarn 재진입.
                    // wanderRetryTimeout 후 tried 초기화 및 재시도.
                    EnterState(State.Explore);
                }
            }
        }

        // ── SeekCoordinate ────────────────────────────────────────────────
        void DoSeekCoordinate()
        {
            if (!buffHolder.HasBuff) { EnterState(State.SeekYarn); return; }

            FollowCurrentPath();
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
                if (Vector2.Distance(currentWaypoint, (Vector2)coord.transform.position) > 0.5f)
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
                case State.Explore:
                    wanderTimer = 0f;
                    PickExploreWaypoint();
                    break;
                case State.SeekYarn:
                    if (!PickUntriedSpawnWaypoint()) state = State.Explore;
                    break;
                case State.SeekCoordinate:
                    if (!PickCoordWaypoint()) state = State.Explore;
                    break;
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

        bool HasUntriedSpawnPoint()
        {
            for (int i = 0; i < knownSpawnPoints.Count; i++)
                if (!triedSpawnIndices.Contains(i)) return true;
            return false;
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

        // ── 웨이포인트 선택 ───────────────────────────────────────────────
        // 방문 안 한 스폰포인트 중 랜덤 선택 (항상 최선 아님).
        bool PickUntriedSpawnWaypoint()
        {
            var untried = new List<int>();
            for (int i = 0; i < knownSpawnPoints.Count; i++)
                if (!triedSpawnIndices.Contains(i)) untried.Add(i);

            if (untried.Count == 0) return false;

            int idx = untried[UnityEngine.Random.Range(0, untried.Count)];
            triedSpawnIndices.Add(idx);
            SetWaypoint(knownSpawnPoints[idx]);
            return true;
        }

        void MarkCurrentWaypointAsTried()
        {
            for (int i = 0; i < knownSpawnPoints.Count; i++)
                if (Vector2.Distance(knownSpawnPoints[i], currentWaypoint) < MergeRadius)
                    triedSpawnIndices.Add(i);
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

        // 웨이포인트 갱신 시 기존 경로 무효화 후 즉시 재요청 — repath 대기 지연 제거.
        void SetWaypoint(Vector2 pos)
        {
            currentWaypoint = pos;
            waypointTimer = 0f;
            currentPath = null;
            RequestPath(pos);
        }

        // ── A* 경로 요청/추종 ─────────────────────────────────────────────
        void RequestPath(Vector2 target)
        {
            if (seeker == null || pathPending) return;
            pathPending = true;
            seeker.StartPath(rb.position, target, OnPathComplete);
        }

        void OnPathComplete(AstarPathType p)
        {
            pathPending = false;
            if (p.error)
            {
                // A*가 도달 불가/그래프 밖 등의 이유로 실패 — 다른 frontier로 즉시 재시도.
                PickFrontierWaypoint();
                return;
            }
            currentPath = p;
            pathNodeIdx = 0;
        }

        void FollowCurrentPath()
        {
            if (currentPath == null || pathNodeIdx >= currentPath.vectorPath.Count)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 next = currentPath.vectorPath[pathNodeIdx];
            Vector2 dir = (next - rb.position).normalized;
            rb.linearVelocity = dir * speed;
            lastMoveDir = dir;

            if (Vector2.Distance(rb.position, next) < waypointArrivalDist)
                pathNodeIdx++;
        }

        // ── Frontier Exploration ──────────────────────────────────────────
        Vector2Int WorldToCell(Vector2 p) => new Vector2Int(
            Mathf.FloorToInt(p.x / cellSize),
            Mathf.FloorToInt(p.y / cellSize)
        );

        Vector2 CellCenter(Vector2Int c) => new Vector2((c.x + 0.5f) * cellSize, (c.y + 0.5f) * cellSize);

        void MarkVisited(Vector2 pos) => visitedCells[WorldToCell(pos)] = true;

        // 시야 반경 내의 모든 셀 중, 실제로 벽에 가리지 않고 보이는 셀을 방문 처리.
        // "이동으로 지나간 셀"뿐 아니라 "시야로 확인한 셀"까지 포함해야 frontier가 벽 뒤로 새지 않음.
        void MarkVisionCells()
        {
            var center = WorldToCell(rb.position);
            int r = Mathf.CeilToInt(visionRadius / cellSize) + 1;
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (visitedCells.ContainsKey(cell)) continue;
                    Vector2 cellCenter = CellCenter(cell);
                    // walkable 체크: void/맵 외곽 셀이 시야에 걸리지 않아도 방문으로 마킹되면
                    // 그 이웃이 frontier가 되어 맵 바깥을 목적지로 잡게 됨.
                    if (CanSee(cellCenter) && IsAstarWalkable(cellCenter))
                        visitedCells[cell] = true;
                }
        }

        // A* 그래프에서 해당 월드 좌표에 해당하는 노드가 walkable한지 검사.
        // NNConstraint.Default는 "가장 가까운 walkable 노드"를 반환하므로 벽/맵 외곽 좌표도
        // 옆의 walkable 노드를 찾아 true를 반환한다 — 체크가 무용지물이 되는 버그.
        // NNConstraint.None으로 실제 최근접 노드(nonwalkable 포함)를 얻고,
        // 거리 임계값으로 그래프 밖 위치(경계 노드가 반환됨)를 추가로 걸러낸다.
        bool IsAstarWalkable(Vector2 worldPos)
        {
            if (AstarPath.active == null) return true;
            var graph = AstarPath.active.data.gridGraph;
            if (graph == null) return true;
            var nearest = AstarPath.active.GetNearest(worldPos, NNConstraint.None);
            if (nearest.node == null) return false;
            // 그래프 밖 위치는 경계 노드를 반환 — 노드 중심까지 거리가 nodeSize*1.5 초과면 범위 밖으로 판정.
            if (Vector2.Distance((Vector2)(Vector3)nearest.position, worldPos) > graph.nodeSize * 1.5f) return false;
            return nearest.node.Walkable;
        }

        // ── 맵 구조 인식 탐색 (1a-1e) ────────────────────────────────────────

        // 현재 위치에서 A* 그래프 기준 walkable 출구 방향 목록.
        // 텔레포터 타일 출구는 CanSee 통과 시에만 포함 (시야 밖 텔레포터는 갈림길 제외).
        List<Vector2> GetCardinalExits(Vector2 pos)
        {
            var result = new List<Vector2>(4);
            float step = AstarPath.active?.data?.gridGraph?.nodeSize ?? 1f;
            foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                Vector2 exitPos = pos + dir * step;
                if (!IsAstarWalkable(exitPos)) continue;
                if (teleportTilemap != null
                    && teleportTilemap.HasTile(teleportTilemap.WorldToCell(exitPos))
                    && !CanSee(exitPos)) continue;
                result.Add(dir);
            }
            return result;
        }

        // 한 방향으로 walkable 경로를 추적해 벽 직전 위치를 반환 (1e: 끝까지 이동).
        // 갈림길 감지는 PickExploreWaypoint의 GetCardinalExits에서 담당하므로 trace는 단순히 벽까지.
        Vector2 TraceInDirection(Vector2 start, Vector2 dir, int maxSteps = 15)
        {
            float step = AstarPath.active?.data?.gridGraph?.nodeSize ?? 1f;
            Vector2 cur = start;
            for (int i = 0; i < maxSteps; i++)
            {
                Vector2 next = cur + dir * step;
                if (!IsAstarWalkable(next)) break;
                cur = next;
            }
            return cur;
        }

        // 해당 방향으로 미방문 셀(frontier)이 존재하는지 확인 — 미탐색 방향 우선 선택에 사용.
        bool HasFrontierInDirection(Vector2 pos, Vector2 dir, int steps = 6)
        {
            for (int i = 1; i <= steps; i++)
                if (!visitedCells.ContainsKey(WorldToCell(pos + dir * (cellSize * i)))) return true;
            return false;
        }

        // 맵 구조 인식 탐색 메인 메서드.
        // GetCardinalExits로 출구를 파악하고 온 방향 제외 후:
        //   0개 → 막다른 길(1c): 돌아감
        //   1개 → 외길(1a): 그쪽으로 계속
        //   2개+ → 갈림길(1b): 미탐색 우선, yarn 방향 우선(1d), 랜덤 선택
        // TraceInDirection으로 다음 갈림길·벽 직전까지 이동(1e).
        bool PickExploreWaypoint()
        {
            // rb.position이 A* 그리드 노드 중심과 어긋나면 IsAstarWalkable이 erode된 인접 노드를
            // 쿼리해 실제 통로를 막혔다고 오판한다. 가장 가까운 노드 중심으로 스냅해 방지.
            Vector2 pos = rb.position;
            if (AstarPath.active != null)
            {
                var snapNode = AstarPath.active.GetNearest(pos, NNConstraint.None);
                if (snapNode.node != null && snapNode.node.Walkable)
                    pos = (Vector2)(Vector3)snapNode.node.position;
            }

            // "온 방향" = 이전 결정 지점 → 현재 위치 방향의 반대.
            Vector2 cameFrom = Vector2.zero;
            if ((pos - prevDecisionPos).sqrMagnitude > 0.25f)
                cameFrom = (prevDecisionPos - pos).normalized;
            prevDecisionPos = pos;

            var allExits = GetCardinalExits(pos);
            if (allExits.Count == 0) return PickFrontierWaypoint();

            // 온 방향 제외 (dot > 0.5 ≒ 거의 온 방향).
            var forwardExits = new List<Vector2>(4);
            foreach (var e in allExits)
                if (cameFrom.sqrMagnitude < 0.01f || Vector2.Dot(e, cameFrom) < 0.5f)
                    forwardExits.Add(e);

            // 1c: 막다른 길 — 온 방향으로 복귀.
            if (forwardExits.Count == 0)
            {
                Vector2 back = allExits[0];
                float bestDot = Vector2.Dot(allExits[0], cameFrom);
                for (int i = 1; i < allExits.Count; i++)
                {
                    float d = Vector2.Dot(allExits[i], cameFrom);
                    if (d > bestDot) { bestDot = d; back = allExits[i]; }
                }
                SetWaypoint(TraceInDirection(pos, back));
                return true;
            }

            // 1d: 시야 내 yarn → 그 방향 우선.
            var yarn = FindVisibleYarn();
            if (yarn != null && forwardExits.Count > 1)
            {
                Vector2 toYarn = ((Vector2)yarn.transform.position - pos).normalized;
                Vector2 best = forwardExits[0];
                float bestDot = Vector2.Dot(forwardExits[0], toYarn);
                for (int i = 1; i < forwardExits.Count; i++)
                {
                    float d = Vector2.Dot(forwardExits[i], toYarn);
                    if (d > bestDot) { bestDot = d; best = forwardExits[i]; }
                }
                if (bestDot > 0.3f) { SetWaypoint(TraceInDirection(pos, best)); return true; }
            }

            // 미탐색 방향 우선, 없으면 전체 forwardExits에서 선택.
            var unexplored = new List<Vector2>(4);
            foreach (var e in forwardExits)
                if (HasFrontierInDirection(pos, e)) unexplored.Add(e);
            var pool = unexplored.Count > 0 ? unexplored : forwardExits;

            // 1a: 단일 출구 → 그쪽으로. 1b: 복수 출구 → 랜덤. 1e: 끝까지 추적해 waypoint 설정.
            SetWaypoint(TraceInDirection(pos, pool[UnityEngine.Random.Range(0, pool.Count)]));
            return true;
        }

        // Frontier = "방문된 셀의 4방향 이웃 중 방문 안 된 셀".
        // 후보를 거리 역수 + 진행방향 관성으로 가중치 두고 룰렛 샘플링 → 자연스러운 확산.
        bool PickFrontierWaypoint()
        {
            var candidates = new List<(Vector2 pos, float weight)>();
            var checkedCells = new HashSet<Vector2Int>();
            // foreach 순회 중 visitedCells를 직접 수정하면 InvalidOperationException 발생.
            // nonwalkable 셀 마킹은 순회 완료 후 일괄 적용.
            var toMarkVisited = new List<Vector2Int>();

            foreach (var kvp in visitedCells)
            {
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var neighbor = kvp.Key + d;
                    if (visitedCells.ContainsKey(neighbor) || !checkedCells.Add(neighbor)) continue;

                    Vector2 worldPos = CellCenter(neighbor);

                    if (!IsAstarWalkable(worldPos))
                    {
                        toMarkVisited.Add(neighbor);
                        continue;
                    }

                    float dist = Vector2.Distance(rb.position, worldPos);
                    float w = 1f / (dist + 1f);

                    if (lastMoveDir.sqrMagnitude > 0.01f)
                    {
                        Vector2 toF = (worldPos - rb.position).normalized;
                        // Dot이 음수면 뒤로 가는 방향 — 관성 보너스는 0으로 클램프.
                        w += Mathf.Max(0f, Vector2.Dot(lastMoveDir, toF)) * directionInertiaWeight;
                    }
                    candidates.Add((worldPos, w));
                }
            }

            foreach (var cell in toMarkVisited)
                visitedCells[cell] = true;

            if (candidates.Count == 0)
            {
                // visitedCells가 비었거나 frontier 소진 — 주변 임의 위치로 fallback.
                SetWaypoint(rb.position + UnityEngine.Random.insideUnitCircle * 5f);
                return false;
            }

            float total = 0f;
            foreach (var c in candidates) total += c.weight;
            float pick = UnityEngine.Random.value * total;
            float cum = 0f;
            foreach (var c in candidates)
            {
                cum += c.weight;
                if (pick <= cum) { SetWaypoint(c.pos); return true; }
            }
            SetWaypoint(candidates[candidates.Count - 1].pos);
            return true;
        }

        // ── 도착/타임아웃 판정 ────────────────────────────────────────────
        // 경로 노드 소진도 도착으로 간주 — 목표가 그래프 밖(예: 좌표 오브젝트 위)일 때 대비.
        bool ReachedWaypoint()
        {
            return (currentPath != null && pathNodeIdx >= currentPath.vectorPath.Count)
                || Vector2.Distance(rb.position, currentWaypoint) < waypointArrivalDist;
        }

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
            if (Application.isPlaying)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(currentWaypoint, 0.2f);
                Gizmos.DrawLine(transform.position, currentWaypoint);
            }
        }
    }
}
