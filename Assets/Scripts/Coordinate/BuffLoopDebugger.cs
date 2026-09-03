using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Thread; // ThreadBuffHolder, ThreadType, YarnSpawnManager 모두 ProjectT.Thread namespace에 있음

namespace ProjectT.Coordinate
{
    // Player·Umia 두 홀더의 버프 수급/반납/바인딩과 스폰 매니저 상태를
    // 한 화면에서 병렬로 관찰하기 위한 런타임 디버그 도우미.
    // 실제 게임 인풋 시스템이 붙기 전까지 IMGUI + 키보드 단축키로 루프를 검증한다.
    public class BuffLoopDebugger : MonoBehaviour
    {
        [SerializeField] ThreadBuffHolder playerHolder;
        [SerializeField] ThreadBuffHolder umiaHolder;
        [SerializeField] Coordinate coordinate;
        [SerializeField] YarnSpawnManager spawnManager;

        void Start()
        {
            // 인스펙터 연결 누락은 디버깅 도구 자체를 무력화하지 말고 명확히 알리고 자기 자신을 끈다 —
            // NullReferenceException이 매 프레임 튀는 상황보다 로그 한 줄이 낫다.
            if (playerHolder == null || umiaHolder == null || coordinate == null || spawnManager == null)
            {
                Debug.LogError("[BuffLoopDebugger] 레퍼런스가 비어 있습니다. 컴포넌트를 비활성화합니다.");
                enabled = false;
                return;
            }
            // 스테이지 진입 트리거가 아직 없으므로 디버거가 대신 킥오프 —
            // ContextMenu 테스트 메서드를 없앤 대신 이 진입점이 스폰 루프를 시작한다.
            spawnManager.StartStage();
        }

        void Update()
        {
            // 프로젝트가 InputSystem 패키지를 사용하므로 legacy Input.GetKeyDown이 아닌 Keyboard.current 사용.
            var kb = Keyboard.current;
            if (kb == null) return;

            // Player 버프 수급 (1/2/3)
            if (kb.digit1Key.wasPressedThisFrame) playerHolder.Acquire(ThreadType.Red);
            else if (kb.digit2Key.wasPressedThisFrame) playerHolder.Acquire(ThreadType.Blue);
            else if (kb.digit3Key.wasPressedThisFrame) playerHolder.Acquire(ThreadType.Gold);

            // Player 반납 / 바인딩
            if (kb.qKey.wasPressedThisFrame) playerHolder.Consume();
            if (kb.bKey.wasPressedThisFrame)
            {
                bool result = coordinate.TryBind(playerHolder);
                Debug.Log($"[BuffLoopDebugger] Player TryBind: {result}");
            }

            // Umia 버프 수급 (4/5/6)
            if (kb.digit4Key.wasPressedThisFrame) umiaHolder.Acquire(ThreadType.Red);
            else if (kb.digit5Key.wasPressedThisFrame) umiaHolder.Acquire(ThreadType.Blue);
            else if (kb.digit6Key.wasPressedThisFrame) umiaHolder.Acquire(ThreadType.Gold);

            // Umia 반납 / 바인딩
            if (kb.eKey.wasPressedThisFrame) umiaHolder.Consume();
            if (kb.nKey.wasPressedThisFrame)
            {
                bool result = coordinate.TryBind(umiaHolder);
                Debug.Log($"[BuffLoopDebugger] Umia TryBind: {result}");
            }
        }

        void OnGUI()
        {
            // Start에서 비활성화되지만, 방어적으로 한 번 더 체크 (도메인 리로드/컴포넌트 재활성 대응)
            if (playerHolder == null || umiaHolder == null || coordinate == null) return;

            // Player 상태 — HasBuff=false일 때 CurrentType은 stale 값이므로 문자열도 NONE으로 표기.
            string playerType = playerHolder.HasBuff ? playerHolder.CurrentType.ToString() : "NONE";
            string playerTime = playerHolder.HasBuff ? playerHolder.RemainingTime.ToString("F2") : "0.00";

            // Umia 상태
            string umiaType = umiaHolder.HasBuff ? umiaHolder.CurrentType.ToString() : "NONE";
            string umiaTime = umiaHolder.HasBuff ? umiaHolder.RemainingTime.ToString("F2") : "0.00";

            // Coordinate 상태
            string coordActive = coordinate.IsActive.ToString();
            string coordType = coordinate.IsActive ? coordinate.BoundType.ToString() : "NONE";

            string text =
                "[Player]                    [Umia]\n" +
                $"HasBuff: {playerHolder.HasBuff,-20} HasBuff: {umiaHolder.HasBuff}\n" +
                $"Type:    {playerType,-20} Type:    {umiaType}\n" +
                $"Time:    {playerTime,-20} Time:    {umiaTime}\n" +
                "1/2/3: 수급  Q: 반납  B: 바인딩   4/5/6: 수급  E: 반납  N: 바인딩\n" +
                "\n" +
                "[Coordinate]\n" +
                $"IsActive: {coordActive}\n" +
                $"BoundType: {coordType}\n";

            if (spawnManager != null)
            {
                // ○/✕로 스폰 매니저가 현재 각 색상 실을 씬에 살려두고 있는지 즉시 확인.
                string redState   = spawnManager.IsTypeActive(ThreadType.Red)  ? "○" : "✕";
                string blueState  = spawnManager.IsTypeActive(ThreadType.Blue) ? "○" : "✕";
                string goldState  = spawnManager.IsTypeActive(ThreadType.Gold) ? "○" : "✕";
                text += $"\n[Yarn]\nRed: {redState}  Blue: {blueState}  Gold: {goldState}";
            }

            GUI.Label(new Rect(10, 10, 500, 300), text);
        }
    }
}
