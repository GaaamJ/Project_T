using UnityEngine;
using ProjectT.Dialogue;
using ProjectT.Interaction;

namespace ProjectT.Player
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] PlayerInteractInput input;
        [SerializeField] DetectZone detectZone;
        [SerializeField] DialogueService dialogueService;

        void OnEnable()
        {
            input.InteractPressed += OnInteractPressed;
        }

        void OnDisable()
        {
            input.InteractPressed -= OnInteractPressed;
        }

        void OnInteractPressed()
        {
            IInteractable target = detectZone.CurrentTarget;
            if (!DetectZone.IsAlive(target) || !target.CanInteract)
                return;

            target.Interact(new InteractContext(dialogueService));
        }
    }
}
