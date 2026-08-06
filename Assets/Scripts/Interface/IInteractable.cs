public interface IInteractable
{
    bool IsActivated { get; }

    void Interact();
}

public interface IInstantInteractable : IInteractable { }

public interface IDelayInteractable : IInteractable
{
    float HoldDuration { get; }
}