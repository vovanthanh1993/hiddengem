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
    
    public bool PlaceGemsRandomly(List<Gem> gems, GemConfigData gemConfigData, Dictionary<Gem, GemOrientation> gemOrientations = null)
    {
        hiddenGems.Clear();
        
        // Validation: Kiểm tra board có đủ chỗ không
        if (!ValidateBoardCapacity(gems, gemConfigData, gemOrientations))
        {
            Debug.LogError("Board is too small to fit all gems!");
            return false;
        }
        
        // Retry mechanism: Thử nhiều lần với các cách sắp xếp khác nhau
        int maxRetries = 50;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            // Clear board trước mỗi lần thử
            ClearAllGems();
            
            // Sắp xếp gems theo kích thước (lớn trước) để dễ đặt hơn
            List<Gem> sortedGems = new List<Gem>(gems);
            
            // Xáo trộn một chút để có nhiều cách sắp xếp khác nhau
            if (retry > 0)
            {
                // Xáo trộn ngẫu nhiên nhưng vẫn ưu tiên gem lớn
                sortedGems.Sort((a, b) => 
                {
                    int sizeA = a.Width * a.Height;
                    int sizeB = b.Width * b.Height;
                    int sizeCompare = sizeB.CompareTo(sizeA);
                    // Nếu cùng kích thước, xáo trộn ngẫu nhiên
                    if (sizeCompare == 0)
                        return Random.Range(-1, 2);
                    return sizeCompare;
                });
            }
            else
            {
                sortedGems.Sort((a, b) => 
                {
                    int sizeA = a.Width * a.Height;
                    int sizeB = b.Width * b.Height;
                    return sizeB.CompareTo(sizeA); // Lớn trước
                });
            }
            
            // Thử đặt tất cả gem với backtracking
            if (PlaceGemsWithBacktracking(sortedGems, gemConfigData, gemOrientations))
            {
                Debug.Log($"Successfully placed all {gems.Count} gems after {retry + 1} attempt(s)");
                return true;
            }
        }
        
        Debug.LogError($"CRITICAL: Failed to place all gems after {maxRetries} attempts!");
        return false;
    }
    
    private bool ValidateBoardCapacity(List<Gem> gems, GemConfigData gemConfigData, Dictionary<Gem, GemOrientation> gemOrientations)
    {
        int totalBoardArea = boardWidth * boardHeight;
        int totalGemArea = 0;
        
        foreach (var gem in gems)
        {
            var config = gemConfigData.GetGemConfig(gem.GemId);
            if (config == null) continue;
            
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gem))
            {
                orientation = gemOrientations[gem];
            }
            
            // Tính diện tích gem sau khi xoay theo orientation
            int gemArea;
            if (config.width == config.height)
            {
                gemArea = config.width * config.height;
            }
            else if (orientation == GemOrientation.Horizontal)
            {
                // Nằm ngang: width > height
                gemArea = (config.width > config.height) ? 
                    config.width * config.height : 
                    config.height * config.width;
            }
            else // Vertical
            {
                // Nằm dọc: height > width
                gemArea = (config.height > config.width) ? 
                    config.width * config.height : 
                    config.height * config.width;
            }
            
            totalGemArea += gemArea;
        }
        
        // Board phải có diện tích lớn hơn tổng diện tích gem (cần thêm buffer cho dynamite và spacing)
        if (totalGemArea > totalBoardArea * 0.9f) // Cho phép 90% board được dùng cho gem
        {
            Debug.LogWarning($"Board capacity warning: Total gem area ({totalGemArea}) vs Board area ({totalBoardArea})");
            return false;
        }
        
        return true;
    }
    
    private void ClearAllGems()
    {
        // Xóa tất cả gem khỏi board
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (board[x, y].Gem != null)
                {
                    board[x, y].SetGem(null);
                }
            }
        }
        hiddenGems.Clear();
    }
    
    private bool PlaceGemsWithBacktracking(List<Gem> gems, GemConfigData gemConfigData, Dictionary<Gem, GemOrientation> gemOrientations)
    {
        return PlaceGemsRecursive(gems, 0, gemConfigData, gemOrientations);
    }
    
    private bool PlaceGemsRecursive(List<Gem> gems, int index, GemConfigData gemConfigData, Dictionary<Gem, GemOrientation> gemOrientations)
    {
        // Nếu đã đặt hết tất cả gem
        if (index >= gems.Count)
        {
            return true;
        }
        
        Gem gem = gems[index];
        GemOrientation orientation = GemOrientation.Horizontal;
        if (gemOrientations != null && gemOrientations.ContainsKey(gem))
        {
            orientation = gemOrientations[gem];
        }
        
        // Lấy tất cả vị trí có thể đặt gem này
        List<PlacementOption> validPlacements = GetAllValidPlacements(gem, gemConfigData, orientation);
        
        // Xáo trộn để có tính ngẫu nhiên
        for (int i = 0; i < validPlacements.Count; i++)
        {
            int randomIndex = Random.Range(i, validPlacements.Count);
            PlacementOption temp = validPlacements[i];
            validPlacements[i] = validPlacements[randomIndex];
            validPlacements[randomIndex] = temp;
        }
        
        // Thử từng vị trí
        foreach (var placement in validPlacements)
        {
            var config = gemConfigData.GetGemConfig(gem.GemId);
            if (config == null) continue;
            
            // Đặt gem tại vị trí này
            PlaceGemAt(gem, placement.x, placement.y, config, placement.actualWidth, placement.actualHeight, placement.isRotated);
            hiddenGems.Add(gem);
            
            // Đệ quy đặt gem tiếp theo
            if (PlaceGemsRecursive(gems, index + 1, gemConfigData, gemOrientations))
            {
                return true; // Thành công!
            }
            
            // Nếu không thành công, backtrack: xóa gem này
            RemoveGem(gem);
            hiddenGems.Remove(gem);
        }
        
        return false; // Không tìm được cách đặt
    }
    
    private void RemoveGem(Gem gem)
    {
        // Xóa gem khỏi tất cả cells
        if (gem.Cells != null)
        {
            foreach (var cell in gem.Cells)
            {
                if (cell != null)
                {
                    cell.SetGem(null);
                    // Reset cell về trạng thái ban đầu (stone)
                    cell.SetStoneLayers(Random.Range(0, 2) == 0 ? 1 : 2);
                }
            }
            // Clear danh sách cells của gem
            gem.Cells.Clear();
        }
    }
    
    private List<PlacementOption> GetAllValidPlacements(Gem gem, GemConfigData gemConfigData, GemOrientation orientation)
    {
        List<PlacementOption> validPlacements = new List<PlacementOption>();
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return validPlacements;
        
        // Xác định các hướng xoay dựa trên orientation được quy định
        bool canRotate = config.width != config.height;
        bool[] rotationOptions;
        
        if (!canRotate)
        {
            rotationOptions = new bool[] { false };
        }
        else if (orientation == GemOrientation.Horizontal)
        {
            rotationOptions = new bool[] { config.width > config.height ? false : true };
        }
        else // Vertical
        {
            rotationOptions = new bool[] { config.height > config.width ? false : true };
        }
        
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
        
        return validPlacements;
    }
    
    private bool PlaceGemRandomly(Gem gem, GemConfigData gemConfigData, GemOrientation orientation = GemOrientation.Horizontal)
    {
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Thu thập tất cả vị trí có thể đặt gem
        List<PlacementOption> validPlacements = new List<PlacementOption>();
        
        // Xác định các hướng xoay dựa trên orientation được quy định
        bool canRotate = config.width != config.height;
        bool[] rotationOptions;
        
        if (!canRotate)
        {
            // Gem vuông, không thể xoay
            rotationOptions = new bool[] { false };
        }
        else if (orientation == GemOrientation.Horizontal)
        {
            // Nằm ngang: width > height sau khi xoay
            // Nếu width gốc > height gốc thì không xoay, ngược lại thì xoay
            rotationOptions = new bool[] { config.width > config.height ? false : true };
        }
        else // Vertical
        {
            // Nằm dọc: height > width sau khi xoay
            // Nếu height gốc > width gốc thì không xoay, ngược lại thì xoay
            rotationOptions = new bool[] { config.height > config.width ? false : true };
        }
        
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
    
    private bool PlaceGemWithAllPositions(Gem gem, GemConfigData gemConfigData, GemOrientation orientation = GemOrientation.Horizontal)
    {
        // Fallback: thử lại với nhiều lần hơn và cách tiếp cận khác
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Tạo danh sách tất cả vị trí có thể theo thứ tự ngẫu nhiên
        List<PlacementOption> allOptions = new List<PlacementOption>();
        
        // Xác định các hướng xoay dựa trên orientation được quy định
        bool canRotate = config.width != config.height;
        bool[] rotationOptions;
        
        if (!canRotate)
        {
            rotationOptions = new bool[] { false };
        }
        else if (orientation == GemOrientation.Horizontal)
        {
            rotationOptions = new bool[] { config.width > config.height ? false : true };
        }
        else // Vertical
        {
            rotationOptions = new bool[] { config.height > config.width ? false : true };
        }
        
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

