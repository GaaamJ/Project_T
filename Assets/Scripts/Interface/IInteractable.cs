public interface IInteractable
{
    bool IsSelected { get; }

    void Interact();
}

public interface IInstantInteractable : IInteractable { }

public interface IDelayInteractable : IInteractable
{
    float HoldDuration { get; }
}