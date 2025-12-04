using System.Collections.Generic;
using UnityEngine;

public class Gem : MonoBehaviour
{
    [SerializeField] private int gemId;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Color gemColor;
    [SerializeField] private List<CellUI> cells; // Các cell UI mà gem chiếm giữ
    [SerializeField] private bool isCollected;
    [SerializeField] private Vector2Int position; // Vị trí top-left của gem trên board
    
    public int GemId => gemId;
    public int Width => width;
    public int Height => height;
    public Color GemColor => gemColor;
    public List<CellUI> Cells => cells;
    public bool IsCollected => isCollected;
    public Vector2Int Position => position;
    
    public void Initialize(int id, int w, int h, Color color)
    {
        gemId = id;
        width = w;
        height = h;
        gemColor = color;
        cells = new List<CellUI>();
        isCollected = false;
    }
    
    public void SetPosition(Vector2Int pos)
    {
        position = pos;
    }
    
    public void AddCellUI(CellUI cell)
    {
        if (!cells.Contains(cell))
        {
            cells.Add(cell);
            cell.SetGem(this);
        }
    }
    
    public bool IsFullyRevealed()
    {
        if (cells == null || cells.Count == 0)
            return false;
            
        foreach (var cell in cells)
        {
            if (!cell.IsRevealed)
                return false;
        }
        return true;
    }
    
    public void Collect()
    {
        isCollected = true;
        
        // TODO: Animation gem bay về slot
    }
}

