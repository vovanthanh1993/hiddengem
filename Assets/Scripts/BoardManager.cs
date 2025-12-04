using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private CellUI cellUIPrefab; // Prefab cho UI cell
    [SerializeField] private RectTransform boardParentUI; // Parent trong Canvas cho UI cells
    [SerializeField] private int cellSize = 100; // Kích thước mỗi cell (100x100 pixels)
    
    [Header("Board Background")]
    [SerializeField] private Sprite boardBackgroundSprite; // Sprite cho background của board (optional)
    [SerializeField] private Color boardBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Màu background (mặc định xám)
    
    private CellUI[,] board;
    private int boardWidth;
    private int boardHeight;
    private List<Gem> hiddenGems;
    private List<Vector2Int> dynamitePositions;
    
    public CellUI[,] Board => board;
    public int BoardWidth => boardWidth;
    public int BoardHeight => boardHeight;
    
    public void InitializeBoard(int width, int height)
    {
        boardWidth = width;
        boardHeight = height;
        board = new CellUI[width, height];
        hiddenGems = new List<Gem>();
        dynamitePositions = new List<Vector2Int>();
        
        // Tạo boardParentUI nếu chưa có
        if (boardParentUI == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            GameObject boardObj = new GameObject("Board");
            boardObj.transform.SetParent(canvasObj.transform, false);
            boardParentUI = boardObj.AddComponent<RectTransform>();
            boardParentUI.anchorMin = new Vector2(0.5f, 0.5f);
            boardParentUI.anchorMax = new Vector2(0.5f, 0.5f);
            boardParentUI.pivot = new Vector2(0.5f, 0.5f);
            boardParentUI.anchoredPosition = Vector2.zero;
        }
        
        // Cập nhật kích thước board
        boardParentUI.sizeDelta = new Vector2(width * cellSize, height * cellSize);
        
        // Clear existing cells
        foreach (Transform child in boardParentUI)
        {
            Destroy(child.gameObject);
        }
        
        // Tạo background cho board (sibling của boardParentUI, không phải child)
        SetupBoardBackground();
        
        // Tạo cells với GridLayoutGroup để tự động sắp xếp
        GridLayoutGroup gridLayout = boardParentUI.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            gridLayout = boardParentUI.gameObject.AddComponent<GridLayoutGroup>();
        }
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.spacing = Vector2.zero;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = width;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        
        // Create cells
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CreateCell(x, y);
            }
        }
    }
    
    private void CreateCell(int x, int y)
    {
        CellUI cell;
        if (cellUIPrefab != null)
        {
            cell = Instantiate(cellUIPrefab, boardParentUI);
        }
        else
        {
            GameObject cellObj = new GameObject($"Cell_{x}_{y}");
            cellObj.transform.SetParent(boardParentUI, false);
            
            RectTransform rectTransform = cellObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
            
            Image image = cellObj.AddComponent<Image>();
            cell = cellObj.AddComponent<CellUI>();
        }
        
        cell.Initialize(x, y);
        board[x, y] = cell;
    }
    
    public void PlaceGemsRandomly(List<Gem> gems, GemConfigData gemConfigData)
    {
        hiddenGems.Clear();
        
        // Sắp xếp gems theo kích thước (lớn trước) để dễ đặt hơn
        List<Gem> sortedGems = new List<Gem>(gems);
        sortedGems.Sort((a, b) => 
        {
            int sizeA = a.Width * a.Height;
            int sizeB = b.Width * b.Height;
            return sizeB.CompareTo(sizeA); // Lớn trước
        });
        
        foreach (var gem in sortedGems)
        {
            if (!PlaceGemRandomly(gem, gemConfigData))
            {
                Debug.LogError($"Failed to place gem {gem.GemId} - board may be too small!");
                // Thử lại với cách tiếp cận khác: tìm tất cả vị trí có thể và chọn ngẫu nhiên
                if (!PlaceGemWithAllPositions(gem, gemConfigData))
                {
                    Debug.LogError($"CRITICAL: Could not place gem {gem.GemId} even with exhaustive search!");
                }
            }
        }
    }
    
    private bool PlaceGemRandomly(Gem gem, GemConfigData gemConfigData)
    {
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Thu thập tất cả vị trí có thể đặt gem (cả hai hướng nếu có thể xoay)
        List<PlacementOption> validPlacements = new List<PlacementOption>();
        
        // Thử cả hai hướng nếu gem có thể xoay
        bool canRotate = config.width != config.height;
        bool[] rotationOptions = canRotate ? new bool[] { false, true } : new bool[] { false };
        
        foreach (bool isRotated in rotationOptions)
        {
            int actualWidth = isRotated ? config.height : config.width;
            int actualHeight = isRotated ? config.width : config.height;
            
            // Kiểm tra tất cả vị trí có thể
            for (int x = 0; x <= boardWidth - actualWidth; x++)
            {
                for (int y = 0; y <= boardHeight - actualHeight; y++)
                {
                    if (CanPlaceGem(x, y, actualWidth, actualHeight))
                    {
                        validPlacements.Add(new PlacementOption
                        {
                            x = x,
                            y = y,
                            isRotated = isRotated,
                            actualWidth = actualWidth,
                            actualHeight = actualHeight
                        });
                    }
                }
            }
        }
        
        // Nếu có vị trí hợp lệ, chọn ngẫu nhiên một vị trí
        if (validPlacements.Count > 0)
        {
            PlacementOption chosen = validPlacements[Random.Range(0, validPlacements.Count)];
            PlaceGemAt(gem, chosen.x, chosen.y, config, chosen.actualWidth, chosen.actualHeight, chosen.isRotated);
            hiddenGems.Add(gem);
            return true;
        }
        
        return false;
    }
    
    private bool PlaceGemWithAllPositions(Gem gem, GemConfigData gemConfigData)
    {
        // Fallback: thử lại với nhiều lần hơn và cách tiếp cận khác
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Tạo danh sách tất cả vị trí có thể theo thứ tự ngẫu nhiên
        List<PlacementOption> allOptions = new List<PlacementOption>();
        bool canRotate = config.width != config.height;
        bool[] rotationOptions = canRotate ? new bool[] { false, true } : new bool[] { false };
        
        foreach (bool isRotated in rotationOptions)
        {
            int actualWidth = isRotated ? config.height : config.width;
            int actualHeight = isRotated ? config.width : config.height;
            
            for (int x = 0; x <= boardWidth - actualWidth; x++)
            {
                for (int y = 0; y <= boardHeight - actualHeight; y++)
                {
                    if (CanPlaceGem(x, y, actualWidth, actualHeight))
                    {
                        allOptions.Add(new PlacementOption
                        {
                            x = x,
                            y = y,
                            isRotated = isRotated,
                            actualWidth = actualWidth,
                            actualHeight = actualHeight
                        });
                    }
                }
            }
        }
        
        // Xáo trộn danh sách
        for (int i = 0; i < allOptions.Count; i++)
        {
            PlacementOption temp = allOptions[i];
            int randomIndex = Random.Range(i, allOptions.Count);
            allOptions[i] = allOptions[randomIndex];
            allOptions[randomIndex] = temp;
        }
        
        // Thử từng vị trí
        foreach (var option in allOptions)
        {
            if (CanPlaceGem(option.x, option.y, option.actualWidth, option.actualHeight))
            {
                PlaceGemAt(gem, option.x, option.y, config, option.actualWidth, option.actualHeight, option.isRotated);
                hiddenGems.Add(gem);
                return true;
            }
        }
        
        return false;
    }
    
    private struct PlacementOption
    {
        public int x;
        public int y;
        public bool isRotated;
        public int actualWidth;
        public int actualHeight;
    }
    
    private bool CanPlaceGem(int x, int y, int width, int height)
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (board[x + i, y + j].Gem != null)
                    return false;
            }
        }
        return true;
    }
    
    private void PlaceGemAt(Gem gem, int x, int y, GemConfig config, int actualWidth, int actualHeight, bool isRotated)
    {
        gem.SetPosition(new Vector2Int(x, y));
        
        for (int i = 0; i < actualWidth; i++)
        {
            for (int j = 0; j < actualHeight; j++)
            {
                CellUI cell = board[x + i, y + j];
                gem.AddCellUI(cell);
                
                // Tính toán vị trí của cell trong gem sprite gốc
                // Nếu gem bị xoay, cần map lại vị trí
                // Khi xoay: (i, j) trong board -> (j, i) trong sprite gốc
                // Ví dụ: gem 1x3 xoay thành 3x1
                //   - Cell (0,0) trong board -> (0, 0) trong sprite gốc
                //   - Cell (1,0) trong board -> (0, 1) trong sprite gốc
                //   - Cell (2,0) trong board -> (0, 2) trong sprite gốc
                int gemCellX, gemCellY;
                if (isRotated)
                {
                    // Gem bị xoay: swap i và j
                    gemCellX = j; // j trong board -> X trong sprite gốc
                    gemCellY = i; // i trong board -> Y trong sprite gốc
                }
                else
                {
                    gemCellX = i;
                    gemCellY = j;
                }
                
                // Set gem info cho cell với actualWidth và actualHeight (đã xoay)
                // Nhưng cần truyền width và height gốc để sprite hiển thị đúng
                cell.SetGemInfo(gem, gemCellX, gemCellY, config.width, config.height, actualWidth, actualHeight, isRotated);
                
                // Random stone layers (1 or 2)
                int layers = Random.Range(0, 2) == 0 ? 1 : 2;
                cell.SetStoneLayers(layers);
            }
        }
    }
    
    public void PlaceDynamites(int count)
    {
        dynamitePositions.Clear();
        
        for (int i = 0; i < count; i++)
        {
            PlaceDynamiteRandomly();
        }
    }
    
    private void PlaceDynamiteRandomly()
    {
        int maxAttempts = 100;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int x = Random.Range(0, boardWidth);
            int y = Random.Range(0, boardHeight);
            
            if (board[x, y].Gem == null && !board[x, y].IsDynamite)
            {
                board[x, y].SetDynamite(true);
                dynamitePositions.Add(new Vector2Int(x, y));
                return;
            }
        }
    }
    
    public bool DigCell(int x, int y)
    {
        if (x < 0 || x >= boardWidth || y < 0 || y >= boardHeight)
            return false;
            
        CellUI cell = board[x, y];
        if (cell.IsRevealed)
            return false;
        
        int pickaxesNeeded = cell.StoneLayers;
        
        if (!PickaxeManager.Instance.HasEnoughPickaxes(pickaxesNeeded))
        {
            PickaxeManager.Instance.ShowAddPickaxePopup();
            return false;
        }
        
        PickaxeManager.Instance.UsePickaxe(pickaxesNeeded);
        cell.Dig();
        
        // Check if hit dynamite
        if (cell.IsDynamite)
        {
            ExplodeDynamite(x, y);
        }
        
        // Xử lý gem reveal với tỉ lệ 30%
        ProcessGemReveal(cell);
        
        return true;
    }
    
    private void ProcessGemReveal(CellUI cell)
    {
        // Check gem hit ratio (30% chance)
        bool hitGem = Random.Range(0f, 1f) < 0.3f;
        
        // If no gem hit but not enough gems to complete stage, force hit
        if (!hitGem)
        {
            int revealedGems = CountRevealedGems();
            int totalGemsNeeded = hiddenGems.Count;
            if (revealedGems < totalGemsNeeded)
            {
                hitGem = true;
            }
        }
        
        if (hitGem && cell.Gem != null && !cell.Gem.IsCollected)
        {
            // Gem hit - check if fully revealed
            if (cell.Gem.IsFullyRevealed())
            {
                cell.Gem.Collect();
            }
        }
    }
    
    private void ExplodeDynamite(int x, int y)
    {
        // Đào và reveal các cell trong vùng nổ 3x3 với tỉ lệ 30% hiện gem
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int newX = x + i;
                int newY = y + j;
                
                if (newX >= 0 && newX < boardWidth && newY >= 0 && newY < boardHeight)
                {
                    CellUI cell = board[newX, newY];
                    
                    // Chỉ xử lý nếu cell chưa được reveal
                    if (!cell.IsRevealed)
                    {
                        // Đào cell (remove stone layers và reveal)
                        cell.Dig();
                        
                        // Xử lý gem reveal với tỉ lệ 30% giống như khi đào bằng pickaxe
                        ProcessGemReveal(cell);
                    }
                }
            }
        }
    }
    
    private int CountRevealedGems()
    {
        int count = 0;
        foreach (var gem in hiddenGems)
        {
            if (gem.IsFullyRevealed() && !gem.IsCollected)
                count++;
        }
        return count;
    }
    
    public List<Gem> GetCollectedGems()
    {
        List<Gem> collected = new List<Gem>();
        foreach (var gem in hiddenGems)
        {
            if (gem.IsCollected)
                collected.Add(gem);
        }
        return collected;
    }
    
    private void SetupBoardBackground()
    {
        // Tìm background đã tồn tại (sibling của boardParentUI)
        Transform parentTransform = boardParentUI.parent;
        Transform backgroundTransform = null;
        
        if (parentTransform != null)
        {
            backgroundTransform = parentTransform.Find("BoardBackground");
        }
        
        Image backgroundImage;
        RectTransform bgRectTransform;
        
        if (backgroundTransform != null)
        {
            backgroundImage = backgroundTransform.GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = backgroundTransform.gameObject.AddComponent<Image>();
            }
            bgRectTransform = backgroundTransform.GetComponent<RectTransform>();
        }
        else
        {
            // Tạo GameObject mới cho background làm sibling của boardParentUI
            GameObject backgroundObj = new GameObject("BoardBackground");
            
            // Đặt cùng parent với boardParentUI
            if (boardParentUI.parent != null)
            {
                backgroundObj.transform.SetParent(boardParentUI.parent, false);
            }
            else
            {
                // Nếu boardParentUI chưa có parent, tìm Canvas
                GameObject canvasObj = GameObject.Find("Canvas");
                if (canvasObj != null)
                {
                    backgroundObj.transform.SetParent(canvasObj.transform, false);
                }
            }
            
            // Đặt làm sibling trước boardParentUI để nằm phía sau
            backgroundObj.transform.SetSiblingIndex(boardParentUI.GetSiblingIndex());
            
            bgRectTransform = backgroundObj.AddComponent<RectTransform>();
            backgroundImage = backgroundObj.AddComponent<Image>();
        }
        
        // Đảm bảo background có cùng anchor, pivot, position và size với boardParentUI
        if (bgRectTransform != null)
        {
            bgRectTransform.anchorMin = boardParentUI.anchorMin;
            bgRectTransform.anchorMax = boardParentUI.anchorMax;
            bgRectTransform.pivot = boardParentUI.pivot;
            bgRectTransform.anchoredPosition = boardParentUI.anchoredPosition;
            bgRectTransform.sizeDelta = boardParentUI.sizeDelta;
        }
        
        // Set sprite và color cho background
        backgroundImage.sprite = boardBackgroundSprite;
        backgroundImage.color = boardBackgroundColor;
        backgroundImage.type = Image.Type.Simple;
    }
}

