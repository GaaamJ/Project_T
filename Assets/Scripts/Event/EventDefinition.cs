using UnityEngine;

[CreateAssetMenu(fileName = "EventDefinition", menuName = "ProjectT/EventDefinition")]
public class EventDefinition : ScriptableObject
{
    public string id;
    public string yarnNode;
    public RepeatPolicy repeatPolicy;
    public PrerequisiteMode prerequisiteMode;
    public EventDefinition[] prerequisites;

    public bool CanRun(EventState state)
    {
        if (repeatPolicy == RepeatPolicy.Once && state.IsCompleted(id))
            return false;

        if (prerequisites == null || prerequisites.Length == 0)
            return true;

        if (prerequisiteMode == PrerequisiteMode.All)
        {
            foreach (var p in prerequisites)
                if (!state.IsCompleted(p.id)) return false;
            return true;
        }
        else
        {
            foreach (var p in prerequisites)
                if (state.IsCompleted(p.id)) return true;
            return false;
        }
    }
}
