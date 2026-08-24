using UnityEngine;

// Thread 자매 기믹의 라운드 진행/판정을 담당하는 컨트롤러.
// Yarn 대사/자매 AI 흐름은 SisterGimmickController가 담당하고,
// 순수 게임 로직(노드 상태, 라운드 전환, 타이머, 라인)은 이 스크립트가 담당한다.
public class StarlightEncounterController : MonoBehaviour
{
    // Inspector에서 라운드 데이터를 직접 편집할 수 있도록 public 필드로 노출.
    // (기획서 Q6 답변)
    [System.Serializable]
    public struct RoundData
    {
        public Node[] candidates;   // 라운드당 후보 노드 (기본 3개)
        public Node correctNode;    // candidates 중 하나여야 함
    }

    // 엔카운터의 진행 단계.
    // Idle: 관찰 이전 (Dim 상태)
    // Observing: Observe() 호출 후, 아직 Bind 이전
    // Bound: Bind() 호출 후 타이머 카운트다운 및 입력 대기
    // Resolved: 모든 라운드 완료
    public enum EncounterState { Idle, Observing, Bound, Resolved }

    [Header("Rounds")]
    [SerializeField] private RoundData[] rounds; // Inspector에서 4개 설정

    [Header("Timer")]
    [SerializeField] private float timerDuration = 10f;

    [Header("Line")]
    [SerializeField] private LineRenderer linePrefab;

    private int currentRound;
    // 첫 라운드에서는 앵커가 없어 라인이 그려지지 않는다. 정답 노드가 다음 라운드의 앵커가 된다.
    private Node anchorNode;
    private EncounterState state = EncounterState.Idle;
    private float remainingTime;

    // 라운드가 넘어갈 때 발생. SisterGimmickController가 구독해서 Observe()를 재실행한다.
    public event System.Action OnRoundStarted;

    public float RemainingTime => remainingTime;
    public EncounterState State => state;
    public int CurrentRound => currentRound;

    private void Awake()
    {
        // 모든 라운드의 후보 노드에게 컨트롤러를 주입하고, 초기 상태를 Dim으로 만든다.
        // 노드는 여러 라운드에서 중복 등장할 수 있으니 SetController가 여러 번 호출돼도 안전해야 한다.
        for (int i = 0; i < rounds.Length; i++)
        {
            var round = rounds[i];

            // correctNode가 candidates에 실제 포함돼 있는지 검증 (기획서 Q7 답변).
            // Inspector에서 실수로 다른 노드를 지정한 경우 즉시 알려야 디버깅이 쉬워진다.
            bool correctInCandidates = false;
            if (round.candidates != null)
            {
                for (int j = 0; j < round.candidates.Length; j++)
                {
                    var node = round.candidates[j];
                    if (node == null) continue;
                    node.SetController(this);
                    node.SetState(Node.NodeState.Dim);
                    if (node == round.correctNode) correctInCandidates = true;
                }
            }

            if (round.correctNode == null || !correctInCandidates)
            {
                Debug.LogError($"[StarlightEncounterController] Round {i}: correctNode가 candidates 배열에 없습니다.", this);
            }
        }
    }

    private void Update()
    {
        // 타이머는 Bound 상태(입력 대기)에서만 흐른다. Observing/Idle/Resolved에서는 정지.
        if (state != EncounterState.Bound) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            Debug.Log("Fail");
            ResetRound();
        }
    }

    // SisterGimmickController(또는 향후 자매 AI)에서 호출.
    // 관찰 단계: 노드는 아직 Dim 상태이며 타이머도 흐르지 않는다.
    public void TriggerObserve()
    {
        if (state == EncounterState.Resolved) return;
        state = EncounterState.Observing;
    }

    // Observe 대사 완료 후 호출. 정답 노드를 Highlighted로 바꾸고 타이머 시작.
    public void TriggerBind()
    {
        if (state == EncounterState.Resolved) return;
        if (currentRound >= rounds.Length) return;

        var round = rounds[currentRound];
        if (round.correctNode != null)
        {
            round.correctNode.SetState(Node.NodeState.Highlighted);
        }

        remainingTime = timerDuration;
        state = EncounterState.Bound;
    }

    // Node.Interact() → myController.TryConnect(this) 경로로 들어온다.
    public void TryConnect(Node node)
    {
        // 아직 Bind 이전이거나 이미 종결된 상태라면 입력을 무시.
        // (Observing 단계에서 노드를 눌러도 반응이 없어야 관찰-바인딩 순서가 강제된다.)
        if (state != EncounterState.Bound) return;
        if (currentRound >= rounds.Length) return;

        var round = rounds[currentRound];

        // 현재 라운드 후보가 아닌 노드(이전 라운드 잔여 노드 등)는 무시한다.
        if (!System.Array.Exists(round.candidates, c => c == node)) return;

        if (node == round.correctNode)
        {
            // 정답 처리: 노드를 Connected로 바꾸고 앵커와 라인을 그린다.
            node.SetState(Node.NodeState.Connected);

            // 첫 라운드에는 앵커가 없어 라인을 그리지 않는다. 정답 노드가 앵커가 되어 다음 라운드로 연결된다.
            if (anchorNode != null && linePrefab != null)
            {
                DrawLine(anchorNode, node);
            }

            anchorNode = node;
            currentRound++;

            if (currentRound >= rounds.Length)
            {
                Debug.Log("Success");
                state = EncounterState.Resolved;
                return;
            }

            // 다음 라운드는 Observe부터 다시 시작해야 하므로 Idle로 복귀 후 이벤트 발생.
            // 타이머와 하이라이트는 TriggerBind()에서 처리한다 (SisterGimmickController 경유).
            state = EncounterState.Idle;
            OnRoundStarted?.Invoke();
        }
        else
        {
            // 오답: 라운드 리셋. Fail 로그만 남기고 스테이지 재시작은 이번 스코프 외 (기획서 Q2).
            Debug.Log("Fail");
            ResetRound();
        }
    }

    // 현재 라운드의 후보 노드를 모두 Dim으로 되돌리고 타이머를 초기화.
    // 이미 Connected된 이전 라운드의 노드까지 되돌리진 않는다 (라운드 단위 리셋).
    private void ResetRound()
    {
        if (currentRound < rounds.Length)
        {
            var round = rounds[currentRound];
            if (round.candidates != null)
            {
                for (int i = 0; i < round.candidates.Length; i++)
                {
                    var node = round.candidates[i];
                    if (node == null) continue;
                    // 이미 Connected된 이전 라운드의 앵커/정답 노드는 유지해야 하므로 앵커는 제외.
                    if (node == anchorNode) continue;
                    node.SetState(Node.NodeState.Dim);
                }
            }
        }

        remainingTime = 0f;
        state = EncounterState.Idle;
    }

    private void DrawLine(Node a, Node b)
    {
        var line = Instantiate(linePrefab);
        line.positionCount = 2;
        line.SetPosition(0, a.transform.position);
        line.SetPosition(1, b.transform.position);
    }
}
