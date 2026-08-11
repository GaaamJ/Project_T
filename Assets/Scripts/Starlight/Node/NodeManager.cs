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
    [SerializeField] private LineRenderer linePrefab;
    [SerializeField] private List<NodeEdge> correctEdgeList; // 오벨리스크가 요구하는 정답 노드 집합

    private HashSet<(Node, Node)> correctEdges;
    private Node[] allNodes;
    private Node lastNode;
    private readonly HashSet<(Node, Node)> connectedEdges = new();
    private readonly List<LineRenderer> drawnLines = new();

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string symbolHintYarnNode;

    // static 제거 (별빛 #5): 여러 NodeManager가 씬에 공존해야 하는데,
    // static이면 전부 같은 이벤트를 공유해서 서로 다른 노드 집합의 결과가 섞여버림
    public event Action<bool> OnSymbolSubmitted;

    void Awake()
    {
        // FindObjectsByType 대신 자식만 스캔 (별빛 #5):
        // 씬 전체를 훑으면 다른 NodeManager의 노드까지 관리 대상에 섞여 들어감
        allNodes = GetComponentsInChildren<Node>();
        // 각 Node에게 "네 매니저는 나"라고 알려줌 (Node는 자기 매니저를 스스로 모름)
        foreach (var node in allNodes)
        {
            node.SetManager(this);
        }

        var obelisk = GetComponentInChildren<Obelisk>();
        if (obelisk != null)
        {
            obelisk.SetManager(this);
        }

        correctEdges = new HashSet<(Node, Node)>(
            correctEdgeList.Select(e => NormalizeEdge(e.from, e.to))
        );
    }

    public void TryRegisterNode(Node node)
    {
        if (lastNode == null)
        {
            lastNode = node;
            lastNode.Select();
            return;
        }

        if (lastNode == node)
        {
            lastNode.Deselect();
            lastNode = null;
            return;
        }

        var edge = NormalizeEdge(lastNode, node);

        if (connectedEdges.Contains(edge))
        {
            Debug.Log("already connected");
            lastNode.Deselect();
            lastNode = null;
            return;
        }

        DrawConnection(lastNode, node);
        connectedEdges.Add(edge);
        lastNode.Deselect();
        lastNode = null;
    }

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

    public bool SubmitSymbol()
    {
        bool success = connectedEdges.SetEquals(correctEdges);
        OnSymbolSubmitted?.Invoke(success);
        ResetChain();
        return success;
    }

    private void ResetChain()
    {
        lastNode = null;
        connectedEdges.Clear();
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