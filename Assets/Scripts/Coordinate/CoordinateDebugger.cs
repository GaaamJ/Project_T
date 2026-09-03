using UnityEngine;
using ProjectT.Thread;

namespace ProjectT.Coordinate
{
    // #12/#13/#14 연동을 PlayMode에서 사람이 직접 확인하기 위한 디버그 도구.
    // 실제 게임 로직이 아니므로 최소한의 UI(OnGUI)와 키 입력(Update)만 노출한다.
    public class CoordinateDebugger : MonoBehaviour
    {
        [SerializeField] private ThreadBuffHolder holder;
        [SerializeField] private Coordinate coordinate;

        private void Awake()
        {
            // Awake 시점에 구독해야 씬 시작 직후 발생하는 첫 활성화 이벤트도 놓치지 않는다.
            if (coordinate != null)
                coordinate.OnActivated += HandleCoordinateActivated;
        }

        private void OnDestroy()
        {
            // 이벤트 소스가 debugger보다 오래 살 수 있으므로 명시적으로 해제한다.
            if (coordinate != null)
                coordinate.OnActivated -= HandleCoordinateActivated;
        }

        private void Start()
        {
            // 레퍼런스가 비면 이후 로직 전체가 NRE를 낼 것이므로 즉시 비활성화한다.
            if (holder == null || coordinate == null)
            {
                Debug.LogError("[CoordinateDebugger] holder 또는 coordinate 레퍼런스가 비어 있습니다. 컴포넌트를 비활성화합니다.");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                holder.Acquire(ThreadType.Red);
                Debug.Log("[CoordinateDebugger] Acquire(Red)");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                holder.Acquire(ThreadType.Blue);
                Debug.Log("[CoordinateDebugger] Acquire(Blue)");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                holder.Acquire(ThreadType.Gold);
                Debug.Log("[CoordinateDebugger] Acquire(Gold)");
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                bool result = coordinate.TryBind(holder);
                Debug.Log($"[CoordinateDebugger] TryBind result: {result}");
            }
        }

        private void OnGUI()
        {
            // 디버그 전용이므로 스타일 커스텀 없이 기본 Label로 좌상단에 그린다.
            if (holder == null || coordinate == null) return;

            // IsActive=false일 때 BoundType 필드 값은 의미가 없으므로 "-" 로 표기한다.
            string boundTypeDisplay = coordinate.IsActive ? coordinate.BoundType.ToString() : "-";

            string text =
                "[ThreadBuffHolder]\n" +
                $"HasBuff: {holder.HasBuff}\n" +
                $"CurrentType: {holder.CurrentType}\n" +
                $"RemainingTime: {holder.RemainingTime:F2}\n" +
                "\n" +
                "[Coordinate]\n" +
                $"IsActive: {coordinate.IsActive}\n" +
                $"BoundType: {boundTypeDisplay}";

            GUI.Label(new Rect(10, 10, 300, 200), text);
        }

        private void HandleCoordinateActivated(Coordinate coord)
        {
            Debug.Log($"[CoordinateDebugger] Coordinate activated: {coord.BoundType}");
        }
    }
}
