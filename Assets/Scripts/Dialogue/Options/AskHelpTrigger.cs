using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

public class AskHelpTrigger : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private InputActionReference askHelpAction; // ` 키 바인딩
    [SerializeField] private string askHelpYarnNode = "AskForHelp";

    void Update()
    {
        if (askHelpAction.action.WasPressedThisFrame() && !dialogueRunner.IsDialogueRunning)
        {
            dialogueRunner.StartDialogue(askHelpYarnNode);
        }
    }
}