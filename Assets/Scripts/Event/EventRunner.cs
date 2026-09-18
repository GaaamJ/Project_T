using UnityEngine;
using Yarn.Unity;
using ProjectT.Save;
using ProjectT.Session;

namespace ProjectT.Event
{
    // 이벤트 실행의 유일한 진입점.
    //
    // 왜 컴포넌트인가:
    // - Yarn Node 실행/중단 감지에는 DialogueRunner 참조가 필수인데,
    //   DialogueRunner 는 씬에 배치된 컴포넌트다. static 으로 만들면 씬 간 참조 관리와
    //   테스트가 오히려 번거로워진다.
    // - EventState 자체는 GameSessionManager (DontDestroyOnLoad) 가 보관하므로,
    //   EventRunner 는 씬마다 새로 생겨도 세션 상태의 연속성은 유지된다.
    //
    // 이벤트별 분기를 넣지 않는다:
    // - EventDefinition.CanRun 이 반복 정책·선행 조건을 판정하고,
    //   YarnNode 이름과 완료 ID 도 EventDefinition 이 들고 있다.
    // - 여기서 필요한 것은 오직 "요청 → 조건 확인 → Yarn 실행 → 완료 기록" 파이프라인뿐.
    // - 새 이벤트를 추가할 때 이 파일을 수정하지 않는 것이 이번 시스템의 핵심 확장 원칙.
    public class EventRunner : MonoBehaviour
    {
        [SerializeField] EventCatalog catalog;
        [SerializeField] DialogueRunner dialogueRunner;

        // 실행 중인 이벤트가 있으면 후발 TryRun 을 거부하기 위한 플래그.
        // "한 번에 하나의 이벤트만 실행" 규칙을 EventRunner 가 단독으로 보장한다.
        bool _isRunning;

        // 현재 실행 중인 이벤트의 ID. 완료 콜백에서 어떤 이벤트를 완료 처리할지 결정하기 위해 보관.
        string _runningId;

        // Yarn Spinner v3 우회 관련 캐시:
        // - Awake 에서 DialogueRunner 의 DialogueCompleteHandler 를 한 번만 우리 콜백으로 교체하고,
        //   원래 핸들러를 _origDialogueCompleteHandler 에 보관해 두었다가 함께 호출한다.
        // - 왜 Awake 에서 한 번인가:
        //   * TryRun 이 호출될 때마다 후킹하면, 완료되지 않은 상태로 다시 TryRun 이 들어올 경우
        //     체인이 계속 쌓여 Overture 시절의 "hang" 이 재현될 위험이 있다.
        //   * Yarn 이 발화하는 완료 신호는 EventRunner 인스턴스 생명주기 동안 항상 우리 콜백을
        //     경유하도록 고정하고, 실제 완료 처리는 "지금 우리가 시작한 이벤트인가"만 확인한다.
        Yarn.DialogueCompleteHandler _origDialogueCompleteHandler;

        void Awake()
        {
            if (dialogueRunner == null)
            {
                Debug.LogWarning("[EventRunner] dialogueRunner 참조가 비어 있습니다.");
                return;
            }

            // Yarn Spinner v3 의 onDialogueComplete UnityEvent 는 발화하지 않는 알려진 이슈가 있어,
            // 내부 Dialogue 객체의 DialogueCompleteHandler 를 직접 후킹한다.
            // (Overture 아카이브 브랜치의 우회 방식과 동일 원리.)
            _origDialogueCompleteHandler = dialogueRunner.Dialogue.DialogueCompleteHandler;
            dialogueRunner.Dialogue.DialogueCompleteHandler = OnDialogueCompleteInternal;
        }

        void OnDestroy()
        {
            // 원 핸들러를 복원해 DialogueRunner 를 깨끗한 상태로 되돌린다.
            // (EventRunner 만 남는 게 아니라 다른 시스템이 DialogueRunner 를 계속 쓸 수 있으므로.)
            if (dialogueRunner != null && dialogueRunner.Dialogue != null)
                dialogueRunner.Dialogue.DialogueCompleteHandler = _origDialogueCompleteHandler;
        }

        // 이벤트 실행 요청. 성공 여부(true=Yarn 시작함)를 반환해 호출자가 부가 처리를 하고 싶다면 활용.
        public bool TryRun(string eventId)
        {
            Debug.Log($"[EventRunner] TryRun 요청: {eventId}");

            // 이미 실행 중 → 즉시 거부. 완료 상태로 전환되지 않고 로그만 남긴다.
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

            // 반복 정책 + 선행 조건 판정을 EventDefinition 에 위임한다.
            // EventRunner 는 이벤트별 분기를 몰라야 한다.
            if (!definition.CanRun(session.EventState))
            {
                // 완료된 일회성 이벤트인지, 선행 조건 미충족인지는 CanRun 이 내부에서 판정.
                // 여기서는 왜 거부됐는지까지 로깅하지는 않지만, 완료 케이스는 가장 흔한 시나리오라
                // 별도 로그로 구분해 준다.
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

        // Yarn 내부의 DialogueCompleteHandler 훅에서 호출된다.
        // - 원 핸들러도 반드시 호출해 다른 리스너(있다면) 를 방해하지 않는다.
        // - 우리 이벤트가 실제로 실행 중이었을 때만 완료 처리한다.
        //   (예: 우리가 시작하지 않은 대사가 종료되는 경우 완료 기록하지 않음.)
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

            // 정상 종료된 경우에만 완료 기록 + 저장.
            // (중단, 오류 케이스는 이 콜백에 도달하지 못하도록 Yarn 이 관리한다.)
            session.EventState.MarkCompleted(completedId);
            session.EventState.Save(SaveManager.Data);
            SaveManager.Save();

            Debug.Log($"[EventRunner] 완료 기록: {completedId}");
            Debug.Log("[EventRunner] 저장 완료");
        }
    }
}
