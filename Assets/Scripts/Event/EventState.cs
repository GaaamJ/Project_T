using System.Collections.Generic;
using UnityEngine;
using ProjectT.Save;

public class EventState
{
    // HashSet 을 쓰는 이유: 완료 여부 조회(IsCompleted) 가 O(1) 이고,
    // 같은 ID 를 중복 완료 처리해도 자동으로 하나만 남기 때문.
    // 저장 시에는 List<string> 으로 변환한다 (JsonUtility 는 HashSet 직렬화 불가).
    readonly HashSet<string> _completedIds = new();

    public bool IsCompleted(string id) => _completedIds.Contains(id);

    public void MarkCompleted(string id)
    {
        _completedIds.Add(id);
        Debug.Log($"[EventState] 완료 기록: {id}");
    }

    // SaveData 의 List<string> 을 내부 HashSet 으로 흡수한다.
    // Clear 를 먼저 호출해서 이전 세션 상태가 남지 않도록 보장한다.
    public void Load(SaveData data)
    {
        _completedIds.Clear();
        if (data.completedEventIds != null)
        {
            foreach (var id in data.completedEventIds)
                _completedIds.Add(id);
        }
        Debug.Log($"[EventState] 로드: 완료된 이벤트 {_completedIds.Count}개" +
                  (_completedIds.Count > 0 ? $" — {string.Join(", ", _completedIds)}" : ""));
    }

    // HashSet -> List 변환. new List 로 스냅샷을 만들어 이후 EventState 변경이
    // SaveData 에 새어 들어가지 않도록 격리한다.
    public void Save(SaveData data)
    {
        data.completedEventIds = new List<string>(_completedIds);
    }
}
