using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMusicManager : MonoBehaviour
{
    public string bgmName = "";
    void Start()
    {
        AudioManager.Instance.PlayMusic(bgmName);
    }
}
