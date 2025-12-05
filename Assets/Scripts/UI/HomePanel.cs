using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class HomePanel : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private float scaleDuration = 0.5f; // Thời gian mỗi lần phóng to/thu nhỏ
    [SerializeField] private float scaleAmount = 0.1f; // Độ phóng to/thu nhỏ (10%)
    
    private Tween scaleTween;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartButtonClick);
        StartScaleAnimation();
    }
    
    private void StartScaleAnimation()
    {
        if (startButton == null) return;
        
        RectTransform rectTransform = startButton.GetComponent<RectTransform>();
        if (rectTransform == null) return;
        
        Vector3 originalScale = rectTransform.localScale;
        Vector3 targetScale = originalScale * (1f + scaleAmount);
        
        // Tạo animation phóng to thu nhỏ liên tục
        scaleTween = rectTransform.DOScale(targetScale, scaleDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo); // Loop vô hạn với kiểu Yoyo (phóng to rồi thu nhỏ)
    }
    
    private void OnDestroy()
    {
        // Dừng animation khi destroy
        if (scaleTween != null)
        {
            scaleTween.Kill();
        }
    }

    private void OnStartButtonClick()
    {
        AudioManager.Instance.PlaySelectSound();
        SceneManager.LoadScene("GamePlayScene");
    }
}
