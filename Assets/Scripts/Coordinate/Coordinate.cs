using System;
using UnityEngine;
using ProjectT.Thread;

namespace ProjectT.Coordinate
{
    public class Coordinate : MonoBehaviour
    {
        public bool IsActive { get; private set; }
        public ThreadType BoundType { get; private set; }

        // 비활성 → 활성 전환 시에만 발행. 스테이지 시스템이 구독해 클리어 조건을 판단한다.
        public event Action<Coordinate> OnActivated;

        // DetectZone이 범위 안의 홀더를 감지했을 때 호출.
        public bool TryBind(ThreadBuffHolder holder)
        {
            if (!holder.HasBuff) return false;

            ThreadType newType = holder.CurrentType;
            holder.Consume();

            bool firstActivation = !IsActive;
            IsActive = true;
            BoundType = newType;

            if (firstActivation)
                OnActivated?.Invoke(this);

            return true;
        }
    }
}
