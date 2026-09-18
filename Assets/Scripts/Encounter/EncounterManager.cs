using UnityEngine;
using ProjectT.Event;

namespace ProjectT.Encounter
{
    public class EncounterManager : MonoBehaviour
    {
        [SerializeField] EventRunner runner;
        [SerializeField] EventDefinition startEvent;

        void Start()
        {
            if (runner == null)
            {
                Debug.LogWarning("[EncounterManager] runner 참조가 비어 있습니다.");
                return;
            }
            if (startEvent == null)
            {
                Debug.LogWarning("[EncounterManager] startEvent 참조가 비어 있습니다.");
                return;
            }

            bool started = runner.TryRun(startEvent.id);
            if (!started)
                Debug.Log($"[EncounterManager] 시작 이벤트 실행 안 됨: {startEvent.id}");
        }
    }
}
