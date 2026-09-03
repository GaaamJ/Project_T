using System;
using UnityEngine;

namespace ProjectT.Thread
{
    // 플레이어에 붙어서 현재 보유 중인 실 버프의 종류와 남은 시간을 관리한다.
    // 스폰 시스템은 OnBuffConsumed/OnBuffExpired 이벤트로 "버프 슬롯이 비었음"을 알 수 있다.
    // 교체(Acquire 중 이미 보유 상태)는 소모도 만료도 아니므로 이벤트를 발행하지 않는다 — 기획 확정 사항.
    public class ThreadBuffHolder : MonoBehaviour
    {
        // 모든 실 타입에 공통 적용되는 지속시간. 개별 타입별 duration은 현재 스코프 외.
        [SerializeField] private float buffDuration = 20f;

        public ThreadType CurrentType { get; private set; }
        public float RemainingTime { get; private set; }

        // HasBuff가 유일한 유효성 판단 기준. CurrentType은 HasBuff=false일 때 의미 없음(리셋하지 않음).
        public bool HasBuff => RemainingTime > 0f;

        // 파라미터는 "방금 소모/만료된 실의 타입" — 구독자가 리셋 이전 값에 접근할 수 있게 한다.
        public event Action<ThreadType> OnBuffConsumed;
        public event Action<ThreadType> OnBuffExpired;

        public void Acquire(ThreadType type)
        {
            // 교체 시에도 이전 이벤트 발행 없이 조용히 덮어쓴다.
            CurrentType = type;
            RemainingTime = buffDuration;
        }

        public void Consume()
        {
            // 없는 버프를 소모 요청하는 것은 no-op. 이벤트도 발행하지 않는다.
            if (!HasBuff) return;

            // 이벤트를 먼저 발행해 구독자가 CurrentType을 조회할 수 있게 한 뒤 상태를 리셋한다.
            var consumed = CurrentType;
            OnBuffConsumed?.Invoke(consumed);
            RemainingTime = 0f;
        }

        private void Update()
        {
            // 실제 감산/만료 로직은 Tick으로 분리 — EditMode 테스트에서 Time.deltaTime=0인 프레임에도
            // 임의의 dt를 주입해 결정적으로 검증하기 위함.
            Tick(Time.deltaTime);
        }

        // 테스트/디버그 목적으로 dt를 직접 주입할 수 있도록 internal로 공개.
        // 런타임에서는 Update가 자동으로 호출하므로 게임 코드에서 직접 부를 필요는 없다.
        internal void Tick(float deltaTime)
        {
            // 버프가 없을 때는 타이머를 굴리지 않는다 — 이벤트 중복 발행 방지 겸 불필요한 감산 방지.
            if (!HasBuff) return;

            RemainingTime -= deltaTime;
            if (RemainingTime <= 0f)
            {
                // Consume과 마찬가지로 이벤트 발행 → 상태 리셋 순서.
                var expired = CurrentType;
                OnBuffExpired?.Invoke(expired);
                RemainingTime = 0f;
            }
        }
    }
}
