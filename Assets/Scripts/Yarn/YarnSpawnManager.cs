using System.Collections.Generic;
using UnityEngine;

namespace ProjectT.Thread
{
    public class YarnSpawnManager : MonoBehaviour
    {
        [SerializeField] Yarn yarnPrefab;
        // 버프 해제(소모/만료/교체) 이벤트를 구독해 해당 타입의 실을 즉시 재스폰하기 위해 필요.
        // Player·Umia 등 여러 캐릭터가 각자 ThreadBuffHolder를 보유하므로 배열로 관리한다.
        // 인스펙터에서 씬의 홀더들을 명시적으로 연결한다 — 자동 탐색보다 안전.
        [SerializeField] ThreadBuffHolder[] buffHolders;

        YarnSpawnPoint[] spawnPoints;
        // 각 타입별로 활성 인스턴스가 최대 1개라는 규칙을 강제하기 위해 dict로 관리.
        // (같은 타입이 두 개 스폰되면 안 됨)
        readonly Dictionary<ThreadType, Yarn> activeThreads = new();

        void Awake()
        {
            // 씬에 배치된 모든 스폰 지점을 자동으로 수집.
            // 인스펙터 수동 연결을 없애서 스폰 지점 추가/제거 시 매니저 재설정 필요 없게 함.
            spawnPoints = FindObjectsByType<YarnSpawnPoint>(FindObjectsSortMode.None);

            // 버프가 해제되는 세 가지 경로(소모/만료/교체) 모두 동일하게 "그 타입을 다시 스폰"으로 이어짐.
            // 매니저가 매 프레임 상태를 폴링하지 않고 이벤트 기반으로 리스폰하기 위해 구독.
            // 여러 홀더 중 어느 하나에서 버프가 해제되어도 같은 SpawnThread 콜백이 호출됨 —
            // SpawnThread 내부 가드에서 "다른 홀더가 그 타입을 들고 있으면 스폰 보류" 처리.
            if (buffHolders != null)
            {
                foreach (var holder in buffHolders)
                {
                    if (holder == null) continue;
                    holder.OnBuffConsumed += SpawnThread;
                    holder.OnBuffExpired  += SpawnThread;
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
                    holder.OnBuffConsumed -= SpawnThread;
                    holder.OnBuffExpired  -= SpawnThread;
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

        // Thread가 파괴될 때 콜백으로 호출됨.
        // 매니저가 매 프레임 감시하지 않고, 이벤트 기반으로 리스폰 트리거.
        public void NotifyDestroyed(ThreadType type)
        {
            activeThreads.Remove(type);
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
        }

        // 매번 동일 위치에 나오면 패턴이 예측 가능해지므로 free 목록에서 랜덤 선택.
        YarnSpawnPoint GetFreeSpawnPoint()
        {
            var free = new List<YarnSpawnPoint>();
            foreach (var p in spawnPoints)
                if (!p.IsOccupied) free.Add(p);
            if (free.Count == 0) return null;
            return free[Random.Range(0, free.Count)];
        }

        // 디버거/HUD가 각 타입의 활성 여부를 조회하기 위한 read-only 프로브 —
        // 내부 dict를 직접 노출하지 않고 boolean만 반환해 캡슐화 유지.
        public bool IsTypeActive(ThreadType type) => activeThreads.ContainsKey(type);
    }
}
