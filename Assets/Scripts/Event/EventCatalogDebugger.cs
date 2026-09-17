using UnityEngine;
using ProjectT.Save;

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
        Debug.Log("[Step2][EventCatalogDebugger] EventA & EventEx 완료 처리 후:");
        Log("EventB", catalog.GetById("EventB")?.CanRun(state));
        Log("EventC", catalog.GetById("EventC")?.CanRun(state));

        state.MarkCompleted("FirstEncounter");
        Debug.Log("[Step2][EventCatalogDebugger] FirstEncounter 완료 처리 후:");
        Log("FirstEncounter (Once 정책)", catalog.GetById("FirstEncounter")?.CanRun(state));

        // Step 3 검증: 세이브에서 불러온 EventState 기준으로 CanRun 을 재확인한 뒤,
        // FirstEncounter 완료 처리와 저장까지 왕복시켜 다음 실행 때 결과가 뒤집히는지 본다.
        // SaveManager 는 static 이라 Load() 가 내부의 Data 프로퍼티를 갱신한다.
        SaveManager.Load();
        var saveData = SaveManager.Data;
        var loadedState = new EventState();
        loadedState.Load(saveData);
        Debug.Log($"[Step3][EventCatalogDebugger] 불러온 상태 기준 FirstEncounter: CanRun = {catalog.GetById("FirstEncounter")?.CanRun(loadedState)}");

        loadedState.MarkCompleted("FirstEncounter");
        loadedState.Save(saveData);
        SaveManager.Save();
        Debug.Log("[Step3][EventCatalogDebugger] FirstEncounter 완료 저장 완료");
    }

    void Log(string id, bool? result) =>
        Debug.Log($"[Step2][EventCatalogDebugger] {id}: CanRun = {result?.ToString() ?? "null (조회 실패)"}");
}
