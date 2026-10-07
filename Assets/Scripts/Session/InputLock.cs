using System.Collections.Generic;
using UnityEngine;

namespace ProjectT.Session
{
    public class InputLock : MonoBehaviour
    {
        readonly Dictionary<InputLockReason, int> counts = new Dictionary<InputLockReason, int>();
        int total;

        public bool IsLocked => total > 0;

        public void Lock(InputLockReason reason)
        {
            counts.TryGetValue(reason, out int count);
            counts[reason] = count + 1;
            total++;
        }

        public void Unlock(InputLockReason reason)
        {
            counts.TryGetValue(reason, out int count);
            if (count == 0)
            {
                Debug.LogError($"[InputLock] 걸지 않은 잠금 사유({reason})를 풀려고 했다.", this);
                return;
            }

            counts[reason] = count - 1;
            total--;
        }
    }
}
