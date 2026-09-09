using UnityEngine;
using ProjectT.Dialogue;

namespace ProjectT.Stage
{
    // 방 안에 배치된 Prop에 붙는 트리거 컴포넌트.
    // 플레이어가 진입하면 10% 확률로 우미아가 힌트 대사(예: "저게 뭔가 있는 것 같은데요")를 낸다.
    //
    // 발화 조건 (기획서 3b2-01):
    //  - 플레이어 태그 진입
    //  - 스테이지 InProgress 상태 (Waiting/Clear 중에는 침묵)
    //  - 이 Prop에서 아직 한 번도 발화된 적이 없음 (_triggered)
    //  - 10% 확률 통과
    //
    // 왜 _triggered 로 잠그는가:
    //  - 스테이지가 리셋돼도 Prop은 씬에 남아있으며, 매 진입마다 발화되면 스팸이 된다.
    //    "이 Prop은 한 번 언급되면 끝"이라는 규칙으로 자연스러움을 확보.
    [RequireComponent(typeof(Collider2D))]
    public class PropTrigger : MonoBehaviour
    {
        [SerializeField] StageManager stageManager;
        [SerializeField] UmiaDialogueQueue dialogueQueue;
        [SerializeField] string propHintNode = "SandBox_PropHint";
        [Range(0f, 1f)] [SerializeField] float triggerChance = 0.1f;

        bool _triggered;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            // 스테이지 진행 중일 때만 힌트를 낸다. 시작 전이나 클리어 후에는 침묵.
            if (stageManager == null || stageManager.State != StageState.InProgress) return;
            if (_triggered) return;
            if (Random.value > triggerChance) return;

            _triggered = true;
            if (dialogueQueue != null)
                dialogueQueue.Enqueue(propHintNode);
        }
    }
}
