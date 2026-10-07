using UnityEngine;

namespace ProjectT.Interaction
{
    [CreateAssetMenu(fileName = "InteractionSettings", menuName = "ProjectT/Interaction Settings")]
    public class InteractionSettings : ScriptableObject
    {
        [Tooltip("라리스 원점에서 대상 콜라이더의 가장 가까운 점까지의 최대 거리")]
        [SerializeField, Min(0.01f)] float distance = 1.5f;

        [Tooltip("바라보는 방향 기준 좌우 허용 각도(도). 45면 ±45도")]
        [SerializeField, Range(0f, 180f)] float halfAngle = 45f;

        [Tooltip("임시 하이라이트 색. 하이라이트 중에는 스프라이트 색을 이 색으로 바꾼다.")]
        [SerializeField] Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        public float Distance => distance;
        public float HalfAngle => halfAngle;
        public Color HighlightColor => highlightColor;
    }
}
