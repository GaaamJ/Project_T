using System.Collections.Generic;
using UnityEngine;
using ProjectT.Dialogue;

namespace ProjectT.Interaction
{
    public readonly struct InteractContext
    {
        public readonly DialogueService Dialogue;
        public readonly HashSet<string> Investigated;

        public InteractContext(DialogueService dialogue, HashSet<string> investigated)
        {
            Dialogue = dialogue;
            Investigated = investigated;
        }
    }

    public interface IInteractable
    {
        bool CanInteract { get; }
        void ShowHighlight(Color color);
        void HideHighlight();
        void Interact(InteractContext context);
    }

    public static class InteractableExtensions
    {
        // 인터페이스로 받으면 파괴된 오브젝트도 == null이 false라서 UnityEngine.Object로 비교한다.
        public static bool IsAlive(this IInteractable interactable)
        {
            return interactable is Object unityObject ? unityObject != null : interactable != null;
        }
    }
}
