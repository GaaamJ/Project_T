using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EventCatalog", menuName = "ProjectT/EventCatalog")]
public class EventCatalog : ScriptableObject
{
    public EventDefinition[] events;

    public EventDefinition GetById(string id)
    {
        foreach (var e in events)
            if (e != null && e.id == id) return e;
        return null;
    }

    public void ValidateIds()
    {
        var seen = new HashSet<string>();
        foreach (var e in events)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.id))
                Debug.LogWarning($"[EventCatalog] 빈 ID가 있는 EventDefinition: {e.name}");
            else if (!seen.Add(e.id))
                Debug.LogWarning($"[EventCatalog] 중복 ID: {e.id}");
        }
    }
}
