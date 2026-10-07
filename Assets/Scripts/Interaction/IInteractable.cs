using ProjectT.Dialogue;

namespace ProjectT.Interaction
{
    public readonly struct InteractContext
    {
        public readonly DialogueService Dialogue;

        public InteractContext(DialogueService dialogue)
        {
            Dialogue = dialogue;
        }
    }

    public interface IInteractable
    {
        bool CanInteract { get; }
        void ShowHighlight(UnityEngine.Color color);
        void HideHighlight();
        void Interact(InteractContext context);
    }
}
