using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using ProjectT.Player;

namespace ProjectT.Dialogue
{
    // 우미아의 모든 대사를 순서대로 재생하기 위한 단일 큐.
    //
    // 왜 큐가 필요한가:
    // - 여러 트리거(스테이지 인트로/실 스폰/실패/클리어/Prop/AskHelp)가 동시에 발화할 수 있다.
    //   IsDialogueRunning 가드로 후발 트리거를 버리는 방식(기존 방식)은
    //   플레이어 관점에서 "우미아가 말을 삼킨" 것처럼 느껴져 몰입을 깬다.
    // - 큐로 관리하면 트리거된 대사는 모두 순서대로 재생되어 놓치는 상황이 없다.
    //
    // freezePlayer:
    // - 스테이지 인트로 등 "플레이어가 반드시 들어야 하는 대사"에서 이동을 잠근다.
    // - PlayerMovement.enabled = false로 FixedUpdate를 정지시켜 이동을 완전 봉쇄.
    //   비활성화 직전 velocity를 0으로 만들어 관성으로 미끄러지는 것을 방지.
    public class UmiaDialogueQueue : MonoBehaviour
    {
        struct QueueItem
        {
            public string nodeName;
            public bool freezePlayer;
        }

        [SerializeField] DialogueRunner dialogueRunner;
        [SerializeField] PlayerMovement playerMovement;

        readonly Queue<QueueItem> _queue = new();
        // 현재 재생 중인 아이템의 freezePlayer 여부를 기억. 완료 시 이 값을 보고 이동 잠금 해제 여부 판단.
        QueueItem _current;
        bool _hasCurrent;

        void Awake()
        {
            if (dialogueRunner != null)
                dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        }

        void OnDestroy()
        {
            // DialogueRunner는 씬에 남아있을 수 있어 명시적 해제로 이벤트 누수 방지.
            if (dialogueRunner != null)
                dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }

        // 외부 트리거가 대사 재생을 요청. 존재하지 않는 노드는 큐에 넣지 않는다.
        public void Enqueue(string nodeName, bool freezePlayer = false)
        {
            if (string.IsNullOrEmpty(nodeName)) return;
            if (dialogueRunner == null)
            {
                Debug.LogWarning("[UmiaDialogueQueue] DialogueRunner가 연결되지 않았습니다.");
                return;
            }

            // Yarn Project에 노드가 실제 존재하는지 검증 — 오타/누락으로 인한 런타임 예외를 조기 차단.
            if (!NodeExists(nodeName))
            {
                Debug.LogWarning($"[UmiaDialogueQueue] Yarn node '{nodeName}' not found in project.");
                return;
            }

            _queue.Enqueue(new QueueItem { nodeName = nodeName, freezePlayer = freezePlayer });

            // 현재 아무 대사도 진행 중이 아니면 즉시 재생 시도.
            // 진행 중이면 onDialogueComplete → OnDialogueComplete → TryPlayNext 경로로 이어진다.
            if (!dialogueRunner.IsDialogueRunning)
                TryPlayNext();
        }

        void TryPlayNext()
        {
            if (_queue.Count == 0) return;
            if (dialogueRunner == null) return;
            // 안전 가드: 재생 중이면 완료 콜백이 다시 이 메서드를 호출할 것.
            if (dialogueRunner.IsDialogueRunning) return;

            _current = _queue.Dequeue();
            _hasCurrent = true;

            // 이동 잠금은 대사 시작 직전에 적용. 이렇게 해야 잠금 시점과 실제 대사 시작이 일치한다.
            if (_current.freezePlayer && playerMovement != null)
            {
                var rb = playerMovement.GetComponent<Rigidbody2D>();
                // 잠금 직전 관성을 0으로 초기화 — 안 그러면 이미 이동 중이던 속도로 계속 미끄러진다.
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                playerMovement.enabled = false;
            }

            dialogueRunner.StartDialogue(_current.nodeName);
        }

        // DialogueRunner의 onDialogueComplete UnityEvent에 바인딩됨.
        void OnDialogueComplete()
        {
            // 방금 끝난 대사의 freezePlayer 상태에 따라 이동 잠금 해제.
            if (_hasCurrent && _current.freezePlayer && playerMovement != null)
                playerMovement.enabled = true;

            _hasCurrent = false;

            // 큐에 남은 다음 대사 재생.
            TryPlayNext();
        }

        bool NodeExists(string nodeName)
        {
            if (dialogueRunner.YarnProject == null) return false;
            var names = dialogueRunner.YarnProject.NodeNames;
            if (names == null) return false;
            foreach (var n in names)
                if (n == nodeName) return true;
            return false;
        }
    }
}
