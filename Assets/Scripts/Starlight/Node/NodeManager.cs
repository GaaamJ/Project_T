using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yarn.Unity;

[System.Serializable]
public struct NodeEdge
{
    public Node from;
    public Node to;
}

public class NodeManager : MonoBehaviour
{
    public static NodeManager Instance { get; private set; } // 프로토타입용 싱글톤 패턴

    [SerializeField] private LineRenderer linePrefab;
    [SerializeField] private List<NodeEdge> correctEdgeList; // 오벨리스크가 요구하는 정답 노드 집합

    private HashSet<(Node, Node)> correctEdges;
    private Node[] allNodes;
    private Node lastNode;
    private readonly HashSet<(Node, Node)> connectedEdges = new();
    private readonly List<LineRenderer> drawnLines = new();

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string symbolHintYarnNode;
    public static event Action<bool> OnSymbolSubmitted;

    void Awake()
    {
        Instance = this;

        // 씬에 존재하는 모든 Node를 찾아서 allNodes 배열에 저장, 추후 방 이동 시 갱신 필요할 듯?
        allNodes = FindObjectsByType<Node>(FindObjectsSortMode.None);
        // Inspector 리스트를 정규화된 HashSet으로 변환 (한 번만 계산)
        correctEdges = new HashSet<(Node, Node)>(
            correctEdgeList.Select(e => NormalizeEdge(e.from, e.to))
        );
    }

    void Start()
    {
        // !! TEST CODE !! 
        GiveSymbolHint();
    }

    public void RegisterNodeInteraction(Node node)
    {
        if (lastNode != null && lastNode != node)
        {
            DrawConnection(lastNode, node);
            connectedEdges.Add(NormalizeEdge(lastNode, node));
        }
        lastNode = node;
    }

    // A-B와 B-A를 같은 간선으로 취급하기 위해 정규화
    private (Node, Node) NormalizeEdge(Node a, Node b)
    {
        return a.GetInstanceID() < b.GetInstanceID() ? (a, b) : (b, a);
    }


    private void DrawConnection(Node a, Node b)
    {
        var line = Instantiate(linePrefab);
        line.positionCount = 2;
        line.SetPosition(0, a.transform.position);
        line.SetPosition(1, b.transform.position);
        drawnLines.Add(line);
    }

    // 오벨리스크가 호출할 예정, 정답 노드 집합과 현재 연결된 노드 집합을 비교하여 성공 여부 반환
    public bool SubmitSymbol()
    {
        bool success = connectedEdges.SetEquals(correctEdges);
        OnSymbolSubmitted?.Invoke(success);
        ResetChain();
        return success;
    }

    // 연결된 노드 집합과 그려진 선들을 초기화
    private void ResetChain()
    {
        lastNode = null;
        connectedEdges.Clear();   // ← connectedNodes 대신 connectedEdges
        foreach (var line in drawnLines) Destroy(line.gameObject);
        drawnLines.Clear();
        foreach (var node in allNodes) node.ResetNode();
    }

    [YarnCommand("GiveSymbolHint")]
    public void GiveSymbolHint()
    {
        if (string.IsNullOrEmpty(symbolHintYarnNode))
        {
            Debug.LogWarning("symbolHintYarnNode가 설정되지 않았습니다.");
            return;
        }
        dialogueRunner.StartDialogue(symbolHintYarnNode);
    }
}