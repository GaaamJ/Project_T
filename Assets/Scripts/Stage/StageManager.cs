using System;
using UnityEngine;
using ProjectT.Thread;
using ProjectT.Player;

namespace ProjectT.Stage
{
    // namespace ProjectT.Coordinate와 class Coordinate 이름이 같아 CS0118을 유발하므로
    // 클래스만 별칭으로 가져와 충돌을 회피한다. global:: 접두어로 최상위부터 명확히 지정.
    using Coordinate = global::ProjectT.Coordinate.Coordinate;

    // 스테이지 진행 상태를 상태 머신으로 명시하기 위해 enum으로 분리.
    // 상태별로 허용되는 전이만 처리해서 잘못된 순서로 API가 호출되는 것을 방지한다.
    public enum StageState
    {
        Waiting,    // 씬 시작 직후. 플레이어가 시작 구역에 있으며 실은 스폰되지 않음.
        InProgress, // 시작 구역을 벗어난 순간부터 클리어까지. 실 스폰/재스폰 활성화.
        Clear       // 좌표 6개 전부 활성화됐을 때. 이후 문 개방 등 후속 로직 훅.
    }

    // 스테이지의 전체 흐름(대기 → 진행 → 클리어/리셋)을 총괄하는 단일 매니저.
    // 씬에 1개만 배치한다. 싱글톤 대신 인스펙터 참조 방식을 쓰는 이유는
    // 참조가 필요한 대상(StartZoneTrigger)이 소수라서 명시적 연결이 더 명확하기 때문.
    public class StageManager : MonoBehaviour
    {
        // 스폰 초기화·리셋 흐름을 위임할 대상.
        [SerializeField] YarnSpawnManager yarnSpawnManager;
        // 리트라이 시 강제 초기화가 필요한 홀더들. Player + Umia 등 스테이지에 참여하는 모든 캐릭터.
        [SerializeField] ThreadBuffHolder[] buffHolders;
        // 리트라이 시 플레이어를 워프시킬 목적지. StartZone 중앙을 가리키게 세팅.
        [SerializeField] Transform startZoneCenter;
        // 체력 시스템. 실패/리셋 시 최대 체력으로 복구하기 위해 참조.
        // 실패 트리거 자체는 HealthSystem이 자신의 stageManager 필드로 직접 호출하므로, 여기서는 리셋 호출용으로만 사용.
        // 씬에 없어도 스테이지 흐름이 죽지 않도록 옵션 참조 — 초기 씬 세팅 편의성.
        [SerializeField] HealthSystem healthSystem;

        // 씬에 배치된 모든 Coordinate. Awake에서 자동 수집 —
        // 좌표는 씬 편집 시 자주 추가/제거되므로 인스펙터 수동 연결은 비효율적.
        Coordinate[] _coordinates;

        public StageState State { get; private set; } = StageState.Waiting;

        // 상태 전이 이벤트. UI/문 개방/카메라 등 후속 시스템이 구독해 클리어 순간을 반응할 수 있게 노출.
        public event Action OnStageCleared;
        // 실패(리셋) 발생 시 발화. 인자는 리셋 직전 활성화된 좌표 수 — 대사 트리거가 "얼마나 근접했나" 분기에 사용한다.
        public event Action<int> OnStageFailed;

        void Awake()
        {
            _coordinates = FindObjectsByType<Coordinate>(FindObjectsSortMode.None);

            // 좌표 활성화 감지 — 6개 모두 활성화되면 자동 클리어.
            // OnActivated는 비활성 → 활성 전환 시에만 발화하므로 중복 카운트 걱정 없음.
            foreach (var c in _coordinates)
                c.OnActivated += HandleCoordinateActivated;
        }

        void OnDestroy()
        {
            if (_coordinates != null)
            {
                foreach (var c in _coordinates)
                    if (c != null) c.OnActivated -= HandleCoordinateActivated;
            }
        }

        // StartZoneTrigger가 플레이어 이탈을 감지하면 호출.
        // Waiting 상태에서만 InProgress로 전환한다 — 이미 진행/클리어된 상태에서 재입장/이탈 반복 시 중복 스폰 방지.
        public void OnPlayerLeftStartZone()
        {
            if (State != StageState.Waiting) return;

            State = StageState.InProgress;
            yarnSpawnManager.StartStage();
            Debug.Log("[StageManager] 스테이지 시작 → InProgress");
        }

