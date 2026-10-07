namespace ProjectT.Interaction
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        void ShowHighlight(UnityEngine.Color color);
        void HideHighlight();
    }
}
