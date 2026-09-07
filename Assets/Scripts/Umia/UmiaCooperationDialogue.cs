using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;
using ProjectT.Thread;

namespace ProjectT.Umia
{
    // 실 스폰 시 우미아가 상황에 맞는 대사를 내뱉는 컴포넌트.
    //
    // 케이스 분기 (기획서 기준):
    // - a: 스폰된 방을 우미아가 방문한 적이 있다 → 그 방 이름의 노드 실행 (예: SandBox_DownLeftRoom)
    // - b: 스폰된 방을 방문한 적이 없다 → noSenseNodes 중 랜덤 노드 실행
    // - c: YarnSpawnPoint가 있는 방문한 방이 하나도 없다 → 대사 없음
    //
    // 설계 결정:
    // - Umia GameObject에 부착 (사용자 지정). 대사 로직의 주체는 우미아이므로
    //   YarnSpawnManager 안이 아니라 Umia 컴포넌트로 분리해 책임 격리.
    // - YarnSpawnManager에서 이벤트 방식으로 연결. 매니저 → 우미아 참조를 직접 두지 않아
    //   양방향 결합 회피.
    // - c 케이스 판정용 "YarnSpawnPoint가 있는 방 집합"은 Awake 시 1회 계산해 캐시.
    //   씬 로드 이후 SpawnPoint 배치가 변하지 않는다는 전제. 변한다면 이 캐시를 초기화해야 함.
    // - 다중 스폰(StartStage 3연속) 시 IsDialogueRunning으로 두 번째부터 무시 →
    //   먼저 스폰된 Red가 이겨서 그 방의 노드만 실행됨 (Q2 답변대로).
    public class UmiaCooperationDialogue : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] DialogueRunner dialogueRunner;
        [SerializeField] YarnSpawnManager spawnManager;

        // b 케이스: 방문하지 않은 방에 실이 스폰됐을 때 랜덤 재생할 후보 노드.
        // 인스펙터에서 직접 지정 (자동 탐색은 오탐 위험).
        [Header("Case B — No sense nodes")]
        [SerializeField] string[] noSenseNodes;

        // "YarnSpawnPoint가 있는 방"의 저장 ID 집합. SaveManager의 visitedSpaces와
        // 형식을 맞추기 위해 "{sceneName}_{roomName}" 형태로 저장.
        // c 케이스 판정: 이 집합과 visited의 교집합이 비어있으면 대사 없음.
        HashSet<string> _roomsWithSpawnPoints;

        void Awake()
        {
            // 씬에 배치된 모든 SpawnPoint의 부모(방)를 스캔.
            // 배치가 런타임에 바뀌지 않는다는 전제 하에 Awake 1회로 충분.
            _roomsWithSpawnPoints = new HashSet<string>();
            string sceneName = SceneManager.GetActiveScene().name;
            var allPoints = FindObjectsByType<YarnSpawnPoint>(FindObjectsSortMode.None);
            foreach (var p in allPoints)
            {
                if (p == null || p.transform.parent == null) continue;
                string roomName = p.transform.parent.name;
                _roomsWithSpawnPoints.Add($"{sceneName}_{roomName}");
            }

            if (spawnManager != null)
                spawnManager.OnYarnSpawned += HandleYarnSpawned;
        }

        void OnDestroy()
        {
            // 매니저는 씬에 남아있을 수 있으므로 명시적 해제 (이벤트 누수 방지).
            if (spawnManager != null)
                spawnManager.OnYarnSpawned -= HandleYarnSpawned;
        }

        // 실이 스폰될 때마다 호출. StartStage의 3연속 스폰 중 첫 번째만 통과되고
        // 나머지는 IsDialogueRunning 체크에서 걸러진다.
        void HandleYarnSpawned(YarnSpawnPoint point)
        {
            if (point == null || point.transform.parent == null) return;
            TryPlayDialogueForRoom(point.transform.parent.name);
        }

        // 스테이지 상태 등 외부 시스템이 임의 시점에 협력 대사 판정을 트리거하고 싶을 때 사용.
        // 현재 호출자는 없으나, 기획서 지침에 따라 향후 연동용으로 노출 (a/b/c 로직 재사용).
        // 이 경로는 특정 방이 아닌 "지금 상황"에 대해 판정하므로:
        //   - visited ∩ roomsWithSpawnPoints이 비면 c
        //   - 아니면 그 교집합에서 랜덤으로 하나 골라 방 노드 재생 (a)
        //   - 방문 안 한 방 중 스폰 가능한 방이 있어도 여기서는 b를 강제할 방법이 없으므로 a만 처리
        public void TriggerCooperationCheck()
        {
            if (dialogueRunner == null || dialogueRunner.IsDialogueRunning) return;

            var visitedWithSpawn = new List<string>();
            foreach (var roomId in _roomsWithSpawnPoints)
            {
                if (SaveManager.Instance.IsVisited(roomId))
                    visitedWithSpawn.Add(roomId);
            }

            // c 케이스: 방문한 방 중 스폰 지점을 가진 곳이 없음 → 대사 없음.
            if (visitedWithSpawn.Count == 0) return;

            // a 케이스와 동일 노드 규칙: "{sceneName}_{roomName}"이 그대로 Yarn 노드 이름.
            string chosen = visitedWithSpawn[Random.Range(0, visitedWithSpawn.Count)];
            if (dialogueRunner.YarnProject != null && dialogueRunner.YarnProject.NodeNames != null)
            {
                // 노드가 실제로 존재할 때만 실행 — 없으면 무시하고 조용히 넘어간다.
                foreach (var n in dialogueRunner.YarnProject.NodeNames)
                {
                    if (n == chosen)
                    {
                        dialogueRunner.StartDialogue(chosen);
                        return;
                    }
                }
            }
        }

        // 실이 스폰된 방에 대한 실제 대사 판정. HandleYarnSpawned에서 호출.
        void TryPlayDialogueForRoom(string roomName)
        {
            if (dialogueRunner == null) return;
            // 이미 대사 중이면 그 상황을 존중해 건너뛴다 (Q2: 3연속 스폰 → 첫 번째만 승리).
            if (dialogueRunner.IsDialogueRunning) return;

            string sceneName = SceneManager.GetActiveScene().name;
            string roomId = $"{sceneName}_{roomName}";

            // c 케이스: 방문한 방 중 스폰 지점을 가진 곳이 하나도 없음 → 완전 침묵.
            // 이 케이스는 게임 초반 CenterRoom만 방문한 상태에 해당.
            bool anyVisitedRoomHasSpawn = false;
            foreach (var id in _roomsWithSpawnPoints)
            {
                if (SaveManager.Instance.IsVisited(id))
                {
                    anyVisitedRoomHasSpawn = true;
                    break;
                }
            }
            if (!anyVisitedRoomHasSpawn) return;

            // a 케이스: 스폰된 방을 방문한 적 있음 → 방 이름 노드 실행.
            if (SaveManager.Instance.IsVisited(roomId))
            {
                PlayNodeIfExists(roomId);
                return;
            }

            // b 케이스: 스폰된 방을 방문한 적 없음 → 랜덤 noSense 노드.
            if (noSenseNodes != null && noSenseNodes.Length > 0)
            {
                string node = noSenseNodes[Random.Range(0, noSenseNodes.Length)];
                PlayNodeIfExists(node);
            }
        }

        // Yarn Project에 등록된 노드에서만 재생. 오타/누락 노드로 인한 런타임 예외 회피.
        void PlayNodeIfExists(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return;
            if (dialogueRunner.YarnProject == null) return;
            var names = dialogueRunner.YarnProject.NodeNames;
            if (names == null) return;
            foreach (var n in names)
            {
                if (n == nodeName)
                {
                    dialogueRunner.StartDialogue(nodeName);
                    return;
                }
            }
            // 노드 없음: 로그만 남기고 진행. 기획 상 자리잡기용이므로 빠졌으면 개발자가 알아야 한다.
            Debug.LogWarning($"[UmiaCooperationDialogue] Yarn node '{nodeName}' not found in project.");
        }
    }
}
