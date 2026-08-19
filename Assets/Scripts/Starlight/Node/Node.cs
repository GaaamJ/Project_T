using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Node : MonoBehaviour, IDelayInteractable
{
    // 자매 기믹(HR286)에서 노드의 시각 상태를 3단계로 구분한다.
    // Dim: Observe 이전 또는 라운드 리셋 시 (alpha 0.3)
    // Highlighted: Bind 이후 정답 노드가 강조되는 상태 (alpha 1.0)
    // Connected: 정답 노드가 앵커와 연결된 이후 상태 (alpha 1.0)
    public enum NodeState { Dim, Highlighted, Connected }

    [SerializeField] private float holdDuration = .6f;
    public float HoldDuration => holdDuration;

    public bool IsSelected { get; private set; }

    private SpriteRenderer sR;
    private NodeManager myManager; // 싱글톤 대신 직접 참조 (별빛 #5)

    // 자매 기믹 컨트롤러 참조. myManager와 동시에 존재할 수 있으며 서로 다른 시나리오에서 쓰인다.
    // (기존 오벨리스크 기믹은 myManager, 자매 기믹은 myController)
    private StarlightEncounterController myController;

    private void Awake()
    {
        sR = GetComponent<SpriteRenderer>();
    }

    // NodeManager가 Awake 시점에 자식 Node들에게 호출해줌
    public void SetManager(NodeManager manager)
    {
        myManager = manager;
    }

    // StarlightEncounterController가 Awake 시점에 라운드 후보 노드들에게 호출해줌
    public void SetController(StarlightEncounterController controller)
    {
        myController = controller;
    }

    public void Interact()
    {
        // Null guard: 노드가 어느 시스템에도 등록되지 않은 채로 상호작용되면 조용히 무시한다.
        // (예: 씬 배치 실수, 라운드 리셋 중 순간적 상태)
        myController?.TryConnect(this);
        myManager?.TryRegisterNode(this);
    }

    public void ResetNode()
    {
        Deselect();
    }

    public void Select()
    {
        IsSelected = true;
        sR.flipY = true;
    }

    public void Deselect()
    {
        IsSelected = false;
        sR.flipY = false;
    }

    // 자매 기믹에서 사용하는 시각 상태 전환. 색상 자체는 유지하고 알파만 조절해서
    // 스프라이트 원본을 그대로 두면서 "관찰 전/후" 대비를 만든다.
    public void SetState(NodeState newState)
    {
        Color c = sR.color;
        c.a = newState == NodeState.Dim ? 0.3f : 1.0f;
        sR.color = c;
    }
}
