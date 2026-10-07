using System.Collections.Generic;
using UnityEngine;
using ProjectT.Interaction;

namespace ProjectT.Player
{
    // 라리스의 자식(로컬 위치 0)에 붙인다. 부모의 Rigidbody2D로 트리거가 동작하므로 Rigidbody2D는 붙이지 않는다.
    [RequireComponent(typeof(CircleCollider2D))]
    public class DetectZone : MonoBehaviour
    {
        [SerializeField] InteractionSettings settings;
        [SerializeField] LayerMask interactableMask;

        // 경계값(정확히 거리·각도 끝)을 포함시키기 위한 부동소수 오차 허용치
        const float Epsilon = 1e-4f;
        // 트리거 원이 경계에 딱 닿은 대상을 후보에서 놓치지 않도록 조금 크게 잡고, 실제 판정은 FindBest가 한다.
        const float TriggerMargin = 0.1f;

        struct Candidate
        {
            public Collider2D collider;
            public IInteractable interactable;
        }

        readonly List<Candidate> candidates = new List<Candidate>();
        PlayerMovement movement;
        IInteractable highlighted;

        public IInteractable CurrentTarget { get; private set; }

        void Reset()
        {
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        void Awake()
        {
            movement = GetComponentInParent<PlayerMovement>();

            var circle = GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.offset = Vector2.zero;
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            circle.radius = (settings.Distance + TriggerMargin) / (maxScale > 0f ? maxScale : 1f);

            if (transform.localPosition != Vector3.zero)
                Debug.LogWarning("[DetectZone] 라리스 원점 기준으로 판정하므로 로컬 위치를 0으로 두어야 한다.", this);
        }

        void OnDisable()
        {
            SetHighlighted(null);
            CurrentTarget = null;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if ((interactableMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            IInteractable interactable = other.GetComponentInParent<IInteractable>();
            if (interactable == null)
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].collider == other)
                    return;
            }

            candidates.Add(new Candidate { collider = other, interactable = interactable });
        }

        void OnTriggerExit2D(Collider2D other)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (candidates[i].collider == other)
                    candidates.RemoveAt(i);
            }
        }

        void Update()
        {
            CurrentTarget = FindBest();
            SetHighlighted(CurrentTarget);
        }

        IInteractable FindBest()
        {
            Vector2 origin = movement.transform.position;
            Vector2 facing = ((Vector2)movement.Facing).normalized;

            IInteractable best = null;
            float bestDistance = 0f;
            float bestAngle = 0f;
            int bestId = 0;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                Candidate candidate = candidates[i];
                if (candidate.collider == null || !IsAlive(candidate.interactable))
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (!candidate.collider.isActiveAndEnabled || !candidate.interactable.CanInteract)
                    continue;

                Vector2 toPoint = candidate.collider.ClosestPoint(origin) - origin;
                float distance = toPoint.magnitude;
                if (distance > settings.Distance + Epsilon)
                    continue;

                float angle = distance <= Epsilon ? 0f : Vector2.Angle(facing, toPoint);
                if (angle > settings.HalfAngle + Epsilon)
                    continue;

                int id = ((Object)candidate.interactable).GetInstanceID();
                if (best != null && !IsBetter(distance, angle, id, bestDistance, bestAngle, bestId))
                    continue;

                best = candidate.interactable;
                bestDistance = distance;
                bestAngle = angle;
                bestId = id;
            }

            return best;
        }

        static bool IsBetter(float distance, float angle, int id, float bestDistance, float bestAngle, int bestId)
        {
            if (Mathf.Abs(distance - bestDistance) > Epsilon)
                return distance < bestDistance;
            if (Mathf.Abs(angle - bestAngle) > Epsilon)
                return angle < bestAngle;
            return id < bestId;
        }

        void SetHighlighted(IInteractable target)
        {
            if (ReferenceEquals(target, highlighted))
                return;

            if (IsAlive(highlighted))
                highlighted.HideHighlight();

            highlighted = target;

            if (target != null)
                target.ShowHighlight(settings.HighlightColor);
        }

        public static bool IsAlive(IInteractable interactable)
        {
            return interactable is Object unityObject ? unityObject != null : interactable != null;
        }
    }
}
