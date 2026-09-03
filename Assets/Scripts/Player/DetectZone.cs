using UnityEngine;

namespace ProjectT.Player
{
    // 플레이어의 자식 오브젝트에 부착. Trigger Collider2D 로 Coordinate 진입/이탈을 감지한다.
    // Rigidbody2D는 붙이지 않는다 — 부모 플레이어의 Rigidbody2D 를 물리적으로 상속받아 트리거가 동작함.
    // 단일 참조 유지 정책: "나중에 들어온" Coordinate 를 우선한다.
    //   - 두 Coordinate 가 겹친 상황에서 새로 진입한 쪽으로 즉시 교체.
    //   - 현재 참조 중인 Coordinate 가 이탈하면 참조 해제(재감지는 다음 진입까지 대기).
    //   - 다른(참조되지 않은) Coordinate 가 이탈하는 것은 무시 — "이전에 밀린" 대상까지 되살릴 필요가 없기 때문.
    // PlayerInteract 는 Interact 입력이 들어온 시점에만 이 참조를 읽어 상호작용을 시도한다.
    [RequireComponent(typeof(Collider2D))]
    public class DetectZone : MonoBehaviour
    {
        // Interact 입력 시점에 PlayerInteract 가 참조하는 "현재 감지된 Coordinate".
        // 없을 때 null. 외부에서는 읽기 전용.
        public Coordinate.Coordinate CurrentTarget { get; private set; }

        void Reset()
        {
            // 편집기에서 컴포넌트 부착 시 자동으로 Trigger 로 세팅 — 실수로 non-trigger 로 두어
            // 물리 충돌이 발생하는 것을 방지.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // 자식/루트 어디에 Coordinate 가 붙어 있어도 찾을 수 있게 GetComponentInParent 사용.
            var coord = other.GetComponentInParent<Coordinate.Coordinate>();
            if (coord == null) return;

            // "나중에 진입한 것으로 교체" 정책 — 이전 참조가 있어도 무조건 새 대상으로 교체.
            CurrentTarget = coord;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var coord = other.GetComponentInParent<Coordinate.Coordinate>();
            if (coord == null) return;

            // 현재 참조 중인 대상이 이탈한 경우에만 참조 해제.
            // 다른(밀려나 있던) Coordinate 이탈은 무시 — 참조 상태에 영향을 주지 않음.
            if (coord == CurrentTarget)
                CurrentTarget = null;
        }
    }
}
