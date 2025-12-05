using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class HomePanel : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private float scaleDuration = 0.5f; // Duration for each scale in/out
    [SerializeField] private float scaleAmount = 0.1f; // Scale amount (e.g., 0.1 = 10%)
    
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
        
        // Create continuous scale in/out animation
        scaleTween = rectTransform.DOScale(targetScale, scaleDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo); // Infinite loop with Yoyo type (scale up then down)
    }
    
    private void OnDestroy()
    {
        // Stop animation when destroyed
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
