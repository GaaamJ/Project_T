using TMPro;
using UnityEngine;
using ProjectT.Thread;

namespace ProjectT.UI
{
    public class BuffTimerUI : MonoBehaviour
    {
        [SerializeField] ThreadBuffHolder buffHolder;
        [SerializeField] TextMeshProUGUI label;

        void Update()
        {
            if (buffHolder == null || label == null) return;

            if (!buffHolder.HasBuff)
            {
                label.gameObject.SetActive(false);
                return;
            }

            label.gameObject.SetActive(true);
            label.text = $"{buffHolder.CurrentType}: {buffHolder.RemainingTime:F1}";
        }
    }
}
