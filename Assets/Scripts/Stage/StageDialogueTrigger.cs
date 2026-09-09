using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectT.Dialogue;

namespace ProjectT.Stage
{
    // StageManager의 상태 이벤트를 받아 우미아 대사 큐에 상황별 노드를 넣는 트리거.
    //
    // 담당 케이스 (기획서 3b 대사 트리거 시스템 기준):
    //  - 3b1: 씬 첫 방문(Waiting 진입) 시 스테이지 인트로 대사 1회. SaveManager로 재방문 여부 판정.
    //  - 3b3: 실패(리셋) 시 c/02/03 세 종류를 독립 10% 확률로 판정해 큐에 추가.
    //         c의 좌표 수 분기(0~2 / 3~4 / 5)는 StageManager가 리셋 직전에 캡처한 값으로 결정.
    //  - 3b4: 클리어 시 클리어 대사 1회.
    //
    // 왜 StageManager와 분리했는가:
    // - StageManager는 스테이지 상태 머신 관리에 집중해야 한다. 대사 정책(어떤 노드, 어떤 확률)이
    //   섞이면 규칙 변경 때마다 스테이지 로직이 흔들린다. 이벤트 구독 방식으로 완전히 분리.
    public class StageDialogueTrigger : MonoBehaviour
    {
        [SerializeField] StageManager stageManager;
        [SerializeField] UmiaDialogueQueue dialogueQueue;

        [Header("Nodes — Stage Intro (3b1)")]
        [SerializeField] string introNode = "SandBox_StageIntro";

        [Header("Nodes — Fail NiceTry (3b3-c)")]
        [SerializeField] string niceTryFewNode = "SandBox_NiceTry_Few";     // 0~2개
        [SerializeField] string niceTryHalfNode = "SandBox_NiceTry_Half";   // 3~4개
        [SerializeField] string niceTryAlmostNode = "SandBox_NiceTry_Almost"; // 5개

        [Header("Nodes — Fail Chat/Retry (3b3-02, 03)")]
        [SerializeField] string chatNode = "SandBox_Chat";
        [SerializeField] string retryNode = "SandBox_Retry";

        [Header("Nodes — Stage Clear (3b4)")]
        [SerializeField] string clearNode = "SandBox_Clear";

        [Header("Probabilities")]
        [Range(0f, 1f)] [SerializeField] float niceTryChance = 0.1f;
        [Range(0f, 1f)] [SerializeField] float chatChance = 0.1f;
        [Range(0f, 1f)] [SerializeField] float retryChance = 0.1f;

        void Start()
        {
            if (stageManager == null || dialogueQueue == null) return;

            stageManager.OnStageFailed += HandleStageFailed;
            stageManager.OnStageCleared += HandleStageCleared;

            // 3b1: 씬 최초 진입 시 인트로. SaveManager로 "이 씬에서 첫 방문인지" 판정.
            // Waiting 상태에서 즉시 큐잉하며, freezePlayer=true로 이동을 잠근다.
            TryPlayStageIntro();
        }

        void OnDestroy()
        {
            if (stageManager != null)
            {
                stageManager.OnStageFailed -= HandleStageFailed;
                stageManager.OnStageCleared -= HandleStageCleared;
            }
        }

        void TryPlayStageIntro()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            // 씬별 첫 방문 키 — 다른 씬의 인트로와 충돌하지 않도록 씬 이름 접두어 사용.
            string firstVisitKey = $"{sceneName}_StageDialogue_FirstVisit";

            if (SaveManager.Instance.IsVisited(firstVisitKey)) return;

            dialogueQueue.Enqueue(introNode, freezePlayer: true);
            SaveManager.Instance.MarkVisited(firstVisitKey);
        }

        void HandleStageFailed(int coordsActive)
        {
            // 세 판정은 독립적으로 이뤄진다 — c가 발화됐다고 02/03이 막히지 않는다.
            // 큐 구조 덕분에 세 개가 모두 통과되어도 순서대로 재생된다.

            // 3b3-c: 활성 좌표 수에 따른 아쉬움 대사.
            if (Random.value <= niceTryChance)
            {
                string node = SelectNiceTryNode(coordsActive);
                if (!string.IsNullOrEmpty(node))
                    dialogueQueue.Enqueue(node);
            }

            // 3b3-02: 일반 대사.
            if (Random.value <= chatChance)
                dialogueQueue.Enqueue(chatNode);

            // 3b3-03: 리트라이 대사.
            if (Random.value <= retryChance)
                dialogueQueue.Enqueue(retryNode);
        }

        // 활성 좌표 수를 3구간으로 분리해 대사를 매핑.
        // 0~2: "아직 익숙하지 않아요?" / 3~4: "거의 다 온 것 같았는데" / 5: "하나 남았었는데"
        string SelectNiceTryNode(int coordsActive)
        {
            if (coordsActive >= 5) return niceTryAlmostNode;
            if (coordsActive >= 3) return niceTryHalfNode;
            return niceTryFewNode;
        }

        void HandleStageCleared()
        {
            // 3b4: 클리어 대사. freezePlayer는 하지 않음 — 클리어 후 자유 이동 필요.
            dialogueQueue.Enqueue(clearNode);
        }
    }
}
