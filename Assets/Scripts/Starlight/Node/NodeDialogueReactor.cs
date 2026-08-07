using UnityEngine;
using Yarn.Unity;

public class NodeDialogueReactor : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string successYarnNode = "SuccessReaction";
    [SerializeField] private string failYarnNode = "FailReaction";

    void OnEnable()
    {
        NodeManager.OnSymbolSubmitted += HandleSymbolSubmitted;
    }

    void OnDisable()
    {
        NodeManager.OnSymbolSubmitted -= HandleSymbolSubmitted;
    }

    private void HandleSymbolSubmitted(bool success)
    {
        string target = success ? successYarnNode : failYarnNode;
        dialogueRunner.StartDialogue(target);
    }
}