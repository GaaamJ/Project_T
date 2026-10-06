using System.Collections.Generic;
using UnityEngine;
using ProjectT.Save;

namespace ProjectT.Session
{
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        readonly HashSet<string> inputLockReasons = new HashSet<string>();

        public bool IsInputLocked => inputLockReasons.Count > 0;

        public void LockInput(string reason) => inputLockReasons.Add(reason);

        public void UnlockInput(string reason) => inputLockReasons.Remove(reason);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SaveManager.Load();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
