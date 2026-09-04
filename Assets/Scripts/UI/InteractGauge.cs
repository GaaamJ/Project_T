using UnityEngine;
using UnityEngine.UI;

namespace ProjectT.UI
{
    public class InteractGauge : MonoBehaviour
    {
        [SerializeField] Image fillImage;

        void Awake() => gameObject.SetActive(false);

        public void Show()
        {
            gameObject.SetActive(true);
            if (fillImage != null) fillImage.fillAmount = 0f;
        }

        public void SetProgress(float t)
        {
            if (fillImage != null) fillImage.fillAmount = t;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
