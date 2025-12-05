using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class HomePanel : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartButtonClick);
    }

    private void OnStartButtonClick()
    {
        SceneManager.LoadScene("GamePlay");
    }
}
