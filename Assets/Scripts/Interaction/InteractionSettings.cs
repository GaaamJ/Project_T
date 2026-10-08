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

        public float Distance => distance;
        public float HalfAngle => halfAngle;
    }
}
