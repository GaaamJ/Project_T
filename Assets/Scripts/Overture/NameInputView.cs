using ProjectT.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace ProjectT.Overture
{
    // Yarn 커맨드 <<ask_player_name>> 을 처리하는 뷰.
    //
    // 왜 async YarnTask 방식인가:
    // - Yarn Spinner v3 는 커맨드 핸들러가 반환하는 YarnTask 가 완료될 때까지 DialogueRunner 를
    //   자동으로 일시 중단시킨다. Stop/StartDialogue 로 재개하는 방식보다 상태 관리가 훨씬 단순.
    // - 입력 대기는 본질적으로 "사용자가 확인을 누를 때까지" 라는 비동기 이벤트라서
    //   YarnTaskCompletionSource 로 콜백을 await 로 자연스럽게 표현 가능.
    //
    // 빈 문자열 처리:
    // - a안(조용히 무시)을 채택. TrySetResult 를 호출하지 않으면 태스크가 완료되지 않아
    //   DialogueRunner 는 여전히 대기 상태를 유지하고, 사용자는 다시 입력할 수 있다.
    //   에러 표시 UI 를 추가하지 않아 구현 표면을 최소화한다.
    public class NameInputView : MonoBehaviour
    {
        // 세이브 필드명이자 Yarn 변수명. 오타 방지를 위해 한 곳에서 상수로 관리.
        const string InGameNameVariable = "$in_game_name";

        [Tooltip("<<ask_player_name>> 커맨드를 등록할 DialogueRunner.")]
        [SerializeField] DialogueRunner dialogueRunner;

        [Tooltip("이름 입력 UI 의 루트. 커맨드 수신 시 활성화, 확인 시 비활성화.")]
        [SerializeField] GameObject panelRoot;

        [Tooltip("이름을 입력받을 InputField.")]
        [SerializeField] TMP_InputField inputField;

        [Tooltip("확인 버튼. Enter 키(InputField.onSubmit)와 함께 동작.")]
        [SerializeField] Button confirmButton;

        // 커맨드 호출 시 새로 생성해 두었다가, 확인 시점에 TrySetResult 로 완료시킨다.
        // 한 번의 <<ask_player_name>> 대응이라 매번 새 인스턴스를 만드는 것이 안전 (재사용 X).
        YarnTaskCompletionSource _completionSource;

        void Awake()
        {
            if (dialogueRunner == null)
            {
                Debug.LogWarning("[NameInputView] dialogueRunner 참조가 비어 있습니다.");
                return;
            }

            // 커맨드는 Awake 에서 등록해야 DialogueRunner 가 노드를 실행하기 전에 준비된다.
            // Func<YarnTask> 시그니처: DialogueRunner 가 이 태스크의 완료를 기다린다.
            dialogueRunner.AddCommandHandler("ask_player_name", AskPlayerNameCommand);

            // 시작 시 패널은 숨긴 상태로 두어야 정상 대사 흐름을 가리지 않는다.
            if (panelRoot != null) panelRoot.SetActive(false);

            // 확인 트리거: 버튼 클릭 + Enter 키(InputField.onSubmit).
            // 두 경로 모두 같은 확정 로직을 타야 하므로 하나의 메서드로 모은다.
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
            if (inputField != null)
                inputField.onSubmit.AddListener(_ => OnConfirm());
        }

        void OnDestroy()
        {
            // 이벤트 누수 방지. RemoveCommandHandler 는 v3 에서 제공되나 씬 종료 시
            // DialogueRunner 자체가 파괴되므로 실질적으로 필수는 아니지만 명시적으로 짝을 맞춘다.
            if (dialogueRunner != null)
                dialogueRunner.RemoveCommandHandler("ask_player_name");

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnConfirm);
            // inputField.onSubmit 는 람다로 등록해서 개별 해제는 어렵지만,
            // InputField 자체가 씬과 함께 파괴되므로 누수 위험 없음.
        }

        // Yarn 이 이 태스크를 await 하는 동안 DialogueRunner 는 다음 라인을 진행하지 않는다.
        // 사용자가 유효 입력으로 확인을 누르면 _completionSource.TrySetResult() 가 호출되어
        // 태스크가 완료되고 대사 흐름이 재개된다.
        YarnTask AskPlayerNameCommand()
        {
            // 항상 새 CompletionSource 를 만들어야 이전 호출의 상태와 섞이지 않는다.
            _completionSource = new YarnTaskCompletionSource();

            if (panelRoot != null) panelRoot.SetActive(true);

            // 자동 검증 지원: 커맨드 수신 → 패널 활성화 흐름을 콘솔에서 확인.
            Debug.Log("[NameInputView] <<ask_player_name>> 수신 → NameInputPanel 활성화, 입력 대기");

            if (inputField != null)
            {
                // 이전 실행의 잔여 입력을 지운다. 이론상 첫 진입에서는 빈 문자열이지만
                // 방어적으로 초기화해 이후 재사용 시나리오에도 안전하게 대응.
                inputField.text = string.Empty;
                // 포커스를 자동으로 이동시켜 사용자가 바로 타이핑할 수 있게 한다.
                inputField.ActivateInputField();
            }

            return _completionSource.Task;
        }

        void OnConfirm()
        {
            if (_completionSource == null) return; // 커맨드가 아직 호출되지 않은 상태에서의 오작동 방지.
            if (inputField == null) return;

            // Trim 하는 이유: 앞뒤 공백만 입력하고 확인해도 "빈 이름" 으로 취급.
            // 게임 안에서 " " 같은 이름이 표시되는 것을 방지한다.
            string trimmed = inputField.text?.Trim() ?? string.Empty;

            // 빈 입력은 무시하고 다시 대기 (a안).
            // TrySetResult 를 호출하지 않으므로 DialogueRunner 는 계속 대기 상태.
            if (string.IsNullOrEmpty(trimmed))
            {
                // 사용자가 다시 타이핑할 수 있도록 포커스를 유지.
                inputField.ActivateInputField();
                return;
            }

            // 세이브와 Yarn 변수 양쪽에 동일 값을 심어둔다.
            // - 세이브: 이후 씬(Encounter 등)에서 참조.
            // - Yarn 변수: 프롤로그 뒷부분의 {$in_game_name} 치환용.
            SaveManager.Data.playerName = trimmed;
            SaveManager.Save();

            if (dialogueRunner != null && dialogueRunner.VariableStorage != null)
                dialogueRunner.VariableStorage.SetValue(InGameNameVariable, trimmed);

            if (panelRoot != null) panelRoot.SetActive(false);

            // 자동 검증 지원: 확정된 이름과 저장/변수 반영 결과를 콘솔에서 확인.
            Debug.Log($"[NameInputView] 이름 확정 → SaveManager.Data.playerName='{SaveManager.Data.playerName}', $in_game_name='{trimmed}'");

            // 태스크 완료 → DialogueRunner 가 다음 라인을 실행.
            _completionSource.TrySetResult();
            _completionSource = null;
        }
    }
}
