using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;
using ProjectT.Dialogue;

// 백틱(`) 키로 우미아에게 도움을 요청하는 트리거.
// 대사 큐에 요청하도록 리팩토링 — IsDialogueRunning 가드 없이 큐가 순서를 관리.
public class AskHelpTrigger : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private UmiaDialogueQueue dialogueQueue;
    [SerializeField] private InputActionReference askHelpAction; // ` 키 바인딩
    [SerializeField] private string askHelpYarnNode = "AskForHelp";

    void Update()
    {
        // 큐 도입 이후: IsDialogueRunning 체크를 제거해 유저가 도움을 요청하면
        // 이전 대사가 끝난 뒤 순서대로 재생되도록 한다.
        if (!askHelpAction.action.WasPressedThisFrame()) return;

        if (dialogueQueue != null)
        {
            dialogueQueue.Enqueue(askHelpYarnNode);
        }
        else if (dialogueRunner != null && !dialogueRunner.IsDialogueRunning)
        {
            // 폴백: 큐가 연결되지 않았을 때만 직접 재생 (옛 동작 유지).
            dialogueRunner.StartDialogue(askHelpYarnNode);
        }
    }
}
