using UnityEngine;
using UnityEngine.UI;
public class GamePlayPanel : MonoBehaviour
{
    [SerializeField] private Button settingBtn;

    private void Start() {
        settingBtn.onClick.AddListener(OnSettingButtonClicked);
    }

    private void OnSettingButtonClicked() {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowSettingPanel();
            AudioManager.Instance.PlayPopupSound();
        }
    }
}
