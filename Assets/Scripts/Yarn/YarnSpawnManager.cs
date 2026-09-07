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

        YarnSpawnPoint[] spawnPoints;
        // 각 타입별로 활성 인스턴스가 최대 1개라는 규칙을 강제하기 위해 dict로 관리.
        // (같은 타입이 두 개 스폰되면 안 됨)
        readonly Dictionary<ThreadType, Yarn> activeThreads = new();
        // 플레이어가 실을 수집한 스폰 지점 — 다음 ResetAllAndRespawn 1회에 한해 재사용 금지.
        readonly HashSet<YarnSpawnPoint> oneTimeExcluded = new();

        void Awake()
        {
            // 씬에 배치된 모든 스폰 지점을 자동으로 수집.
            // 인스펙터 수동 연결을 없애서 스폰 지점 추가/제거 시 매니저 재설정 필요 없게 함.
            spawnPoints = FindObjectsByType<YarnSpawnPoint>(FindObjectsSortMode.None);

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
            SpawnThread(ThreadType.Red);
            SpawnThread(ThreadType.Blue);
            SpawnThread(ThreadType.Gold);
        }

        // 플레이어가 실을 수집했을 때 Yarn.TakeHit에서 호출.
        // 수집된 스폰 지점은 다음 ResetAllAndRespawn 1회에서 제외된다.
        public void NotifyDestroyed(ThreadType type, YarnSpawnPoint point)
        {
            activeThreads.Remove(type);
            if (point != null) oneTimeExcluded.Add(point);
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
            if (activeThreads.ContainsKey(type)) return;
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
            activeThreads[type] = yarn;

            // 스폰 완료 이벤트. 구독자(예: UmiaCooperationDialogue)가 방 문맥 대사 트리거에 사용.
            // 예외로 다른 스폰이 막히지 않도록 try/catch로 격리.
            try { OnYarnSpawned?.Invoke(point); }
            catch (Exception e) { Debug.LogError($"[YarnSpawnManager] OnYarnSpawned handler threw: {e}"); }
        }

        // 버프 소모/만료 시 씬에 남아있는 실을 전부 제거하고 3색을 새 위치에 다시 생성.
        // OnBuffReplaced(교체)는 이 경로를 타지 않는다 — 홀더가 버프를 계속 보유 중이므로.
        void ResetAllAndRespawn(ThreadType _)
        {
            foreach (var yarn in activeThreads.Values)
                if (yarn != null) Destroy(yarn.gameObject);

            foreach (var point in spawnPoints)
                point.Free();

            activeThreads.Clear();

            SpawnThread(ThreadType.Red);
            SpawnThread(ThreadType.Blue);
            SpawnThread(ThreadType.Gold);
            oneTimeExcluded.Clear();
        }

        // 매번 동일 위치에 나오면 패턴이 예측 가능해지므로 free 목록에서 랜덤 선택.
        YarnSpawnPoint GetFreeSpawnPoint()
        {
            var free = new List<YarnSpawnPoint>();
            foreach (var p in spawnPoints)
                if (!p.IsOccupied && !oneTimeExcluded.Contains(p)) free.Add(p);
            if (free.Count == 0) return null;
            // System.Random과 모호성 회피를 위해 완전한 이름 사용.
            return free[UnityEngine.Random.Range(0, free.Count)];
        }

        // 디버거/HUD가 각 타입의 활성 여부를 조회하기 위한 read-only 프로브 —
        // 내부 dict를 직접 노출하지 않고 boolean만 반환해 캡슐화 유지.
        public bool IsTypeActive(ThreadType type) => activeThreads.ContainsKey(type);
    }
}
