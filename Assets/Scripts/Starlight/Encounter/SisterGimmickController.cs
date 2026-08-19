using UnityEngine;
using Yarn.Unity;

// HR286 자매 기믹의 대사/시퀀스 흐름을 담당한다.
// 라운드 판정·타이머 등 게임 로직은 StarlightEncounterController가 처리하고,
// 여기서는 "언제 Observe/Bind를 호출할지"만 결정한다.
// 자동 시퀀서: BeginEncounter() → Observe() 대사 시작 → 대사 완료 → Bind() 자동 호출.
public class SisterGimmickController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private StarlightEncounterController encounterController;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Yarn Nodes")]
    // 관찰 단계에서 재생할 자매 대사 노드. 이 대사가 끝난 순간이 Bind() 호출 시점이다.
    [SerializeField] private string observeYarnNode;

    // 자동 시퀀서가 Bind를 기다리는 중인지 여부.
    // Yarn onDialogueComplete는 관찰 이외의 다른 대사 완료에도 호출될 수 있으므로
    // "이번 완료가 Observe 완료인가"를 구분할 플래그가 필요하다.
    private bool waitingForObserveComplete;

    private void OnEnable()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.AddListener(HandleDialogueComplete);
        }
    }

    private void OnDisable()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
        }
    }

    // 엔카운터 시작 트리거. 외부(트리거 존, 스테이지 매니저 등)에서 호출한다.
    // 자동 시퀀서 진입점: Observe → (대사 대기) → Bind 순으로 자동 진행된다.
    public void BeginEncounter()
    {
        Observe();
    }

    // 외부에서 직접 호출 가능한 public API (자매 AI 연동 대비, 기획서 Q1 답변).
    // 관찰 단계로 진입시키고 Yarn 대사를 시작한다. 대사 완료 이벤트에서 Bind가 이어진다.
    public void Observe()
    {
        if (encounterController != null)
        {
            encounterController.TriggerObserve();
        }

        if (dialogueRunner != null && !string.IsNullOrEmpty(observeYarnNode))
        {
            waitingForObserveComplete = true;
            dialogueRunner.StartDialogue(observeYarnNode);
        }
        else
        {
            // 대사 없이 바로 Bind로 넘어가는 예비 경로. (대사 세팅 전 개발 중 테스트용)
            Bind();
        }
    }

    // 외부에서 직접 호출 가능한 public API.
    // 정상 흐름에서는 HandleDialogueComplete에서 자동 호출되지만,
    // 자매 AI가 대사를 우회하고 즉시 Bind를 걸고 싶을 때도 사용 가능.
    public void Bind()
    {
        if (encounterController != null)
        {
            encounterController.TriggerBind();
        }
    }

    // Yarn 대사 완료 콜백. Observe 완료를 감지해 자동으로 Bind를 호출한다.
    private void HandleDialogueComplete()
    {
        if (!waitingForObserveComplete) return;
        waitingForObserveComplete = false;
        Bind();
    }
}
