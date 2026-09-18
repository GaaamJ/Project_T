using UnityEngine;
using Yarn.Unity;
using ProjectT.Save;
using ProjectT.Session;

namespace ProjectT.Event
{
    public class EventRunner : MonoBehaviour
    {
        [SerializeField] EventCatalog catalog;
        [SerializeField] DialogueRunner dialogueRunner;

        bool _isRunning;
        string _runningId;

        // Yarn Spinner v3: onDialogueComplete UnityEvent가 발화하지 않는 알려진 이슈로,
        // 내부 DialogueCompleteHandler를 Awake에서 한 번만 후킹한다.
        Yarn.DialogueCompleteHandler _origDialogueCompleteHandler;

        void Awake()
        {
            if (dialogueRunner == null)
            {
                Debug.LogWarning("[EventRunner] dialogueRunner 참조가 비어 있습니다.");
                return;
            }

            _origDialogueCompleteHandler = dialogueRunner.Dialogue.DialogueCompleteHandler;
            dialogueRunner.Dialogue.DialogueCompleteHandler = OnDialogueCompleteInternal;
        }

        void OnDestroy()
        {
            if (dialogueRunner != null && dialogueRunner.Dialogue != null)
                dialogueRunner.Dialogue.DialogueCompleteHandler = _origDialogueCompleteHandler;
        }

        public bool TryRun(string eventId)
        {
            Debug.Log($"[EventRunner] TryRun 요청: {eventId}");

            if (_isRunning)
            {
                Debug.Log($"[EventRunner] 거부 (실행 중): {eventId}");
                return false;
            }

            if (catalog == null)
            {
                Debug.LogWarning("[EventRunner] catalog 참조가 비어 있습니다.");
                return false;
            }

            var definition = catalog.GetById(eventId);
            if (definition == null)
            {
                Debug.LogWarning($"[EventRunner] 거부 (카탈로그 미존재): {eventId}");
                return false;
            }

            var session = GameSessionManager.Instance;
            if (session == null)
            {
                Debug.LogWarning("[EventRunner] GameSessionManager 인스턴스가 없습니다. 씬에 배치되었는지 확인하세요.");
                return false;
            }

            if (!definition.CanRun(session.EventState))
            {
                if (session.EventState.IsCompleted(definition.id))
                    Debug.Log($"[EventRunner] 거부 (이미 완료): {eventId}");
                else
                    Debug.Log($"[EventRunner] 거부 (조건 미충족): {eventId}");
                return false;
            }

            if (dialogueRunner == null)
            {
                Debug.LogWarning("[EventRunner] dialogueRunner 참조가 비어 있습니다.");
                return false;
            }

            if (!dialogueRunner.Dialogue.NodeExists(definition.yarnNode))
            {
                Debug.LogWarning($"[EventRunner] 거부 (Yarn Node 없음): {definition.yarnNode}");
                return false;
            }

            _isRunning = true;
            _runningId = definition.id;

            Debug.Log($"[EventRunner] 실행 시작: {_runningId}");

            dialogueRunner.StartDialogue(definition.yarnNode);

            return true;
        }

        void OnDialogueCompleteInternal()
        {
            _origDialogueCompleteHandler?.Invoke();

            if (!_isRunning) return;

            var completedId = _runningId;
            _runningId = null;
            _isRunning = false;

            var session = GameSessionManager.Instance;
            if (session == null)
            {
                Debug.LogWarning("[EventRunner] 완료 처리 시 GameSessionManager 를 찾지 못했습니다.");
                return;
            }

            session.EventState.MarkCompleted(completedId);
            session.EventState.Save(SaveManager.Data);
            SaveManager.Save();

            Debug.Log($"[EventRunner] 완료 기록: {completedId}");
            Debug.Log("[EventRunner] 저장 완료");
        }
    }
}
