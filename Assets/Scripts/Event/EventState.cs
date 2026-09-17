using System.Collections.Generic;

public class EventState
{
    readonly HashSet<string> _completedIds = new();

    public bool IsCompleted(string id) => _completedIds.Contains(id);

    public void MarkCompleted(string id)
    {
        _completedIds.Add(id);
    }
}
