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

        SpriteRenderer sr;

        public bool IsActive { get; private set; }
        public ThreadType BoundType { get; private set; }

        // 비활성 → 활성 전환 시에만 발행. 스테이지 시스템이 구독해 클리어 조건을 판단한다.
        public event Action<Coordinate> OnActivated;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
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

            sr.sprite = target;

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
    // 비활성 상태의 기본 스프라이트가 별도로 없으므로 null로 클리어.
    // 필요 시 인스펙터에 defaultSprite 필드를 추가해 회색 스프라이트로 교체할 수 있음.
    if (sr != null) sr.sprite = null;
}
}
}
