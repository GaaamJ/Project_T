using System.Collections.Generic;
using UnityEngine;

namespace ProjectT.Thread
{
    public class ThreadSpawnManager : MonoBehaviour
    {
        [SerializeField] Thread threadPrefab;

        ThreadSpawnPoint[] spawnPoints;
        // 각 타입별로 활성 인스턴스가 최대 1개라는 규칙을 강제하기 위해 dict로 관리.
        // (같은 타입이 두 개 스폰되면 안 됨)
        readonly Dictionary<ThreadType, Thread> activeThreads = new();

        void Awake()
        {
            // 씬에 배치된 모든 스폰 지점을 자동으로 수집.
            // 인스펙터 수동 연결을 없애서 스폰 지점 추가/제거 시 매니저 재설정 필요 없게 함.
            spawnPoints = FindObjectsByType<ThreadSpawnPoint>(FindObjectsSortMode.None);
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

        void SpawnThread(ThreadType type)
        {
            if (activeThreads.ContainsKey(type)) return;

            ThreadSpawnPoint point = GetFreeSpawnPoint();
            if (point == null)
            {
                Debug.LogWarning($"[ThreadSpawnManager] No free spawn point for {type}");
                return;
            }

            Thread thread = Instantiate(threadPrefab, point.transform.position, Quaternion.identity);
            thread.Initialize(type, point, this);
            point.Occupy();
            activeThreads[type] = thread;
        }

        // 매번 동일 위치에 나오면 패턴이 예측 가능해지므로 free 목록에서 랜덤 선택.
        ThreadSpawnPoint GetFreeSpawnPoint()
        {
            var free = new List<ThreadSpawnPoint>();
            foreach (var p in spawnPoints)
                if (!p.IsOccupied) free.Add(p);
            if (free.Count == 0) return null;
            return free[Random.Range(0, free.Count)];
        }

        // 스테이지 진입 트리거(도어/텔레포터)가 붙기 전까지 수동 검증용.
        [ContextMenu("Test: Start Stage")]
        void TestStartStage() => StartStage();
    }
}
