using System.Collections.Generic;
using UnityEngine;
using ProjectT.Save;

namespace ProjectT.Session
{
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        readonly HashSet<InputLockReason> inputLockReasons = new HashSet<InputLockReason>();
        readonly HashSet<string> investigatedObjectIds = new HashSet<string>();

        public bool IsInputLocked => inputLockReasons.Count > 0;

        public void LockInput(InputLockReason reason) => inputLockReasons.Add(reason);

        public void UnlockInput(InputLockReason reason) => inputLockReasons.Remove(reason);

        public bool HasInvestigated(string objectId) => investigatedObjectIds.Contains(objectId);

        public void MarkInvestigated(string objectId) => investigatedObjectIds.Add(objectId);

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
