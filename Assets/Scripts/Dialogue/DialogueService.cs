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

        void Awake()
        {
            runner = GetComponent<DialogueRunner>();

            if (inputLock == null)
                Debug.LogError("[DialogueService] InputLock 참조가 비어 있다.", this);
        }

        public async YarnTask<bool> Play(string nodeName)
        {
            if (isPlaying || runner.IsDialogueRunning)
            {
                Debug.LogWarning($"[DialogueService] 대화 중이라 '{nodeName}' 재생을 거부했다.", this);
                return false;
            }

            if (string.IsNullOrEmpty(nodeName) || !runner.Dialogue.NodeExists(nodeName))
            {
                Debug.LogWarning($"[DialogueService] 노드 '{nodeName}'가 없다.", this);
                return false;
            }

            // isPlaying보다 먼저 건다. 참조가 비어 여기서 예외가 나도 isPlaying이 true로 굳지 않게 하기 위함.
            inputLock.Lock(InputLockReason.Dialogue);
            isPlaying = true;

            try
            {
                // StartDialogue는 첫 내용을 띄우는 시점에 반환되므로 종료는 DialogueTask로 기다린다.
                await runner.StartDialogue(nodeName);
                await runner.DialogueTask;

                // DialogueTask가 끝난 직후에는 IsDialogueRunning이 아직 true라서 바로 다음 대화를 시작하면 거부된다.
                while (runner != null && runner.IsDialogueRunning)
                    await YarnTask.Yield();
            }
            finally
            {
                isPlaying = false;
                inputLock.Unlock(InputLockReason.Dialogue);
            }

            return true;
        }
    }
}
