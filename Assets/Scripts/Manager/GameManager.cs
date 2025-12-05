using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private StageManager stageManager;
    [SerializeField] private BoardManager boardManager;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        InitializeGame();
    }
    
    private void InitializeGame()
    {
        stageManager.LoadStage(1);
    }
    
    // Không cần Update() nữa vì click được xử lý trực tiếp qua IPointerClickHandler trong CellUI
    
    public void DigCellUI(CellUI cell)
    {
        if (cell == null || cell.IsRevealed) return;
        
        bool success = boardManager.DigCell(cell.BoardX, cell.BoardY);
        
        if (success)
        {
            // Check if stage is complete
            if (stageManager.CheckStageComplete())
            {
                stageManager.CompleteStage();
            }
        }
    }
    
    public void OnAddPickaxeConfirmed(bool confirmed)
    {
        if (confirmed)
        {
            PickaxeManager.Instance.AddPickaxes(100);
        }
    }
}

