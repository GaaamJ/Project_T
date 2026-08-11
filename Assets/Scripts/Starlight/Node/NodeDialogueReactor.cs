using UnityEngine;
using Yarn.Unity;

public class NodeDialogueReactor : MonoBehaviour
{
    [SerializeField] private NodeManager nodeManager; // static 대신 직접 참조 (별빛 #5)
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string successYarnNode = "SuccessReaction";
    [SerializeField] private string failYarnNode = "FailReaction";

    void OnEnable()
    {
        nodeManager.OnSymbolSubmitted += HandleSymbolSubmitted;
    }

    void OnDisable()
    {
        nodeManager.OnSymbolSubmitted -= HandleSymbolSubmitted;
    }

    private void HandleSymbolSubmitted(bool success)
    {
        string target = success ? successYarnNode : failYarnNode;
        dialogueRunner.StartDialogue(target);
    }
}