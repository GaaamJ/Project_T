using NUnit.Framework;
using ProjectT.Thread;
using UnityEngine;

namespace ProjectT.Tests.Editor
{
    // EditMode 단위 테스트. Play 없이 컴포넌트 로직만 검증한다.
    // GameObject.AddComponent로 인스턴스를 만들지만, 사용자 규칙(삭제는 항상 승인 필요)에 따라
    // 명시적으로 DestroyImmediate하지 않는다. hideFlags=HideAndDontSave로 두면 씬에 저장되지 않고,
    // 테스트 러너 세션 종료 시 정리된다.
    public class ThreadBuffHolderTests
    {
        private ThreadBuffHolder CreateHolder()
        {
            var go = new GameObject("TestThreadBuffHolder_" + System.Guid.NewGuid().ToString("N"));
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<ThreadBuffHolder>();
        }

        [Test]
        public void Acquire_SetsHasBuffAndRemainingTimeToDuration()
        {
            var holder = CreateHolder();
            // buffDuration 기본값(20f)에 의존 — 인스펙터 노출 필드지만 테스트에서는 default를 사용한다.
            holder.Acquire(ThreadType.Red);

            Assert.IsTrue(holder.HasBuff, "Acquire 직후 HasBuff는 true여야 한다.");
            Assert.AreEqual(20f, holder.RemainingTime, 0.0001f,
                "Acquire 직후 RemainingTime은 buffDuration과 같아야 한다.");
            Assert.AreEqual(ThreadType.Red, holder.CurrentType);
        }

        [Test]
        public void Consume_FiresOnBuffConsumedAndClearsHasBuff()
        {
            var holder = CreateHolder();
            holder.Acquire(ThreadType.Blue);

            ThreadType? consumedType = null;
            int consumedCount = 0;
            holder.OnBuffConsumed += t =>
            {
                consumedCount++;
                consumedType = t;
            };

            holder.Consume();

            Assert.AreEqual(1, consumedCount, "OnBuffConsumed는 정확히 1번 발행되어야 한다.");
            Assert.AreEqual(ThreadType.Blue, consumedType,
                "이벤트 파라미터는 소모된 타입(Blue)이어야 한다.");
            Assert.IsFalse(holder.HasBuff, "Consume 후 HasBuff는 false여야 한다.");
        }

        [Test]
        public void Tick_WhenRemainingTimeReachesZero_FiresOnBuffExpiredOnceAndSuppressesDuplicates()
        {
            var holder = CreateHolder();
            holder.Acquire(ThreadType.Gold);

            int expiredCount = 0;
            ThreadType? expiredType = null;
            holder.OnBuffExpired += t =>
            {
                expiredCount++;
                expiredType = t;
            };

            // buffDuration(20f)을 확실히 초과하는 dt를 한 번에 넣어 만료를 강제.
            holder.Tick(999f);
            // 이미 HasBuff=false 상태 — 두 번째 Tick에서 이벤트가 다시 발행되지 않아야 한다.
            holder.Tick(999f);

            Assert.AreEqual(1, expiredCount, "OnBuffExpired는 정확히 1번만 발행되어야 한다(중복 방지).");
            Assert.AreEqual(ThreadType.Gold, expiredType,
                "이벤트 파라미터는 만료된 타입(Gold)이어야 한다.");
            Assert.IsFalse(holder.HasBuff);
            Assert.AreEqual(0f, holder.RemainingTime, 0.0001f,
                "만료 후 RemainingTime은 정확히 0으로 리셋되어야 한다(음수 누적 방지).");
        }

        [Test]
        public void Consume_CallbackObservesResetState()
        {
            // "상태 먼저 리셋 → 이벤트 발행" 순서 보장 —
            // 콜백에서 HasBuff/RemainingTime을 조회하면 이미 리셋된 값이 보여야 한다.
            var holder = CreateHolder();
            holder.Acquire(ThreadType.Blue);

            bool hasBuffInCallback = true;
            float remainingInCallback = -1f;
            holder.OnBuffConsumed += _ =>
            {
                hasBuffInCallback = holder.HasBuff;
                remainingInCallback = holder.RemainingTime;
            };

            holder.Consume();

            Assert.IsFalse(hasBuffInCallback,
                "OnBuffConsumed 콜백 시점에는 HasBuff가 이미 false여야 한다.");
            Assert.AreEqual(0f, remainingInCallback, 0.0001f,
                "OnBuffConsumed 콜백 시점에는 RemainingTime이 이미 0이어야 한다.");
        }

        [Test]
        public void Tick_ExpiredCallbackObservesResetState()
        {
            // Tick 만료 경로도 동일하게 "상태 먼저 리셋 → 이벤트 발행" 순서여야 한다.
            var holder = CreateHolder();
            holder.Acquire(ThreadType.Gold);

            bool hasBuffInCallback = true;
            float remainingInCallback = -1f;
            holder.OnBuffExpired += _ =>
            {
                hasBuffInCallback = holder.HasBuff;
                remainingInCallback = holder.RemainingTime;
            };

            holder.Tick(999f);

            Assert.IsFalse(hasBuffInCallback,
                "OnBuffExpired 콜백 시점에는 HasBuff가 이미 false여야 한다.");
            Assert.AreEqual(0f, remainingInCallback, 0.0001f,
                "OnBuffExpired 콜백 시점에는 RemainingTime이 이미 0이어야 한다.");
        }

        [Test]
        public void Consume_CallbackCanReacquireImmediately()
        {
            // 콜백에서 즉시 Acquire를 호출해도 재진입성 문제가 없어야 한다 —
            // 이 시나리오가 사용자가 지적한 원래 버그였다.
            var holder = CreateHolder();
            holder.Acquire(ThreadType.Red);

            holder.OnBuffConsumed += _ =>
            {
                // 즉시 다른 타입 재획득 — 이전 순서(이벤트 먼저)라면 이 호출 후
                // RemainingTime=0f 대입이 뒤늦게 실행되어 새 버프가 즉시 클리어됐음.
                holder.Acquire(ThreadType.Blue);
            };

            holder.Consume();

            Assert.IsTrue(holder.HasBuff,
                "콜백에서 Acquire 재호출 시 새 버프가 유지되어야 한다.");
            Assert.AreEqual(ThreadType.Blue, holder.CurrentType);
            Assert.AreEqual(20f, holder.RemainingTime, 0.0001f);
        }

        [Test]
        public void Acquire_WhenReplacingExistingBuff_DoesNotFireAnyEvent()
        {
            var holder = CreateHolder();
            holder.Acquire(ThreadType.Red);

            int consumedCount = 0;
            int expiredCount = 0;
            holder.OnBuffConsumed += _ => consumedCount++;
            holder.OnBuffExpired += _ => expiredCount++;

            // 교체는 소모도 만료도 아니므로 어떤 이벤트도 발행되면 안 된다(Q1 확정 사항).
            holder.Acquire(ThreadType.Blue);

            Assert.AreEqual(0, consumedCount, "교체 시 OnBuffConsumed가 발행되면 안 된다.");
            Assert.AreEqual(0, expiredCount, "교체 시 OnBuffExpired가 발행되면 안 된다.");
            Assert.AreEqual(ThreadType.Blue, holder.CurrentType, "CurrentType은 새 타입으로 갱신되어야 한다.");
            Assert.AreEqual(20f, holder.RemainingTime, 0.0001f, "RemainingTime은 buffDuration으로 리셋되어야 한다.");
        }
    }
}
