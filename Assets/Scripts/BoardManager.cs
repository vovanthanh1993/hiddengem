using System.Collections;
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
    
    [Header("Explosion Effects")]
    [SerializeField] private ParticleSystem explosionParticlePrefab; // Prefab cho explosion particle system
    [SerializeField] private float explosionParticleDuration = 1.5f; // Thời gian hiệu ứng nổ
    
    [Header("Digging Settings")]
    [SerializeField] private float digCooldown = 0.3f; // Khoảng cách thời gian giữa mỗi lần đào (giây)
    
    private CellUI[,] board;
    private float lastDigTime = 0f; // Thời gian lần đào cuối cùng
    private int boardWidth;
    private int boardHeight;
    private List<Gem> hiddenGems;
    private List<Gem> unplacedGems; // Pool các gems chưa được đặt lên board
    private List<Vector2Int> dynamitePositions;
    private GemConfigData gemConfigData;
    private Dictionary<Gem, GemOrientation> gemOrientations;
    
    public CellUI[,] Board => board;
    public int BoardWidth => boardWidth;
    public int BoardHeight => boardHeight;
    
    public void InitializeBoard(int width, int height)
    {
        boardWidth = width;
        boardHeight = height;
        board = new CellUI[width, height];
        hiddenGems = new List<Gem>();
        unplacedGems = new List<Gem>();
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
        
        // Set stone layers ngẫu nhiên cho tất cả các cell (1 hoặc 2 layers)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int layers = Random.Range(0, 2) == 0 ? 1 : 2;
                board[x, y].SetStoneLayers(layers);
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
    
    public void InitializeGemPool(List<Gem> gems, GemConfigData configData, Dictionary<Gem, GemOrientation> orientations = null)
    {
        // Xóa các gem cũ khỏi board và destroy các GameObject gem cũ
        ClearAllGems();
        
        // Destroy các gem GameObject cũ từ unplacedGems và hiddenGems
        if (unplacedGems != null)
        {
            foreach (var oldGem in unplacedGems)
            {
                if (oldGem != null && oldGem.gameObject != null)
                {
                    Destroy(oldGem.gameObject);
                }
            }
        }
        
        if (hiddenGems != null)
        {
            foreach (var oldGem in hiddenGems)
            {
                if (oldGem != null && oldGem.gameObject != null)
                {
                    Destroy(oldGem.gameObject);
                }
            }
        }
        
        // Lưu danh sách gems chưa được đặt vào pool (không đặt lên board ngay)
        unplacedGems = new List<Gem>(gems);
        gemConfigData = configData;
        gemOrientations = orientations ?? new Dictionary<Gem, GemOrientation>();
        hiddenGems.Clear();
        
        Debug.Log($"Initialized gem pool with {unplacedGems.Count} gems (not placed on board yet)");
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
                CellUI cell = board[x + i, y + j];
                // Không thể đặt gem nếu:
                // - Cell đã có gem
                // - Cell đã bị exclude khỏi gem spawn
                // - Cell đã được reveal (đã đào trước đó)
                // - Cell có dynamite (thuốc nổ)
                if (cell.Gem != null || cell.IsExcludedFromGemSpawn || cell.IsRevealed || cell.IsDynamite)
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
                
                // Không thay đổi stone layers của cell khi spawn gem - giữ nguyên stone layers ban đầu
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
        
        // Kiểm tra cooldown giữa các lần đào
        float currentTime = Time.time;
        if (currentTime - lastDigTime < digCooldown)
        {
            return false; // Chưa đủ thời gian, không cho đào
        }
            
        CellUI cell = board[x, y];
        if (cell.IsRevealed)
            return false;
        
        int pickaxesNeeded = cell.StoneLayers;
        
        if (!PickaxeManager.Instance.HasEnoughPickaxes(pickaxesNeeded))
        {
            PickaxeManager.Instance.ShowAddPickaxePopup();
            return false;
        }
        
        // Cập nhật thời gian đào cuối cùng
        lastDigTime = currentTime;
        
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
        // Nếu cell đã có gem (đã được spawn trước đó), chỉ cần check reveal
        if (cell.Gem != null && !cell.Gem.IsCollected)
        {
            // Gem hit - check if fully revealed
            if (cell.Gem.IsFullyRevealed())
            {
                cell.Gem.Collect();
            }
            return;
        }
        
        // Nếu cell chưa có gem, thử spawn gem mới từ pool
        // Check gem hit ratio (30% chance)
        bool hitGem = Random.Range(0f, 1f) < 0.3f;
        
        // Nếu hitGem = false, phải kiểm tra xem các gem còn lại có thể được đặt trên board không
        // Nếu không thể đặt được, phải force hitGem = true
        if (!hitGem && unplacedGems.Count > 0)
        {
            // Kiểm tra xem các gem còn lại có thể được đặt trên board không
            // (giả định cell này không có gem - tức là không spawn gem ở đây)
            if (!CanPlaceRemainingGemsIgnoringCell(cell))
            {
                // Nếu không thể đặt đủ các gem còn lại, phải force hit để spawn gem ở cell này
                hitGem = true;
                Debug.Log($"Force hit: Cannot place remaining {unplacedGems.Count} gems without using this cell");
            }
        }
        
        Debug.Log("hitGem: " + hitGem);
        
        if (hitGem && unplacedGems.Count > 0)
        {
            // Kiểm tra xem các gem còn lại có thể đặt được không nếu không spawn gem ở cell này
            bool canPlaceRemainingWithoutThisCell = CanPlaceRemainingGemsIgnoringCell(cell);
            
            // Spawn một gem ngẫu nhiên từ pool, PHẢI đặt chính xác ở cell vừa đào
            bool spawnSuccess = SpawnGemAtExactCell(cell);
            
            // Nếu spawn fail, phải force spawn nếu:
            // 1. Các gem còn lại không thể đặt được nếu không spawn gem ở cell này, HOẶC
            // 2. Đây là force hit (đã được force từ false thành true)
            if (!spawnSuccess && unplacedGems.Count > 0)
            {
                if (!canPlaceRemainingWithoutThisCell)
                {
                    // Phải force spawn gem ở cell này (bỏ qua điều kiện các gem còn lại có thể đặt)
                    Debug.LogWarning("Spawn failed but remaining gems cannot be placed - forcing spawn at this cell");
                    ForceSpawnGemAtCellIgnoringRemainingGems(cell);
                }
                else
                {
                    Debug.LogWarning($"Spawn failed at cell ({cell.BoardX}, {cell.BoardY}) but remaining gems can be placed");
                }
            }
        }
        else if (!hitGem)
        {
            // Chỉ đánh dấu cell này là excluded khi đã chắc chắn các gem còn lại có thể đặt được
            // (Logic này đã được kiểm tra ở trên, nếu không thể đặt thì hitGem đã được force = true)
            cell.MarkAsExcludedFromGemSpawn();
        }
    }
    
    // Force spawn gem ở cell mà không kiểm tra điều kiện các gem còn lại có thể đặt không
    private void ForceSpawnGemAtCellIgnoringRemainingGems(CellUI cell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return;
        
        // Reset exclude flag của cell này để có thể spawn gem
        cell.ResetExcludedFromGemSpawn();
        
        // Xáo trộn danh sách gems để có tính ngẫu nhiên
        List<Gem> shuffledGems = new List<Gem>(unplacedGems);
        for (int i = 0; i < shuffledGems.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffledGems.Count);
            Gem temp = shuffledGems[i];
            shuffledGems[i] = shuffledGems[randomIndex];
            shuffledGems[randomIndex] = temp;
        }
        
        // Thử spawn từng gem ngẫu nhiên
        foreach (Gem gemToSpawn in shuffledGems)
        {
            // Lấy orientation của gem này
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            // Tìm tất cả vị trí có thể đặt gem này mà chứa chính xác cell này
            List<PlacementOption> placementsAtCell = GetPlacementsContainingCell(gemToSpawn, gemConfigData, orientation, cell);
            
            if (placementsAtCell.Count > 0)
            {
                // Chọn ngẫu nhiên một placement (không kiểm tra điều kiện các gem còn lại)
                var chosenPlacement = placementsAtCell[Random.Range(0, placementsAtCell.Count)];
                
                // Reset exclude flag của tất cả cells trong placement
                for (int x = 0; x < chosenPlacement.actualWidth; x++)
                {
                    for (int y = 0; y < chosenPlacement.actualHeight; y++)
                    {
                        board[chosenPlacement.x + x, chosenPlacement.y + y].ResetExcludedFromGemSpawn();
                    }
                }
                
                // Đặt gem tại vị trí này
                PlaceGemAt(gemToSpawn, chosenPlacement.x, chosenPlacement.y, config, chosenPlacement.actualWidth, chosenPlacement.actualHeight, chosenPlacement.isRotated);
                
                // Xóa khỏi pool và thêm vào hiddenGems
                unplacedGems.Remove(gemToSpawn);
                hiddenGems.Add(gemToSpawn);
                
                Debug.Log($"Force spawned gem {gemToSpawn.GemId} at cell ({cell.BoardX}, {cell.BoardY}) - ignoring remaining gems check");
                return; // Thành công
            }
        }
        
        Debug.LogError($"Failed to force spawn gem at cell ({cell.BoardX}, {cell.BoardY})");
    }
    
    private void ForceSpawnGemAtCell(CellUI cell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return;
        
        // Reset exclude flag của cell này để có thể spawn gem
        cell.ResetExcludedFromGemSpawn();
        
        // Thử spawn từng gem trong pool để tìm gem có thể đặt ở cell này
        for (int i = 0; i < unplacedGems.Count; i++)
        {
            Gem gemToSpawn = unplacedGems[i];
            
            // Lấy orientation của gem này
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            // Tìm tất cả vị trí có thể đặt gem này mà chứa cell (bỏ qua exclude flag)
            List<PlacementOption> allPlacements = GetAllValidPlacementsIgnoringExclude(gemToSpawn, gemConfigData, orientation);
            
            foreach (var placement in allPlacements)
            {
                // Kiểm tra xem placement này có chứa cell không
                bool containsCell = false;
                for (int x = 0; x < placement.actualWidth; x++)
                {
                    for (int y = 0; y < placement.actualHeight; y++)
                    {
                        if (placement.x + x == cell.BoardX && placement.y + y == cell.BoardY)
                        {
                            containsCell = true;
                            break;
                        }
                    }
                    if (containsCell) break;
                }
                
                if (containsCell)
                {
                    // Kiểm tra lại xem có thể đặt gem không (chỉ kiểm tra gem và revealed, không kiểm tra exclude flag)
                    bool canPlace = true;
                    for (int x = 0; x < placement.actualWidth; x++)
                    {
                        for (int y = 0; y < placement.actualHeight; y++)
                        {
                            CellUI tempCell = board[placement.x + x, placement.y + y];
                            if (tempCell.Gem != null || tempCell.IsRevealed || tempCell.IsDynamite)
                            {
                                canPlace = false;
                                break;
                            }
                        }
                        if (!canPlace) break;
                    }
                    
                    if (canPlace)
                    {
                        // Kiểm tra xem sau khi đặt gem này, các gem còn lại có thể được đặt không
                        if (CanPlaceRemainingGemsAfterPlacing(gemToSpawn, placement))
                        {
                            // Reset exclude flag của tất cả cells trong placement
                            for (int x = 0; x < placement.actualWidth; x++)
        {
                                for (int y = 0; y < placement.actualHeight; y++)
                                {
                                    board[placement.x + x, placement.y + y].ResetExcludedFromGemSpawn();
                                }
                            }
                            
                            // Đặt gem tại vị trí này
                            PlaceGemAt(gemToSpawn, placement.x, placement.y, config, placement.actualWidth, placement.actualHeight, placement.isRotated);
                            
                            // Xóa khỏi pool và thêm vào hiddenGems
                            unplacedGems.RemoveAt(i);
                            hiddenGems.Add(gemToSpawn);
                            
                            Debug.Log($"Force spawned gem {gemToSpawn.GemId} at cell ({cell.BoardX}, {cell.BoardY})");
                            return; // Thành công
                        }
                    }
                }
            }
        }
        
        Debug.LogError($"Failed to force spawn gem at cell ({cell.BoardX}, {cell.BoardY})");
    }
    
    // Lấy tất cả vị trí có thể đặt gem mà bỏ qua exclude flag
    private List<PlacementOption> GetAllValidPlacementsIgnoringExclude(Gem gem, GemConfigData gemConfigData, GemOrientation orientation)
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
        else
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
                    // Kiểm tra có thể đặt gem không (chỉ kiểm tra gem, revealed và dynamite, không kiểm tra exclude flag)
                    bool canPlace = true;
                    for (int i = 0; i < actualWidth; i++)
                    {
                        for (int j = 0; j < actualHeight; j++)
                        {
                            CellUI tempCell = board[x + i, y + j];
                            if (tempCell.Gem != null || tempCell.IsRevealed || tempCell.IsDynamite)
                            {
                                canPlace = false;
                                break;
                            }
                        }
                        if (!canPlace) break;
                    }
                    
                    if (canPlace)
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
    
    // Spawn gem chính xác ở cell được chỉ định (cell đó phải là một phần của gem)
    // Chọn ngẫu nhiên 1 gem, đặt vào, kiểm tra các gem còn lại có thể đặt không, nếu không chọn gem khác
    private bool SpawnGemAtExactCell(CellUI cell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return false;
        
        // Reset exclude flag của cell này để có thể spawn gem
        cell.ResetExcludedFromGemSpawn();
        
        // Xáo trộn danh sách gems để có tính ngẫu nhiên
        List<Gem> shuffledGems = new List<Gem>(unplacedGems);
        for (int i = 0; i < shuffledGems.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffledGems.Count);
            Gem temp = shuffledGems[i];
            shuffledGems[i] = shuffledGems[randomIndex];
            shuffledGems[randomIndex] = temp;
        }
        
        // Thử từng gem ngẫu nhiên
        foreach (Gem gemToSpawn in shuffledGems)
        {
            // Lấy orientation của gem này
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            // Tìm tất cả vị trí có thể đặt gem này mà chứa chính xác cell này
            List<PlacementOption> placementsAtCell = GetPlacementsContainingCell(gemToSpawn, gemConfigData, orientation, cell);
            
            if (placementsAtCell.Count > 0)
            {
                // Xáo trộn placements để có tính ngẫu nhiên
                for (int i = 0; i < placementsAtCell.Count; i++)
                {
                    int randomIndex = Random.Range(i, placementsAtCell.Count);
                    PlacementOption temp = placementsAtCell[i];
                    placementsAtCell[i] = placementsAtCell[randomIndex];
                    placementsAtCell[randomIndex] = temp;
                }
                
                // Thử từng placement để tìm placement thỏa mãn điều kiện
                foreach (var chosenPlacement in placementsAtCell)
                {
                    // Kiểm tra xem sau khi đặt gem này, các gem còn lại (ngoại trừ gem này) có thể được đặt không
                    if (CanPlaceRemainingGemsAfterPlacing(gemToSpawn, chosenPlacement))
                    {
                        // Reset exclude flag của tất cả cells trong placement
                        for (int x = 0; x < chosenPlacement.actualWidth; x++)
                        {
                            for (int y = 0; y < chosenPlacement.actualHeight; y++)
                            {
                                board[chosenPlacement.x + x, chosenPlacement.y + y].ResetExcludedFromGemSpawn();
                            }
                        }
                        
                        // Đặt gem tại vị trí này
                        PlaceGemAt(gemToSpawn, chosenPlacement.x, chosenPlacement.y, config, chosenPlacement.actualWidth, chosenPlacement.actualHeight, chosenPlacement.isRotated);
                        
                        // Xóa khỏi pool và thêm vào hiddenGems
                        unplacedGems.Remove(gemToSpawn);
                        hiddenGems.Add(gemToSpawn);
                        
                        Debug.Log($"Spawned gem {gemToSpawn.GemId} at exact cell ({cell.BoardX}, {cell.BoardY})");
                        return true; // Thành công
                    }
                }
                
                // Nếu không tìm được placement nào thỏa mãn điều kiện cho gem này, thử gem tiếp theo
                Debug.Log($"Gem {gemToSpawn.GemId} cannot be placed at cell ({cell.BoardX}, {cell.BoardY}) - remaining gems cannot be placed. Trying next gem...");
            }
        }
        
        Debug.LogWarning($"Cannot spawn any gem at cell ({cell.BoardX}, {cell.BoardY}) - no gem satisfies the condition");
        return false; // Không thể spawn gem nào ở cell này
    }
    
    // Lấy tất cả vị trí có thể đặt gem mà chứa chính xác cell được chỉ định
    private List<PlacementOption> GetPlacementsContainingCell(Gem gem, GemConfigData gemConfigData, GemOrientation orientation, CellUI targetCell)
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
        else
        {
            rotationOptions = new bool[] { config.height > config.width ? false : true };
        }
        
        foreach (bool isRotated in rotationOptions)
        {
            int actualWidth = isRotated ? config.height : config.width;
            int actualHeight = isRotated ? config.width : config.height;
            
            // Tìm tất cả vị trí có thể đặt gem sao cho targetCell nằm trong gem
            // targetCell phải nằm trong khoảng [x, x+width) và [y, y+height)
            for (int x = Mathf.Max(0, targetCell.BoardX - actualWidth + 1); x <= Mathf.Min(boardWidth - actualWidth, targetCell.BoardX); x++)
            {
                for (int y = Mathf.Max(0, targetCell.BoardY - actualHeight + 1); y <= Mathf.Min(boardHeight - actualHeight, targetCell.BoardY); y++)
                {
                    // Kiểm tra xem targetCell có nằm trong placement này không
                    if (x <= targetCell.BoardX && targetCell.BoardX < x + actualWidth &&
                        y <= targetCell.BoardY && targetCell.BoardY < y + actualHeight)
                    {
                        // Kiểm tra có thể đặt gem không (chỉ kiểm tra gem và revealed, không kiểm tra exclude flag)
                        // Lưu ý: targetCell có thể đã được reveal nhưng vẫn cho phép đặt gem ở đó (vì đó là cell đang được đào)
                        bool canPlace = true;
                        for (int i = 0; i < actualWidth; i++)
                        {
                            for (int j = 0; j < actualHeight; j++)
                            {
                                CellUI tempCell = board[x + i, y + j];
                                // Cho phép đặt gem ở targetCell dù nó đã được reveal (vì đó là cell đang được đào)
                                if (tempCell == targetCell)
                                {
                                    // Chỉ kiểm tra gem, không kiểm tra revealed cho targetCell
                                    if (tempCell.Gem != null)
                                    {
                                        canPlace = false;
                                        break;
                                    }
                                }
                                else
                                {
                                    // Các cell khác: kiểm tra cả gem, revealed và dynamite
                                    if (tempCell.Gem != null || tempCell.IsRevealed || tempCell.IsDynamite)
                                    {
                                        canPlace = false;
                                        break;
                                    }
                                }
                            }
                            if (!canPlace) break;
                        }
                        
                        if (canPlace)
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
        }
        
        return validPlacements;
    }
    
    private bool SpawnRandomGemFromPool(CellUI preferredCell = null)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return false;
        
        // Kiểm tra xem các gem còn lại có thể được đặt không (trước khi spawn gem mới)
        // Nếu không thể đặt được, vẫn thử spawn nếu có preferredCell (để đảm bảo có thể spawn ở cell đó)
        if (!CanPlaceRemainingGems() && preferredCell == null)
        {
            Debug.LogWarning("Cannot spawn gem - remaining gems may not fit on board. Will retry later.");
            return false;
            }
        
        // Chọn một gem ngẫu nhiên từ pool
        int randomIndex = Random.Range(0, unplacedGems.Count);
        Gem gemToSpawn = unplacedGems[randomIndex];
        
        // Lấy orientation của gem này
        GemOrientation orientation = GemOrientation.Horizontal;
        if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
        {
            orientation = gemOrientations[gemToSpawn];
        }
        
        // Thử đặt gem này lên board, ưu tiên đặt ở preferredCell nếu có
        if (PlaceSingleGem(gemToSpawn, gemConfigData, orientation, preferredCell))
        {
            // Thành công: xóa khỏi pool và thêm vào hiddenGems
            unplacedGems.RemoveAt(randomIndex);
            hiddenGems.Add(gemToSpawn);
            Debug.Log($"Spawned gem {gemToSpawn.GemId} from pool. Remaining: {unplacedGems.Count}");
            return true;
        }
        
        // Không thể đặt gem này (board đã đầy hoặc không đủ chỗ)
        // Không spawn gem này, để lại trong pool để thử lại sau
        Debug.LogWarning($"Cannot spawn gem {gemToSpawn.GemId} - no valid placement found. Will retry later.");
        return false;
    }
    
    private bool PlaceSingleGem(Gem gem, GemConfigData configData, GemOrientation orientation, CellUI preferredCell = null)
    {
        var config = configData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Thu thập tất cả vị trí có thể đặt gem
        List<PlacementOption> validPlacements = GetAllValidPlacements(gem, configData, orientation);
        
        if (validPlacements.Count == 0)
            return false;
        
        // Nếu có preferredCell, ưu tiên các vị trí chứa cell đó
        if (preferredCell != null)
        {
            List<PlacementOption> preferredPlacements = new List<PlacementOption>();
            List<PlacementOption> otherPlacements = new List<PlacementOption>();
            
            foreach (var placement in validPlacements)
            {
                // Kiểm tra xem placement này có chứa preferredCell không
                bool containsPreferredCell = false;
                for (int i = 0; i < placement.actualWidth; i++)
                {
                    for (int j = 0; j < placement.actualHeight; j++)
                    {
                        int cellX = placement.x + i;
                        int cellY = placement.y + j;
                        if (cellX == preferredCell.BoardX && cellY == preferredCell.BoardY)
                        {
                            containsPreferredCell = true;
                            break;
                        }
                    }
                    if (containsPreferredCell) break;
                }
                
                if (containsPreferredCell)
                {
                    preferredPlacements.Add(placement);
                }
                else
                {
                    otherPlacements.Add(placement);
                }
            }
            
            // Ưu tiên sử dụng các placement chứa preferredCell
            if (preferredPlacements.Count > 0)
            {
                validPlacements = preferredPlacements;
            }
        }
        
        // Xáo trộn để có tính ngẫu nhiên
        for (int i = 0; i < validPlacements.Count; i++)
        {
            int randomIndex = Random.Range(i, validPlacements.Count);
            PlacementOption temp = validPlacements[i];
            validPlacements[i] = validPlacements[randomIndex];
            validPlacements[randomIndex] = temp;
        }
        
        // Thử từng placement để tìm placement thỏa mãn điều kiện
        foreach (var chosenPlacement in validPlacements)
        {
            // Kiểm tra xem sau khi đặt gem này, các gem còn lại có thể được đặt không
            if (CanPlaceRemainingGemsAfterPlacing(gem, chosenPlacement))
            {
                // Đặt gem tại vị trí này
                PlaceGemAt(gem, chosenPlacement.x, chosenPlacement.y, config, chosenPlacement.actualWidth, chosenPlacement.actualHeight, chosenPlacement.isRotated);
                return true;
            }
        }
        
        // Không tìm được placement nào thỏa mãn điều kiện
        return false;
    }
    
    // Kiểm tra xem các gem còn lại trong pool có thể được đặt trên board không
    // (không tính cell được chỉ định - giả định cell đó không có gem)
    private bool CanPlaceRemainingGemsIgnoringCell(CellUI ignoredCell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0)
            return true; // Không còn gem nào, coi như có thể đặt
        
        if (gemConfigData == null)
            return false;
        
        // Tạo một bản sao board để test (giả định ignoredCell không có gem)
        bool[,] testBoard = new bool[boardWidth, boardHeight];
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                CellUI cell = board[x, y];
                // Cell trống nếu: không có gem, không phải dynamite, không bị exclude, chưa được reveal, và không phải ignoredCell
                bool isEmpty = cell.Gem == null && !cell.IsDynamite && !cell.IsExcludedFromGemSpawn && !cell.IsRevealed;
                if (cell == ignoredCell)
                {
                    // Giả định ignoredCell không có gem (không spawn gem ở đó)
                    // Nhưng vẫn tính đến flag IsExcludedFromGemSpawn và IsRevealed nếu có
                    isEmpty = !cell.IsExcludedFromGemSpawn && !cell.IsRevealed;
                }
                testBoard[x, y] = isEmpty;
            }
        }
        
        // Thử đặt tất cả gems còn lại lên test board
        List<Gem> gemsToPlace = new List<Gem>(unplacedGems);
        return CanPlaceAllGemsOnTestBoard(gemsToPlace, testBoard);
    }
    
    // Kiểm tra xem có thể đặt tất cả gems lên test board không (sử dụng backtracking)
    // Tối ưu: Early exit, giảm allocation, giới hạn số lần thử, xáo trộn vị trí ngẫu nhiên
    private const int MAX_BACKTRACK_ATTEMPTS = 10000; // Giới hạn số lần thử để tránh quá lâu
    
    private bool CanPlaceAllGemsOnTestBoard(List<Gem> gems, bool[,] testBoard)
    {
        if (gems.Count == 0)
            return true;
        
        // Early exit: Kiểm tra diện tích trước (tối ưu quan trọng nhất)
        int totalGemArea = 0;
        int availableArea = 0;
        
        // Đếm diện tích trống trên test board một lần
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (testBoard[x, y]) availableArea++;
            }
        }
        
        // Tính tổng diện tích gems
        foreach (var g in gems)
        {
            var cfg = gemConfigData?.GetGemConfig(g.GemId);
            if (cfg == null) continue;
            GemOrientation ori = gemOrientations != null && gemOrientations.ContainsKey(g) 
                ? gemOrientations[g] : GemOrientation.Horizontal;
            int area = GetGemArea(cfg, ori);
            totalGemArea += area;
        }
        
        // Nếu diện tích gem lớn hơn diện tích trống, không thể đặt
        if (totalGemArea > availableArea)
            return false;
        
        // Không sắp xếp để giữ tính ngẫu nhiên, chỉ dùng đệ quy tối ưu với giới hạn
        int attemptCount = 0;
        return CanPlaceAllGemsOnTestBoardRecursive(gems, 0, testBoard, ref attemptCount);
    }
    
    // Helper để tính diện tích gem
    private int GetGemArea(GemConfig config, GemOrientation orientation)
    {
        if (config.width == config.height)
            return config.width * config.height;
        
        if (orientation == GemOrientation.Horizontal)
            return (config.width > config.height) ? config.width * config.height : config.height * config.width;
        else
            return (config.height > config.width) ? config.width * config.height : config.height * config.width;
    }
    
    // Hàm backtracking đệ quy đã tối ưu với giới hạn số lần thử và xáo trộn vị trí ngẫu nhiên
    private bool CanPlaceAllGemsOnTestBoardRecursive(List<Gem> gems, int index, bool[,] testBoard, ref int attemptCount)
    {
        // Giới hạn số lần thử để tránh quá lâu
        if (attemptCount >= MAX_BACKTRACK_ATTEMPTS)
            return false;
        
        if (index >= gems.Count)
            return true;
        
        Gem gem = gems[index];
        GemOrientation orientation = GemOrientation.Horizontal;
        if (gemOrientations != null && gemOrientations.ContainsKey(gem))
        {
            orientation = gemOrientations[gem];
        }
        
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Xác định các hướng xoay dựa trên orientation
        bool canRotate = config.width != config.height;
        bool isRotated = false;
        
        if (canRotate)
        {
            if (orientation == GemOrientation.Horizontal)
                isRotated = config.width <= config.height;
            else
                isRotated = config.height <= config.width;
        }
        
        int actualWidth = isRotated ? config.height : config.width;
        int actualHeight = isRotated ? config.width : config.height;
        
        // Tạo danh sách vị trí có thể và xáo trộn ngẫu nhiên để giữ tính ngẫu nhiên
        List<Vector2Int> validPositions = new List<Vector2Int>();
        for (int x = 0; x <= boardWidth - actualWidth; x++)
        {
            for (int y = 0; y <= boardHeight - actualHeight; y++)
            {
                if (CanPlaceGemOnTestBoard(x, y, actualWidth, actualHeight, testBoard))
                {
                    validPositions.Add(new Vector2Int(x, y));
                }
            }
        }
        
        // Xáo trộn ngẫu nhiên các vị trí để giữ tính ngẫu nhiên
        for (int i = 0; i < validPositions.Count; i++)
        {
            int randomIndex = Random.Range(i, validPositions.Count);
            Vector2Int temp = validPositions[i];
            validPositions[i] = validPositions[randomIndex];
            validPositions[randomIndex] = temp;
        }
        
        // Thử đặt gem này ở các vị trí đã xáo trộn
        foreach (var pos in validPositions)
        {
            attemptCount++;
            if (attemptCount >= MAX_BACKTRACK_ATTEMPTS)
                return false;
            
            int x = pos.x;
            int y = pos.y;
            
            // Đặt gem tạm thời
            PlaceGemOnTestBoard(x, y, actualWidth, actualHeight, testBoard, false);
            
            // Đệ quy đặt gem tiếp theo (không tạo List mới)
            if (CanPlaceAllGemsOnTestBoardRecursive(gems, index + 1, testBoard, ref attemptCount))
            {
                return true; // Thành công!
            }
            
            // Backtrack: xóa gem
            PlaceGemOnTestBoard(x, y, actualWidth, actualHeight, testBoard, true);
        }
        
        return false; // Không thể đặt
    }
    
    // Tối ưu: Early exit ngay khi gặp cell không trống
    private bool CanPlaceGemOnTestBoard(int x, int y, int width, int height, bool[,] testBoard)
    {
        // Kiểm tra boundary trước
        if (x + width > boardWidth || y + height > boardHeight)
            return false;
        
        // Early exit: kiểm tra từng cell và return false ngay khi gặp cell không trống
        int endX = x + width;
        int endY = y + height;
        for (int i = x; i < endX; i++)
        {
            for (int j = y; j < endY; j++)
            {
                if (!testBoard[i, j])
                    return false;
            }
        }
        return true;
    }
    
    private void PlaceGemOnTestBoard(int x, int y, int width, int height, bool[,] testBoard, bool isEmpty)
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                testBoard[x + i, y + j] = isEmpty;
            }
        }
    }
    
    // Kiểm tra xem sau khi đặt một gem ở vị trí cụ thể, các gem còn lại có thể được đặt không
    private bool CanPlaceRemainingGemsAfterPlacing(Gem gemToPlace, PlacementOption placement)
    {
        if (unplacedGems == null || unplacedGems.Count == 0)
            return true; // Không còn gem nào, coi như có thể đặt
        
        if (gemConfigData == null)
            return false;
        
        // Tạo một bản sao board để test (giả định gem đã được đặt ở placement)
        bool[,] testBoard = new bool[boardWidth, boardHeight];
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                CellUI cell = board[x, y];
                // Cell trống nếu: không có gem, không phải dynamite, không bị exclude
                bool isEmpty = cell.Gem == null && !cell.IsDynamite && !cell.IsExcludedFromGemSpawn;
                testBoard[x, y] = isEmpty;
            }
        }
        
        // Đánh dấu vùng của gem sẽ được đặt là không trống
        for (int x = 0; x < placement.actualWidth; x++)
        {
            for (int y = 0; y < placement.actualHeight; y++)
            {
                testBoard[placement.x + x, placement.y + y] = false;
            }
        }
        
        // Tạo danh sách gems còn lại (loại bỏ gem sẽ được đặt)
        List<Gem> remainingGems = new List<Gem>(unplacedGems);
        remainingGems.Remove(gemToPlace);
        
        // Thử đặt tất cả gems còn lại lên test board
        return CanPlaceAllGemsOnTestBoard(remainingGems, testBoard);
    }
    
    // Kiểm tra xem các gem còn lại trong pool có thể được đặt trên board không
    private bool CanPlaceRemainingGems()
    {
        if (unplacedGems == null || unplacedGems.Count == 0)
            return true; // Không còn gem nào, coi như có thể đặt
        
        // Tính tổng diện tích các gem còn lại
        int totalRemainingArea = 0;
        foreach (var gem in unplacedGems)
        {
            var config = gemConfigData?.GetGemConfig(gem.GemId);
            if (config == null) continue;
            
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gem))
            {
                orientation = gemOrientations[gem];
            }
            
            int gemArea;
            if (config.width == config.height)
            {
                gemArea = config.width * config.height;
            }
            else if (orientation == GemOrientation.Horizontal)
            {
                gemArea = (config.width > config.height) ? 
                    config.width * config.height : 
                    config.height * config.width;
            }
            else
            {
                gemArea = (config.height > config.width) ? 
                    config.width * config.height : 
                    config.height * config.width;
            }
            
            totalRemainingArea += gemArea;
        }
        
        // Tính diện tích còn trống trên board
        int availableArea = 0;
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (board[x, y].Gem == null && !board[x, y].IsDynamite)
                {
                    availableArea++;
                }
            }
        }
        
        // Kiểm tra xem có đủ chỗ không (cho phép một chút buffer)
        return totalRemainingArea <= availableArea * 1.1f; // Cho phép 10% buffer
    }
    
    private void ExplodeDynamite(int x, int y)
    {
        // Spawn explosion particle effect
        SpawnExplosionEffect(x, y);
        
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
    
    // Spawn explosion particle effect tại vị trí dynamite
    private void SpawnExplosionEffect(int x, int y)
    {
        if (explosionParticlePrefab == null || boardParentUI == null)
        {
            Debug.LogWarning("Explosion particle prefab or board parent UI is null!");
            return;
        }
        
        // Tính toán vị trí của cell trong Canvas
        CellUI cell = board[x, y];
        if (cell == null)
        {
            Debug.LogWarning($"Cell at ({x}, {y}) is null!");
            return;
        }
        
        RectTransform cellRect = cell.GetComponent<RectTransform>();
        if (cellRect == null)
        {
            Debug.LogWarning($"Cell RectTransform is null!");
            return;
        }
        
        // Tạo particle system instance (KHÔNG đặt trong Canvas, đặt trong scene root)
        ParticleSystem explosion = Instantiate(explosionParticlePrefab);
        
        // Với Screen Space Overlay Canvas, Particle System cần được đặt trong World Space
        // Convert UI position sang world position
        Canvas canvas = boardParentUI.GetComponentInParent<Canvas>();
        Vector3 worldPosition = Vector3.zero;
        
        // Lấy camera chính (dùng chung cho cả function)
        Camera mainCamera = Camera.main ?? FindObjectOfType<Camera>();
        
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Screen Space Overlay: convert screen position sang world position
                if (mainCamera != null)
                {
                    // Convert UI world position sang screen point, rồi sang world position ở một distance nhất định
                    Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(null, cellRect.position);
                    // Đặt particle ở một khoảng cách trước camera (ví dụ: 10 units)
                    worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, mainCamera.nearClipPlane + 5f));
                }
                else
                {
                    // Fallback: dùng position trực tiếp
                    worldPosition = cellRect.position;
                }
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // Screen Space Camera: convert sang world position của camera
                if (canvas.worldCamera != null)
                {
                    RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        cellRect,
                        RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, cellRect.position),
                        canvas.worldCamera,
                        out worldPosition);
                }
                else
                {
                    worldPosition = cellRect.position;
                }
            }
            else
            {
                // World Space: dùng position trực tiếp
                worldPosition = cellRect.position;
            }
        }
        else
        {
            // Fallback: dùng position trực tiếp
            worldPosition = cellRect.position;
        }
        
        // Đặt particle system tại vị trí đã tính toán
        explosion.transform.position = worldPosition;
        
        // Đảm bảo Particle System render đúng cho game 2D
        var renderer = explosion.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            // Render mode phù hợp với 2D
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            
            // Đảm bảo sorting layer và order để hiển thị trên UI
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 100; // Render trên UI
            
            // Đảm bảo renderer được enable
            renderer.enabled = true;
        }
        
        // Đảm bảo Particle System được đặt ở đúng depth với camera 2D
        if (mainCamera != null)
        {
            // Với orthographic camera (2D), đặt particle ở cùng Z với camera hoặc gần hơn một chút
            Vector3 pos = worldPosition;
            if (mainCamera.orthographic)
            {
                // Orthographic camera: đặt ở cùng Z với camera hoặc gần hơn một chút
                pos.z = mainCamera.transform.position.z + 1f;
            }
            else
            {
                // Perspective camera: đặt ở một khoảng cách hợp lý
                pos.z = mainCamera.nearClipPlane + 5f;
            }
            explosion.transform.position = pos;
            worldPosition = pos; // Update cho debug log
        }
        
        // Play particle system
        explosion.Play();
        
        // Debug: Hiển thị thông tin vị trí
        Debug.Log($"Explosion Particle spawned at cell ({x}, {y}):\n" +
                  $"  - Cell UI position: {cellRect.position}\n" +
                  $"  - Cell anchored position: {cellRect.anchoredPosition}\n" +
                  $"  - World position (Particle): {worldPosition}\n" +
                  $"  - Canvas render mode: {(canvas != null ? canvas.renderMode.ToString() : "null")}\n" +
                  $"  - Camera: {(mainCamera != null ? mainCamera.name : "null")}\n" +
                  $"  - Camera type: {(mainCamera != null ? (mainCamera.orthographic ? "Orthographic" : "Perspective") : "null")}\n" +
                  $"  - Particle Z position: {explosion.transform.position.z}");
        
        // Tự động destroy sau khi effect kết thúc
        Destroy(explosion.gameObject, explosionParticleDuration);
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
    
    public int GetTotalGemsNeeded()
    {
        return (unplacedGems?.Count ?? 0) + (hiddenGems?.Count ?? 0);
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

