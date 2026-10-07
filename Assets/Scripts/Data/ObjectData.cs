using UnityEngine;

namespace ProjectT.Data
{
    public enum ObjectKind
    {
        Basic,
        Item
    }

    [CreateAssetMenu(fileName = "ObjectData", menuName = "ProjectT/Object Data")]
    public class ObjectData : ScriptableObject
    {
        [Tooltip("오브젝트 고유 이름. 이후 사망·퍼즐 데이터가 이 값으로 연결된다.")]
        [SerializeField] string id;

        [Tooltip("조사 결과 종류. 기본: 대사 / 아이템: 로그 출력만")]
        [SerializeField] ObjectKind kind;

        [Tooltip("첫 조사 때 재생할 Yarn 노드 이름. 기본은 필수, 아이템은 비워도 된다.")]
        [SerializeField] string dialogueNode;

        [Tooltip("다시 조사하면 재생할 짧은 대사 노드 이름. 아이템은 비워도 된다.")]
        [SerializeField] string recheckDialogueNode;

        public string Id => id;
        public ObjectKind Kind => kind;
        public string DialogueNode => dialogueNode;
        public string RecheckDialogueNode => recheckDialogueNode;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                Debug.LogWarning($"[ObjectData] '{name}': id가 비어 있다.", this);

            if (kind != ObjectKind.Basic)
                return;

            if (string.IsNullOrWhiteSpace(dialogueNode))
                Debug.LogWarning($"[ObjectData] '{name}': 기본 오브젝트인데 대사 노드가 비어 있다.", this);

            if (string.IsNullOrWhiteSpace(recheckDialogueNode))
                Debug.LogWarning($"[ObjectData] '{name}': 기본 오브젝트인데 재조사 대사 노드가 비어 있다.", this);
        }
#endif
    }
}