        // 좌표 6개 전부 활성화 시 자동 호출. 외부에서도 강제 클리어용으로 호출 가능.
        public void ClearStage()
        {
            if (State == StageState.Clear) return;

            State = StageState.Clear;
            // 마지막 좌표 바인딩 흐름(Coordinate.TryBind → holder.Consume → OnBuffConsumed → ResetAllAndRespawn → SpawnThread → OnYarnSpawned)이
            // 실제로는 HandleCoordinateActivated → ClearStage보다 먼저 실행되어, 클리어 이후에도 힌트 대사가 재생되는 버그가 있었다.
            // ClearStage에서 즉시 ResetStage를 호출해 스폰 게이트(isActive)를 내리고 씬의 실타래를 정리하면,
            // 지연 도달하는 잔여 버프 이벤트가 스폰과 이벤트 발화를 트리거하지 못한다.
            if (yarnSpawnManager != null)
                yarnSpawnManager.ResetStage();
            Debug.Log("[StageManager] 스테이지 클리어");
            OnStageCleared?.Invoke();
        }

        // 사망/실패 트리거. 실질적으로는 InProgress일 때만 리셋을 수행한다 —
        // Waiting 상태에서 호출되면 아무것도 안 함(초기 상태와 동일), Clear 상태에서 호출되면 무시(진행 종료 후 되돌리지 않음).
        public void TriggerStageFail()
        {
            if (State != StageState.InProgress) return;
            PerformReset();
        }

        // 실제 리셋 로직. TriggerStageFail과 디버그 메뉴에서 공유.
        void PerformReset()
        {
            // 실패 직전 활성 좌표 수를 캡처 — 이후 c.Reset()으로 상태가 지워지기 전에 계산해야 정확.
            // 대사 트리거가 이 값을 "몇 개까지 갔었는지" 분기에 사용한다.
            int coordsActive = 0;
            if (_coordinates != null)
            {
                foreach (var c in _coordinates)
                    if (c != null && c.IsActive) coordsActive++;
            }
            OnStageFailed?.Invoke(coordsActive);

            // 1) 실타래 전체 제거 + 스폰 상태 초기화 (StartStage는 호출하지 않음 — Waiting에서 시작 트리거로 다시 시작해야 함)
            if (yarnSpawnManager != null)
                yarnSpawnManager.ResetStage();

            // 2) 버프 강제 초기화. 이벤트를 발행하지 않는 ForceReset을 써야
            //    YarnSpawnManager의 ResetAllAndRespawn이 트리거되지 않아 중복 스폰이 방지됨.
            if (buffHolders != null)
            {
                foreach (var h in buffHolders)
                    if (h != null) h.ForceReset();
            }

            // 3) 좌표 비활성화. 스프라이트도 함께 초기화됨.
            foreach (var c in _coordinates)
                if (c != null) c.Reset();

            // 4) 플레이어를 시작 구역 중앙으로 워프.
            //    태그 기반으로 찾는 이유: StageManager가 특정 Player 컴포넌트에 의존하지 않게 하기 위함.
            if (startZoneCenter != null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    // Rigidbody2D가 있으면 물리 상태도 함께 리셋 — 워프 후 이전 속도가 남아있으면 즉시 튕겨나갈 수 있음.
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.angularVelocity = 0f;
                    }
                    player.transform.position = startZoneCenter.position;
                }
                else
                {
                    Debug.LogWarning("[StageManager] Player 태그를 가진 오브젝트를 찾지 못해 워프하지 못했습니다.");
                }
            }

            // 5) 체력 최대 복구. 실패로 진입한 경우와 디버그 리트라이 모두에서 동일하게 적용된다.
            if (healthSystem != null)
                healthSystem.ResetHealth();

            // 6) 상태 → Waiting. StartZone 재이탈 시 InProgress로 전환된다.
            State = StageState.Waiting;
            Debug.Log("[StageManager] 스테이지 리셋 → Waiting");
        }

        void HandleCoordinateActivated(Coordinate _)
        {
            // InProgress일 때만 클리어 판정 — 리셋 도중 잔여 이벤트가 오더라도 클리어로 오인하지 않게 방어.
            if (State != StageState.InProgress) return;

            int activeCount = 0;
            foreach (var c in _coordinates)
                if (c != null && c.IsActive) activeCount++;

            if (activeCount >= 6)
                ClearStage();
        }

        // 테스트/디버그용. 상태 무관하게 강제로 리셋을 수행할 수 있게 함 —
        // TriggerStageFail은 InProgress일 때만 작동하므로 Waiting/Clear 상태에서 리셋 절차를 검증하려면 이 경로가 필요.
        [ContextMenu("Retry Stage")]
        void DebugRetryStage()
        {
            PerformReset();
        }
    }
}
