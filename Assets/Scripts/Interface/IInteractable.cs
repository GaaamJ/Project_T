public interface IInteractable
{
    void Interact();
}

public interface IInstantInteractable : IInteractable { }

public interface IDelayInteractable : IInteractable
{
    float HoldDuration { get; }
}