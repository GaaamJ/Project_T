using UnityEngine;
using Yarn.Unity;
using ProjectT.Session;

namespace ProjectT.Dialogue
{
    [RequireComponent(typeof(DialogueRunner))]
    public class DialogueService : MonoBehaviour
    {
        [SerializeField] InputLock inputLock;

        DialogueRunner runner;
        bool isPlaying;
        bool nodeCompleted;

        void Awake()
        {
            runner = GetComponent<DialogueRunner>();

            if (inputLock == null)
                Debug.LogError("[DialogueService] InputLock 참조가 비어 있다.", this);
        }

        public async YarnTask<DialogueResult> Play(string nodeName)
        {
            if (isPlaying || runner.IsDialogueRunning)
            {
                Debug.LogWarning($"[DialogueService] 대화 중이라 '{nodeName}' 재생을 거부했다.", this);
                return DialogueResult.Rejected;
            }

            if (string.IsNullOrEmpty(nodeName) || !runner.Dialogue.NodeExists(nodeName))
            {
                Debug.LogWarning($"[DialogueService] 노드 '{nodeName}'가 없다.", this);
                return DialogueResult.Rejected;
            }

            // isPlaying보다 먼저 건다. 참조가 비어 여기서 예외가 나도 isPlaying이 true로 굳지 않게 하기 위함.
            inputLock.Lock(InputLockReason.Dialogue);
            isPlaying = true;
            nodeCompleted = false;
            runner.onNodeStart.AddListener(OnNodeStart);
            runner.onNodeComplete.AddListener(OnNodeComplete);

            try
            {
                // StartDialogue는 첫 내용을 띄우는 시점에 반환되므로 종료는 DialogueTask로 기다린다.
                await runner.StartDialogue(nodeName);
                await runner.DialogueTask;

                // DialogueTask가 끝난 직후에는 IsDialogueRunning이 아직 true라서 바로 다음 대화를 시작하면 거부된다.
                while (runner.IsDialogueRunning)
                    await YarnTask.Yield();
            }
            finally
            {
                runner.onNodeStart.RemoveListener(OnNodeStart);
                runner.onNodeComplete.RemoveListener(OnNodeComplete);
                isPlaying = false;
                inputLock.Unlock(InputLockReason.Dialogue);
            }

            return nodeCompleted ? DialogueResult.Completed : DialogueResult.Interrupted;
        }

        // 노드 끝이나 <<stop>>에서는 NodeComplete가 오지만, runner.Stop()·파괴로 끊기면 오지 않는다.
        void OnNodeStart(string nodeName) => nodeCompleted = false;

        void OnNodeComplete(string nodeName) => nodeCompleted = true;
    }
}
