using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectT.Thread
{
    public class YarnSpawnManager : MonoBehaviour
    {
        // 실이 새로 스폰될 때마다 발화. UmiaCooperationDialogue 등 외부 시스템이
        // "어느 방(=SpawnPoint의 부모)에서 스폰됐는지"를 알고 반응하기 위해 필요.
        // 스폰 대상(SpawnPoint) 자체를 넘겨 소비자가 부모 방 이름을 직접 조회하도록 한다 —
        // ThreadType까지 넘길 수도 있으나 현재 소비자는 방 정보만 필요.
        public event Action<YarnSpawnPoint> OnYarnSpawned;

        [SerializeField] Yarn yarnPrefab;
        // 버프 해제(소모/만료/교체) 이벤트를 구독해 해당 타입의 실을 즉시 재스폰하기 위해 필요.
        // Player·Umia 등 여러 캐릭터가 각자 ThreadBuffHolder를 보유하므로 배열로 관리한다.
        // 인스펙터에서 씬의 홀더들을 명시적으로 연결한다 — 자동 탐색보다 안전.
        [SerializeField] ThreadBuffHolder[] buffHolders;

        YarnSpawnPoint[] _spawnPoints;
        // 각 타입별로 활성 인스턴스가 최대 1개라는 규칙을 강제하기 위해 dict로 관리.
        // (같은 타입이 두 개 스폰되면 안 됨)
        readonly Dictionary<ThreadType, Yarn> _activeThreads = new();
        // 플레이어가 실을 수집한 스폰 지점 — 다음 ResetAllAndRespawn 1회에 한해 재사용 금지.
        readonly HashSet<YarnSpawnPoint> _oneTimeExcluded = new();
        // 가장 최근에 실을 수집한 방(SpawnPoint의 부모). 해당 방에는 실을 스폰하지 않는다.
        // 새 실을 수집하면 갱신되며, 이전 방 제한은 자동 해제된다.
        Transform _lastAcquiredRoom;
        // 스테이지 진행 중에만 스폰을 허용하기 위한 가드.
        // 클리어 시점에 마지막 좌표 바인딩이 holder.Consume()을 호출하면 OnBuffConsumed가 발화되어
        // ResetAllAndRespawn이 트리거되고, 그 안의 SpawnThread가 OnYarnSpawned를 발화시켜
        // 이미 클리어된 상태인데도 힌트 대사가 재생되는 버그가 있었다.
        // ClearStage()가 ResetStage()로 이 플래그를 false로 내려서, 이후 잔여 이벤트가
        // ResetAllAndRespawn/SpawnThread에 도달해도 진입 직후 return시켜 스폰과 이벤트 발화를 차단한다.
        bool _isActive;

        void Awake()
        {
            // 씬에 배치된 모든 스폰 지점을 자동으로 수집.
            // 인스펙터 수동 연결을 없애서 스폰 지점 추가/제거 시 매니저 재설정 필요 없게 함.
            _spawnPoints = FindObjectsByType<YarnSpawnPoint>(FindObjectsSortMode.None);

            // 소모/만료 시 전체 리셋 후 3색 재스폰, 교체 시 교체된 타입만 재스폰.
            if (buffHolders != null)
            {
                foreach (var holder in buffHolders)
                {
                    if (holder == null) continue;
                    holder.OnBuffConsumed += ResetAllAndRespawn;
                    holder.OnBuffExpired  += ResetAllAndRespawn;
                    holder.OnBuffReplaced += SpawnThread;
                }
            }
        }

        void OnDestroy()
        {
            // 매니저가 파괴되어도 buffHolder들은 씬에 살아있을 수 있으므로 이벤트 누수/댕글링 콜백 방지.
            if (buffHolders != null)
            {
                foreach (var holder in buffHolders)
                {
                    if (holder == null) continue;
                    holder.OnBuffConsumed -= ResetAllAndRespawn;
                    holder.OnBuffExpired  -= ResetAllAndRespawn;
                    holder.OnBuffReplaced -= SpawnThread;
                }
            }
        }

        // 스테이지 진입 시점에 3색을 한 번씩 스폰. 시작 트리거는 외부에서 호출한다.
        public void StartStage()
        {
            // 스폰 가드 활성화 — SpawnThread/ResetAllAndRespawn이 실제로 동작할 수 있도록 게이트를 연다.
            _isActive = true;
            SpawnThread(ThreadType.Red);
            SpawnThread(ThreadType.Blue);
            SpawnThread(ThreadType.Gold);
        }

        // StageManager가 리트라이/클리어 시 호출. 씬의 모든 실을 제거하고 스폰 상태를 완전히 초기화한다.
        // ResetAllAndRespawn과 다른 점: 이 메서드는 StartStage()를 호출하지 않는다.
        // 리트라이 시나리오에서는 플레이어가 시작 구역을 다시 이탈해야 스테이지가 재개되므로
        // 스폰은 그 시점(StartStage 호출)에 트리거되어야 하기 때문.
        // 클리어 경로에서도 이 메서드를 호출해 _isActive를 내려 잔여 이벤트로 인한 재스폰을 차단한다.
        public void ResetStage()
        {
            // 스폰 가드를 먼저 내려서, 이 메서드 실행 중이나 직후에 도달할 수 있는
            // OnBuffConsumed/OnBuffExpired → ResetAllAndRespawn 호출을 즉시 무력화한다.
            _isActive = false;
            ClearActiveThreads();
            _oneTimeExcluded.Clear();
            // 리셋 후 첫 스폰 시 방 제한이 남아있으면 안 되므로 마지막 획득 방도 초기화.
            _lastAcquiredRoom = null;
        }

        // 플레이어가 실을 수집했을 때 Yarn.TakeHit에서 호출.
        // 수집된 스폰 지점은 다음 ResetAllAndRespawn 1회에서 제외된다.
        public void NotifyDestroyed(ThreadType type, YarnSpawnPoint point)
        {
            _activeThreads.Remove(type);
            if (point != null) _oneTimeExcluded.Add(point);
            _lastAcquiredRoom = point?.transform.parent;
            SpawnThread(type);
        }

        // 여러 홀더 중 하나라도 해당 타입 버프를 보유 중인지 확인.
        // 스폰 보류 판단은 "모든 홀더 합쳐서 아무도 안 들고 있을 때만 스폰"이 되어야 하므로 OR 검사.
        bool AnyHolderHasBuff(ThreadType type)
        {
            if (buffHolders == null) return false;
            foreach (var holder in buffHolders)
            {
                if (holder == null) continue;
                if (holder.HasBuff && holder.CurrentType == type) return true;
            }
            return false;
        }

        void SpawnThread(ThreadType type)
        {
            // 스테이지 진행 중이 아닐 때는 어떤 경로로 진입해도 스폰을 막는다.
            // OnBuffReplaced(교체) 이벤트도 이 메서드에 직접 바인딩되어 있어, 클리어 이후
            // 잔여 이벤트로 인한 스폰과 OnYarnSpawned 발화(→ 힌트 대사)를 차단하려면 여기 가드가 필요하다.
            if (!_isActive) return;
            if (_activeThreads.ContainsKey(type)) return;
            // 어느 홀더든 해당 타입 버프를 보유 중이면 그 실은 존재하지 않는 상태가 정상 —
            // 그 홀더의 버프 해제(소모/만료/교체) 이벤트 시점에 다시 스폰되므로 여기서는 건너뛴다.
            if (AnyHolderHasBuff(type)) return;

            YarnSpawnPoint point = GetFreeSpawnPoint();
            if (point == null)
            {
                Debug.LogWarning($"[YarnSpawnManager] No free spawn point for {type}");
                return;
            }

            Yarn yarn = Instantiate(yarnPrefab, point.transform.position, Quaternion.identity);
            // 스폰 지점의 자식으로 두어 하이라키에서 소속 관계를 시각적으로 파악하기 쉽게 하고,
            // 스폰 지점이 이동/파괴될 경우 소속 실도 함께 따라가도록 한다.
            yarn.transform.SetParent(point.transform);
            yarn.Initialize(type, point, this);
            point.Occupy();
            _activeThreads[type] = yarn;

            // 스폰 완료 이벤트. 구독자(예: UmiaCooperationDialogue)가 방 문맥 대사 트리거에 사용.
            // 예외로 다른 스폰이 막히지 않도록 try/catch로 격리.
            try { OnYarnSpawned?.Invoke(point); }
            catch (Exception e) { Debug.LogError($"[YarnSpawnManager] OnYarnSpawned handler threw: {e}"); }
        }

        // 버프 소모/만료 시 씬에 남아있는 실을 전부 제거하고 3색을 새 위치에 다시 생성.
        // OnBuffReplaced(교체)는 이 경로를 타지 않는다 — 홀더가 버프를 계속 보유 중이므로.
        void ResetAllAndRespawn(ThreadType _)
        {
            // 스테이지가 이미 클리어/리셋된 상태에서 지연 도달한 소모/만료 이벤트를 흡수.
            // 특히 마지막 좌표 바인딩 → holder.Consume() → OnBuffConsumed 경로가
            // ClearStage()에서 재스폰과 힌트 대사(OnYarnSpawned)를 트리거하지 않도록 차단한다.
            if (!_isActive) return;

            ClearActiveThreads();
            SpawnThread(ThreadType.Red);
            SpawnThread(ThreadType.Blue);
            SpawnThread(ThreadType.Gold);
            _oneTimeExcluded.Clear();
        }

        // ResetStage와 ResetAllAndRespawn에서 공유하는 "모든 실 파괴 + 스폰 지점 해제 + dict 초기화" 흐름.
        void ClearActiveThreads()
        {
            foreach (var yarn in _activeThreads.Values)
                if (yarn != null) Destroy(yarn.gameObject);

            foreach (var point in _spawnPoints)
                point.Free();

            _activeThreads.Clear();
        }

        // 매번 동일 위치에 나오면 패턴이 예측 가능해지므로 free 목록에서 랜덤 선택.
        YarnSpawnPoint GetFreeSpawnPoint()
        {
            var free = new List<YarnSpawnPoint>();
            foreach (var p in _spawnPoints)
                if (!p.IsOccupied && !_oneTimeExcluded.Contains(p)
                    && p.transform.parent != _lastAcquiredRoom) free.Add(p);
            if (free.Count == 0) return null;
            // System.Random과 모호성 회피를 위해 완전한 이름 사용.
            return free[UnityEngine.Random.Range(0, free.Count)];
        }

        // 디버거/HUD가 각 타입의 활성 여부를 조회하기 위한 read-only 프로브 —
        // 내부 dict를 직접 노출하지 않고 boolean만 반환해 캡슐화 유지.
        public bool IsTypeActive(ThreadType type) => _activeThreads.ContainsKey(type);
    }
}
