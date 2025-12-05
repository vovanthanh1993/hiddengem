using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private CellUI cellUIPrefab; // Prefab for UI cell
    [SerializeField] private RectTransform boardParentUI; // Parent in Canvas for UI cells
    [SerializeField] private int cellSize = 100; // Size of each cell (100x100 pixels)
    
    [Header("Explosion Effects")]
    [SerializeField] private ParticleSystem explosionParticlePrefab; // Prefab for explosion particle system
    [SerializeField] private float explosionParticleDuration = 1.5f; // Duration of explosion effect
    
    [Header("Digging Settings")]
    [SerializeField] private float digCooldown = 0.3f; // Time interval between each dig (seconds)
    
    private CellUI[,] board;
    private float lastDigTime = 0f; // Time of last dig
    private int boardWidth;
    private int boardHeight;
    private List<Gem> hiddenGems;
    private List<Gem> unplacedGems; // Pool of gems not yet placed on board
    private List<Vector2Int> dynamitePositions;
    private GemConfigData gemConfigData;
    private Dictionary<Gem, GemOrientation> gemOrientations;
    private bool isInputEnabled = true; // Flag to control whether digging is allowed
    
    public event System.Action<Gem> OnGemCollected; // Event when gem is collected
    
    public CellUI[,] Board => board;
    public int BoardWidth => boardWidth;
    public int BoardHeight => boardHeight;
    
    public void SetInputEnabled(bool enabled)
    {
        isInputEnabled = enabled;
    }
    
    public void SetBoardVisible(bool visible)
    {
        if (boardParentUI != null)
        {
            boardParentUI.gameObject.SetActive(visible);
        }
    }
    
    public void InitializeBoard(int width, int height)
    {
        boardWidth = width;
        boardHeight = height;
        board = new CellUI[width, height];
        hiddenGems = new List<Gem>();
        unplacedGems = new List<Gem>();
        dynamitePositions = new List<Vector2Int>();
        isInputEnabled = true; // Enable input when initializing new board
        
        // Create boardParentUI if not exists
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
        
        // Update board size
        boardParentUI.sizeDelta = new Vector2(width * cellSize, height * cellSize);
        
        // Clear existing cells
        foreach (Transform child in boardParentUI)
        {
            Destroy(child.gameObject);
        }
        
        // Create cells with GridLayoutGroup for automatic arrangement
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
        
        // Set random stone layers for all cells (1 or 2 layers)
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
        // Clear old gems from board and destroy old gem GameObjects
        ClearAllGems();
        
        // Destroy old gem GameObjects from unplacedGems and hiddenGems
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
        
        // Store list of unplaced gems in pool (not placed on board immediately)
        unplacedGems = new List<Gem>(gems);
        gemConfigData = configData;
        gemOrientations = orientations ?? new Dictionary<Gem, GemOrientation>();
        hiddenGems.Clear();
        
        Debug.Log($"Initialized gem pool with {unplacedGems.Count} gems (not placed on board yet)");
    }
    
    public bool PlaceGemsRandomly(List<Gem> gems, GemConfigData gemConfigData, Dictionary<Gem, GemOrientation> gemOrientations = null)
    {
        hiddenGems.Clear();
        
        // Validation: Check if board has enough space
        if (!ValidateBoardCapacity(gems, gemConfigData, gemOrientations))
        {
            Debug.LogError("Board is too small to fit all gems!");
            return false;
        }
        
        // Retry mechanism: Try multiple times with different arrangements
        int maxRetries = 50;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            // Clear board before each attempt
            ClearAllGems();
            
            // Sort gems by size (largest first) for easier placement
            List<Gem> sortedGems = new List<Gem>(gems);
            
            // Shuffle a bit to have different arrangements
            if (retry > 0)
            {
                // Random shuffle but still prioritize large gems
                sortedGems.Sort((a, b) => 
                {
                    int sizeA = a.Width * a.Height;
                    int sizeB = b.Width * b.Height;
                    int sizeCompare = sizeB.CompareTo(sizeA);
                    // If same size, random shuffle
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
                    return sizeB.CompareTo(sizeA); // Largest first
                });
            }
            
            // Try placing all gems with backtracking
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
            
            // Calculate gem area after rotating according to orientation
            int gemArea;
            if (config.width == config.height)
            {
                gemArea = config.width * config.height;
            }
            else if (orientation == GemOrientation.Horizontal)
            {
                // Horizontal: width > height
                gemArea = (config.width > config.height) ? 
                    config.width * config.height : 
                    config.height * config.width;
            }
            else // Vertical
            {
                // Vertical: height > width
                gemArea = (config.height > config.width) ? 
                    config.width * config.height : 
                    config.height * config.width;
            }
            
            totalGemArea += gemArea;
        }
        
        // Board must have area larger than total gem area (need buffer for dynamite and spacing)
        if (totalGemArea > totalBoardArea * 0.9f) // Allow 90% of board to be used for gems
        {
            Debug.LogWarning($"Board capacity warning: Total gem area ({totalGemArea}) vs Board area ({totalBoardArea})");
            return false;
        }
        
        return true;
    }
    
    private void ClearAllGems()
    {
        // Remove all gems from board
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
        // If all gems are placed
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
        
        // Get all possible positions to place this gem
        List<PlacementOption> validPlacements = GetAllValidPlacements(gem, gemConfigData, orientation);
        
        // Shuffle for randomness
        for (int i = 0; i < validPlacements.Count; i++)
        {
            int randomIndex = Random.Range(i, validPlacements.Count);
            PlacementOption temp = validPlacements[i];
            validPlacements[i] = validPlacements[randomIndex];
            validPlacements[randomIndex] = temp;
        }
        
        // Try each position
        foreach (var placement in validPlacements)
        {
            var config = gemConfigData.GetGemConfig(gem.GemId);
            if (config == null) continue;
            
            // Place gem at this position
            PlaceGemAt(gem, placement.x, placement.y, config, placement.actualWidth, placement.actualHeight, placement.isRotated);
            hiddenGems.Add(gem);
            
            // Recursively place next gem
            if (PlaceGemsRecursive(gems, index + 1, gemConfigData, gemOrientations))
            {
                return true; // Success!
            }
            
            // If not successful, backtrack: remove this gem
            RemoveGem(gem);
            hiddenGems.Remove(gem);
        }
        
        return false; // Could not find a way to place
    }
    
    public void RemoveGem(Gem gem)
    {
        // Remove gem from all cells
        if (gem.Cells != null)
        {
            foreach (var cell in gem.Cells)
            {
                if (cell != null)
                {
                    cell.SetGem(null);
                    // Reset cell to initial state (stone)
                    cell.SetStoneLayers(Random.Range(0, 2) == 0 ? 1 : 2);
                }
            }
            // Clear gem's cell list
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
        
        // Collect all possible positions to place gem
        List<PlacementOption> validPlacements = new List<PlacementOption>();
        
        // Determine rotation directions based on specified orientation
        bool canRotate = config.width != config.height;
        bool[] rotationOptions;
        
        if (!canRotate)
        {
            // Square gem, cannot rotate
            rotationOptions = new bool[] { false };
        }
        else if (orientation == GemOrientation.Horizontal)
        {
            // Horizontal: width > height after rotation
            // If original width > original height then don't rotate, otherwise rotate
            rotationOptions = new bool[] { config.width > config.height ? false : true };
        }
        else // Vertical
        {
            // Vertical: height > width after rotation
            // If original height > original width then don't rotate, otherwise rotate
            rotationOptions = new bool[] { config.height > config.width ? false : true };
        }
        
        foreach (bool isRotated in rotationOptions)
        {
            int actualWidth = isRotated ? config.height : config.width;
            int actualHeight = isRotated ? config.width : config.height;
            
            // Check all possible positions
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
        
        // If there are valid positions, randomly choose one
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
        // Fallback: try again with more attempts and different approach
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return false;
        
        // Create list of all possible positions in random order
        List<PlacementOption> allOptions = new List<PlacementOption>();
        
        // Determine rotation directions based on specified orientation
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
        
        // Shuffle list
        for (int i = 0; i < allOptions.Count; i++)
        {
            PlacementOption temp = allOptions[i];
            int randomIndex = Random.Range(i, allOptions.Count);
            allOptions[i] = allOptions[randomIndex];
            allOptions[randomIndex] = temp;
        }
        
        // Try each position
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
                // Cannot place gem if:
                // - Cell already has gem
                // - Cell is excluded from gem spawn
                // - Cell is revealed (dug before)
                // - Cell has dynamite
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
                
                // Calculate position of cell in original gem sprite
                // If gem is rotated, need to remap position
                // When rotated: (i, j) in board -> (j, i) in original sprite
                // Example: gem 1x3 rotated to 3x1
                //   - Cell (0,0) in board -> (0, 0) in original sprite
                //   - Cell (1,0) in board -> (0, 1) in original sprite
                //   - Cell (2,0) in board -> (0, 2) in original sprite
                int gemCellX, gemCellY;
                if (isRotated)
                {
                    // Gem is rotated: swap i and j
                    gemCellX = j; // j in board -> X in original sprite
                    gemCellY = i; // i in board -> Y in original sprite
                }
                else
                {
                    gemCellX = i;
                    gemCellY = j;
                }
                
                // Set gem info for cell with actualWidth and actualHeight (rotated)
                // But need to pass original width and height for sprite to display correctly
                cell.SetGemInfo(gem, gemCellX, gemCellY, config.width, config.height, actualWidth, actualHeight, isRotated);
                
                // Don't change stone layers of cell when spawning gem - keep original stone layers
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
        // Check if digging is allowed
        if (!isInputEnabled)
        {
            return false;
        }
        
        if (x < 0 || x >= boardWidth || y < 0 || y >= boardHeight)
            return false;
        
        // Check cooldown between digs
        float currentTime = Time.time;
        if (currentTime - lastDigTime < digCooldown)
        {
            return false; // Not enough time, don't allow digging
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
        
        // Update last dig time
        lastDigTime = currentTime;
        
        PickaxeManager.Instance.UsePickaxe(pickaxesNeeded);
        cell.Dig();
        
        // Check if hit dynamite
        if (cell.IsDynamite)
        {
            ExplodeDynamite(x, y);
        }
        
        // Process gem reveal with 30% chance
        ProcessGemReveal(cell);
        
        return true;
    }
    
    private void ProcessGemReveal(CellUI cell)
    {
        // If cell already has gem (spawned before), just check reveal
        if (cell.Gem != null && !cell.Gem.IsCollected)
        {
            // Gem hit - check if fully revealed
            if (cell.Gem.IsFullyRevealed() && !cell.Gem.IsCollected)
            {
                // Play sound when collecting full gem
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayGemCollectSound();
                }
                
                cell.Gem.Collect();
                // Notify gem collected event
                OnGemCollected?.Invoke(cell.Gem);
            }
            else
            {
                // Play sound when finding part of gem (not fully revealed yet)
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayGemFoundSound();
                }
            }
            return;
        }
        
        // If cell doesn't have gem yet, try spawning new gem from pool
        // Check gem hit ratio (30% chance)
        bool hitGem = Random.Range(0f, 1f) < 0.3f;
        
        // If hitGem = false, must check if remaining gems can be placed on board
        // If cannot be placed, must force hitGem = true
        if (!hitGem && unplacedGems.Count > 0)
        {
            // Check if remaining gems can be placed on board
            // (assume this cell has no gem - i.e., don't spawn gem here)
            if (!CanPlaceRemainingGemsIgnoringCell(cell))
            {
                // If cannot place all remaining gems, must force hit to spawn gem at this cell
                hitGem = true;
                Debug.Log($"Force hit: Cannot place remaining {unplacedGems.Count} gems without using this cell");
            }
        }
        
        Debug.Log("hitGem: " + hitGem);
        
        if (hitGem && unplacedGems.Count > 0)
        {
            // Check if remaining gems can be placed if not spawning gem at this cell
            bool canPlaceRemainingWithoutThisCell = CanPlaceRemainingGemsIgnoringCell(cell);
            
            // Spawn a random gem from pool, MUST place exactly at the cell just dug
            bool spawnSuccess = SpawnGemAtExactCell(cell);
            
            // Play sound when spawn gem succeeds (found part of gem)
            if (spawnSuccess && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGemFoundSound();
            }
            
            // If spawn fails, must force spawn if:
            // 1. Remaining gems cannot be placed if not spawning gem at this cell, OR
            // 2. This is a force hit (forced from false to true)
            if (!spawnSuccess && unplacedGems.Count > 0)
            {
                if (!canPlaceRemainingWithoutThisCell)
                {
                    // Must force spawn gem at this cell (ignore condition that remaining gems can be placed)
                    Debug.LogWarning("Spawn failed but remaining gems cannot be placed - forcing spawn at this cell");
                    ForceSpawnGemAtCellIgnoringRemainingGems(cell);
                    
                    // Play sound when force spawn (found part of gem)
                    if (cell.Gem != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayGemFoundSound();
                    }
                }
                else
                {
                    Debug.LogWarning($"Spawn failed at cell ({cell.BoardX}, {cell.BoardY}) but remaining gems can be placed");
                    
                    // Play digging missed sound when spawn failed but remaining gems can be placed
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayDiggingSound();
                    }
                }
            }
        }
        else if (!hitGem)
        {
            // Play sound when digging misses
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDiggingSound();
            }
            
            // Only mark this cell as excluded when certain that remaining gems can be placed
            // (This logic has been checked above, if cannot place then hitGem has been forced = true)
            cell.MarkAsExcludedFromGemSpawn();
        }
    }
    
    // Force spawn gem at cell without checking condition that remaining gems can be placed
    // But still use backtracking to ensure remaining gems can be placed
    private void ForceSpawnGemAtCellIgnoringRemainingGems(CellUI cell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return;
        
        // Reset exclude flag of this cell to allow gem spawn
        cell.ResetExcludedFromGemSpawn();
        
        // Shuffle gem list for randomness
        List<Gem> shuffledGems = new List<Gem>(unplacedGems);
        for (int i = 0; i < shuffledGems.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffledGems.Count);
            Gem temp = shuffledGems[i];
            shuffledGems[i] = shuffledGems[randomIndex];
            shuffledGems[randomIndex] = temp;
        }
        
        // Try spawning each gem randomly
        foreach (Gem gemToSpawn in shuffledGems)
        {
            // Get orientation of this gem
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            // Priority: Find all positions that can place this gem containing exactly this cell
            List<PlacementOption> placementsAtCell = GetPlacementsContainingCell(gemToSpawn, gemConfigData, orientation, cell);
            
            // Shuffle placements for randomness
            for (int i = 0; i < placementsAtCell.Count; i++)
            {
                int randomIndex = Random.Range(i, placementsAtCell.Count);
                PlacementOption temp = placementsAtCell[i];
                placementsAtCell[i] = placementsAtCell[randomIndex];
                placementsAtCell[randomIndex] = temp;
            }
            
            // Try each placement containing this cell
            foreach (var placement in placementsAtCell)
            {
                // Check if after placing this gem, remaining gems can be placed (using backtracking)
                if (CanPlaceRemainingGemsAfterPlacing(gemToSpawn, placement))
                {
                    // Reset exclude flag of all cells in placement
                    for (int x = 0; x < placement.actualWidth; x++)
                    {
                        for (int y = 0; y < placement.actualHeight; y++)
                        {
                            board[placement.x + x, placement.y + y].ResetExcludedFromGemSpawn();
                        }
                    }
                    
                    // Place gem at this position
                    PlaceGemAt(gemToSpawn, placement.x, placement.y, config, placement.actualWidth, placement.actualHeight, placement.isRotated);
                    
                    // Remove from pool and add to hiddenGems
                    unplacedGems.Remove(gemToSpawn);
                    hiddenGems.Add(gemToSpawn);
                    
                    Debug.Log($"Force spawned gem {gemToSpawn.GemId} at cell ({cell.BoardX}, {cell.BoardY}) using backtracking");
                    return; // Success
                }
            }
        }
        
        // If cannot find any gem that can be placed at this cell while ensuring remaining gems can be placed,
        // try placing gem at any valid position (fallback)
        Debug.LogWarning($"Cannot spawn any gem at cell ({cell.BoardX}, {cell.BoardY}) - trying fallback to any valid position");
        
        // Sort gems by size (largest first) for more efficient backtracking
        List<Gem> sortedGems = new List<Gem>(shuffledGems);
        sortedGems.Sort((a, b) =>
        {
            var cfgA = gemConfigData?.GetGemConfig(a.GemId);
            var cfgB = gemConfigData?.GetGemConfig(b.GemId);
            if (cfgA == null || cfgB == null) return 0;
            
            GemOrientation oriA = gemOrientations != null && gemOrientations.ContainsKey(a) 
                ? gemOrientations[a] : GemOrientation.Horizontal;
            GemOrientation oriB = gemOrientations != null && gemOrientations.ContainsKey(b) 
                ? gemOrientations[b] : GemOrientation.Horizontal;
            
            int areaA = GetGemArea(cfgA, oriA);
            int areaB = GetGemArea(cfgB, oriB);
            
            return areaB.CompareTo(areaA); // Largest first
        });
        
        foreach (Gem gemToSpawn in sortedGems)
        {
            // Get orientation of this gem
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            // Find all positions that can place this gem (ignore exclude flag)
            List<PlacementOption> allPlacements = GetAllValidPlacementsIgnoringExclude(gemToSpawn, gemConfigData, orientation);
            
            // Shuffle placements for randomness
            for (int i = 0; i < allPlacements.Count; i++)
            {
                int randomIndex = Random.Range(i, allPlacements.Count);
                PlacementOption temp = allPlacements[i];
                allPlacements[i] = allPlacements[randomIndex];
                allPlacements[randomIndex] = temp;
            }
            
            // Try each placement and check with backtracking
            foreach (var placement in allPlacements)
            {
                // Check if after placing this gem, remaining gems can be placed
                if (CanPlaceRemainingGemsAfterPlacing(gemToSpawn, placement))
                {
                    // Reset exclude flag of all cells in placement
                    for (int x = 0; x < placement.actualWidth; x++)
                    {
                        for (int y = 0; y < placement.actualHeight; y++)
                        {
                            board[placement.x + x, placement.y + y].ResetExcludedFromGemSpawn();
                        }
                    }
                    
                    // Place gem at this position
                    PlaceGemAt(gemToSpawn, placement.x, placement.y, config, placement.actualWidth, placement.actualHeight, placement.isRotated);
                    
                    // Remove from pool and add to hiddenGems
                    unplacedGems.Remove(gemToSpawn);
                    hiddenGems.Add(gemToSpawn);
                    
                    Debug.Log($"Force spawned gem {gemToSpawn.GemId} at position ({placement.x}, {placement.y}) - fallback from cell ({cell.BoardX}, {cell.BoardY})");
                    return; // Success
                }
            }
        }
        
        // Final fallback: if backtracking finds no solution, still spawn first possible gem
        // to avoid deadlock (extremely rare case when board truly cannot be placed)
        Debug.LogError($"Backtracking found no solution for cell ({cell.BoardX}, {cell.BoardY}) - using emergency fallback");
        
        foreach (Gem gemToSpawn in shuffledGems)
        {
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            List<PlacementOption> allPlacements = GetAllValidPlacementsIgnoringExclude(gemToSpawn, gemConfigData, orientation);
            if (allPlacements.Count > 0)
            {
                var emergencyPlacement = allPlacements[Random.Range(0, allPlacements.Count)];
                
                // Reset exclude flag of all cells in placement
                for (int x = 0; x < emergencyPlacement.actualWidth; x++)
                {
                    for (int y = 0; y < emergencyPlacement.actualHeight; y++)
                    {
                        board[emergencyPlacement.x + x, emergencyPlacement.y + y].ResetExcludedFromGemSpawn();
                    }
                }
                
                // Place gem at this position
                PlaceGemAt(gemToSpawn, emergencyPlacement.x, emergencyPlacement.y, config, emergencyPlacement.actualWidth, emergencyPlacement.actualHeight, emergencyPlacement.isRotated);
                
                // Remove from pool and add to hiddenGems
                unplacedGems.Remove(gemToSpawn);
                hiddenGems.Add(gemToSpawn);
                
                Debug.LogWarning($"Emergency spawned gem {gemToSpawn.GemId} at position ({emergencyPlacement.x}, {emergencyPlacement.y}) - backtracking failed");
                return;
            }
        }
        
        Debug.LogError($"Failed to force spawn gem at cell ({cell.BoardX}, {cell.BoardY}) - no valid placement found");
    }
    
    private void ForceSpawnGemAtCell(CellUI cell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return;
        
        // Reset exclude flag of this cell to allow gem spawn
        cell.ResetExcludedFromGemSpawn();
        
        // Try spawning each gem in pool to find gem that can be placed at this cell
        for (int i = 0; i < unplacedGems.Count; i++)
        {
            Gem gemToSpawn = unplacedGems[i];
            
            // Get orientation of this gem
            GemOrientation orientation = GemOrientation.Horizontal;
            if (gemOrientations != null && gemOrientations.ContainsKey(gemToSpawn))
            {
                orientation = gemOrientations[gemToSpawn];
            }
            
            var config = gemConfigData.GetGemConfig(gemToSpawn.GemId);
            if (config == null) continue;
            
            // Find all positions that can place this gem containing cell (ignore exclude flag)
            List<PlacementOption> allPlacements = GetAllValidPlacementsIgnoringExclude(gemToSpawn, gemConfigData, orientation);
            
            foreach (var placement in allPlacements)
            {
                // Check if this placement contains cell
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
                    // Check again if gem can be placed (only check gem and revealed, don't check exclude flag)
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
                        // Check if after placing this gem, remaining gems can be placed
                        if (CanPlaceRemainingGemsAfterPlacing(gemToSpawn, placement))
                        {
                            // Reset exclude flag of all cells in placement
                            for (int x = 0; x < placement.actualWidth; x++)
        {
                                for (int y = 0; y < placement.actualHeight; y++)
                                {
                                    board[placement.x + x, placement.y + y].ResetExcludedFromGemSpawn();
                                }
                            }
                            
                            // Place gem at this position
                            PlaceGemAt(gemToSpawn, placement.x, placement.y, config, placement.actualWidth, placement.actualHeight, placement.isRotated);
                            
                            // Remove from pool and add to hiddenGems
                            unplacedGems.RemoveAt(i);
                            hiddenGems.Add(gemToSpawn);
                            
                            Debug.Log($"Force spawned gem {gemToSpawn.GemId} at cell ({cell.BoardX}, {cell.BoardY})");
                            return; // Success
                        }
                    }
                }
            }
        }
        
        Debug.LogError($"Failed to force spawn gem at cell ({cell.BoardX}, {cell.BoardY})");
    }
    
    // Get all positions that can place gem ignoring exclude flag
    private List<PlacementOption> GetAllValidPlacementsIgnoringExclude(Gem gem, GemConfigData gemConfigData, GemOrientation orientation)
    {
        List<PlacementOption> validPlacements = new List<PlacementOption>();
        var config = gemConfigData.GetGemConfig(gem.GemId);
        if (config == null) return validPlacements;
        
        // Determine rotation directions based on specified orientation
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
                    // Check if gem can be placed (only check gem, revealed and dynamite, don't check exclude flag)
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
    
    // Spawn gem exactly at specified cell (cell must be part of gem)
    // Randomly choose 1 gem, place it, check if remaining gems can be placed, if not choose another gem
    private bool SpawnGemAtExactCell(CellUI cell)
    {
        if (unplacedGems == null || unplacedGems.Count == 0 || gemConfigData == null)
            return false;
        
        // Reset exclude flag of this cell to allow gem spawn
        cell.ResetExcludedFromGemSpawn();
        
        // Shuffle gem list for randomness
        List<Gem> shuffledGems = new List<Gem>(unplacedGems);
        for (int i = 0; i < shuffledGems.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffledGems.Count);
            Gem temp = shuffledGems[i];
            shuffledGems[i] = shuffledGems[randomIndex];
            shuffledGems[randomIndex] = temp;
        }
        
        // Try each gem randomly
        foreach (Gem gemToSpawn in shuffledGems)
        {
            // Get orientation of this gem
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
    // Tối ưu: Early exit, giảm allocation, giới hạn số lần thử, sắp xếp gems theo kích thước
    private const int MAX_BACKTRACK_ATTEMPTS = 2000000; // Tăng giới hạn số lần thử để tìm giải pháp tốt hơn
    
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
        
        // Sắp xếp gems theo kích thước (lớn trước) để backtracking hiệu quả hơn
        // Tạo bản sao để không ảnh hưởng đến list gốc
        List<Gem> sortedGems = new List<Gem>(gems);
        sortedGems.Sort((a, b) =>
        {
            var cfgA = gemConfigData?.GetGemConfig(a.GemId);
            var cfgB = gemConfigData?.GetGemConfig(b.GemId);
            if (cfgA == null || cfgB == null) return 0;
            
            GemOrientation oriA = gemOrientations != null && gemOrientations.ContainsKey(a) 
                ? gemOrientations[a] : GemOrientation.Horizontal;
            GemOrientation oriB = gemOrientations != null && gemOrientations.ContainsKey(b) 
                ? gemOrientations[b] : GemOrientation.Horizontal;
            
            int areaA = GetGemArea(cfgA, oriA);
            int areaB = GetGemArea(cfgB, oriB);
            
            return areaB.CompareTo(areaA); // Lớn trước
        });
        
        int attemptCount = 0;
        return CanPlaceAllGemsOnTestBoardRecursive(sortedGems, 0, testBoard, ref attemptCount);
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
                // Cell trống nếu: không có gem, không phải dynamite, không bị exclude, và chưa được reveal
                // (Các cell đã được reveal không thể spawn gem mới)
                bool isEmpty = cell.Gem == null && !cell.IsDynamite && !cell.IsExcludedFromGemSpawn && !cell.IsRevealed;
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
        // Play explosion sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayExplosionSound();
        }
        
        // Spawn explosion particle effect
        SpawnExplosionEffect(x, y);
        
        // Dig and reveal cells in 3x3 explosion area with 30% gem reveal chance
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int newX = x + i;
                int newY = y + j;
                
                if (newX >= 0 && newX < boardWidth && newY >= 0 && newY < boardHeight)
                {
                    CellUI cell = board[newX, newY];
                    
                    // Only process if cell is not revealed yet
                    if (!cell.IsRevealed)
                    {
                        // Dig cell (remove stone layers and reveal)
                        cell.Dig();
                        
                        // Process gem reveal with 30% chance same as when digging with pickaxe
                        ProcessGemReveal(cell);
                    }
                }
            }
        }
    }
    
    // Spawn explosion particle effect at dynamite position
    private void SpawnExplosionEffect(int x, int y)
    {
        if (explosionParticlePrefab == null || boardParentUI == null)
        {
            Debug.LogWarning("Explosion particle prefab or board parent UI is null!");
            return;
        }
        
        // Calculate position of cell in Canvas
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
        
        // Create particle system instance (DO NOT place in Canvas, place in scene root)
        ParticleSystem explosion = Instantiate(explosionParticlePrefab);
        
        // With Screen Space Overlay Canvas, Particle System needs to be placed in World Space
        // Convert UI position to world position
        Canvas canvas = boardParentUI.GetComponentInParent<Canvas>();
        Vector3 worldPosition = Vector3.zero;
        
        // Get main camera (used for entire function)
        Camera mainCamera = Camera.main ?? FindObjectOfType<Camera>();
        
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Screen Space Overlay: convert screen position to world position
                if (mainCamera != null)
                {
                    // Convert UI world position to screen point, then to world position at a certain distance
                    Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(null, cellRect.position);
                    // Place particle at a distance before camera (e.g., 10 units)
                    worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, mainCamera.nearClipPlane + 5f));
                }
                else
                {
                    // Fallback: use position directly
                    worldPosition = cellRect.position;
                }
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // Screen Space Camera: convert to world position of camera
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
    
}

