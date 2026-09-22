using System;
using UnityEngine;
using ProjectT.Thread;

namespace ProjectT.Coordinate
{
    // SpriteRenderer는 스프라이트 교체를 위해 필수. 누락 시 컴포넌트 자동 부착으로 런타임 에러 방지.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Coordinate : MonoBehaviour
    {
        [SerializeField] Sprite redSprite;
        [SerializeField] Sprite blueSprite;
        [SerializeField] Sprite goldSprite;

        SpriteRenderer _sr;

        // 인스펙터에서 지정한 비활성 외형 스프라이트를 캐시.
        // Reset() 시 null로 밀어버리면 리트라이 후 좌표가 화면에서 사라지는 버그가 생겨서
        // Awake 시점에 초기값을 붙잡아 뒀다가 리셋에서 복원한다.
        Sprite _initialSprite;

        public bool IsActive { get; private set; }
        public ThreadType BoundType { get; private set; }

        // 비활성 → 활성 전환 시에만 발행. 스테이지 시스템이 구독해 클리어 조건을 판단한다.
        public event Action<Coordinate> OnActivated;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _initialSprite = _sr.sprite;
        }

        // DetectZone이 범위 안의 홀더를 감지했을 때 호출.
        public bool TryBind(ThreadBuffHolder holder)
        {
            if (!holder.HasBuff) return false;

            ThreadType newType = holder.CurrentType;
            holder.Consume();

            bool firstActivation = !IsActive;
            IsActive = true;
            BoundType = newType;

            // 재바인딩 시에도 타입이 바뀌면 스프라이트를 갱신해야 하므로 firstActivation 체크 밖에서 교체한다.
            Sprite target = newType switch
            {
                ThreadType.Blue => blueSprite,
                ThreadType.Gold => goldSprite,
                // 미지정 타입은 Red로 폴백 — 기본 색이자 가장 흔한 케이스로 처리.
                _               => redSprite,
            };

            if (target == null)
                Debug.LogWarning($"[Coordinate] {newType} 스프라이트가 할당되지 않았습니다.");

            _sr.sprite = target;

            if (firstActivation)
                OnActivated?.Invoke(this);

            return true;
        }

        // StageManager가 리트라이 시 호출. 좌표를 비활성 상태로 되돌리고 스프라이트도 초기화한다.
        // 이벤트를 발행하지 않는 이유: 리셋 자체는 게임플레이 반응이 아니라 상태 초기화이므로
        // OnActivated의 반대 개념(OnDeactivated)을 만들지 않고 단순 상태 리셋만 수행.
        public void Reset()
        {
            IsActive = false;
            // Awake에서 캐시한 초기(비활성) 스프라이트로 복원.
            // null로 밀면 리트라이 후 좌표가 씬에서 보이지 않게 되므로 반드시 캐시값을 사용한다.
            if (_sr != null) _sr.sprite = _initialSprite;
        }
    }
}
