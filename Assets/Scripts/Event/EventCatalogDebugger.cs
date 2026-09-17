using UnityEngine;

public class EventCatalogDebugger : MonoBehaviour
{
    [SerializeField] EventCatalog catalog;

    void Start()
    {
        catalog.ValidateIds();

        var state = new EventState();

        Log("EventA", catalog.GetById("EventA")?.CanRun(state));
        Log("EventEx", catalog.GetById("EventEx")?.CanRun(state));
        Log("EventB", catalog.GetById("EventB")?.CanRun(state));
        Log("EventC", catalog.GetById("EventC")?.CanRun(state));
        Log("FirstEncounter", catalog.GetById("FirstEncounter")?.CanRun(state));

        state.MarkCompleted("EventA");
        state.MarkCompleted("EventEx");
        Debug.Log("[EventCatalogDebugger] EventA & EventEx 완료 처리 후:");
        Log("EventB", catalog.GetById("EventB")?.CanRun(state));
        Log("EventC", catalog.GetById("EventC")?.CanRun(state));

        state.MarkCompleted("FirstEncounter");
        Debug.Log("[EventCatalogDebugger] FirstEncounter 완료 처리 후:");
        Log("FirstEncounter (Once 정책)", catalog.GetById("FirstEncounter")?.CanRun(state));
    }

    void Log(string id, bool? result) =>
        Debug.Log($"[EventCatalogDebugger] {id}: CanRun = {result?.ToString() ?? "null (조회 실패)"}");
}
