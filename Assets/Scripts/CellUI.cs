using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class CellUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CellType cellType;
    [SerializeField] private int stoneLayers;
    [SerializeField] private Gem gem; // Gem chứa trong cell này (nếu có)
    [SerializeField] private bool isDynamite;
    [SerializeField] private bool isRevealed;
    [SerializeField] private bool isExcludedFromGemSpawn; // Đánh dấu cell này đã được đào nhưng không spawn gem
    
    [Header("UI Sprites")]
    [SerializeField] private Sprite stone1LayerSprite;
    [SerializeField] private Sprite stone2LayersSprite;
    [SerializeField] private Sprite dynamiteSprite;
    
    [Header("Gem Display")]
    [SerializeField] private Image gemImage; // Child Image để hiển thị phần gem của cell này
    
    private Image cellImage;
    private int boardX;
    private int boardY;
    
    // Thông tin về vị trí của cell này trong gem
    private Gem gemParent;
    private int gemCellX; // Vị trí X của cell này trong gem sprite gốc (0-based)
    private int gemCellY; // Vị trí Y của cell này trong gem sprite gốc (0-based)
    private int gemWidth; // Width của gem sprite gốc
    private int gemHeight; // Height của gem sprite gốc
    private bool isRotated; // Gem có bị xoay không
    
    public CellType Type => cellType;
    public int StoneLayers => stoneLayers;
    public Gem Gem => gem;
    public bool IsDynamite => isDynamite;
    public bool IsRevealed => isRevealed;
    public bool IsExcludedFromGemSpawn => isExcludedFromGemSpawn;
    public int BoardX => boardX;
    public int BoardY => boardY;
    
    private void Awake()
    {
        cellImage = GetComponent<Image>();
        if (cellImage == null)
            cellImage = gameObject.AddComponent<Image>();
    }
    
    public void Initialize(int x, int y)
    {
        boardX = x;
        boardY = y;
        ResetCell();
    }
    
    public void ResetCell()
    {
        cellType = CellType.Stone1Layer;
        stoneLayers = 1;
        gem = null;
        isDynamite = false;
        isRevealed = false;
        isExcludedFromGemSpawn = false;
        UpdateVisual();
    }
    
    public void SetStoneLayers(int layers)
    {
        stoneLayers = layers;
        cellType = layers == 1 ? CellType.Stone1Layer : CellType.Stone2Layer;
        UpdateVisual();
    }
    
    public void SetGem(Gem gemData)
    {
        gem = gemData;
        if (gem != null)
        {
            cellType = CellType.Gem;
            isExcludedFromGemSpawn = false; // Khi có gem, không còn bị exclude nữa
            // Cập nhật visual để hiển thị gem nếu cell đã được reveal
            UpdateVisual();
        }
    }
    
    public void MarkAsExcludedFromGemSpawn()
    {
        isExcludedFromGemSpawn = true;
    }
    
    public void ResetExcludedFromGemSpawn()
    {
        isExcludedFromGemSpawn = false;
    }
    
    public void SetGemInfo(Gem gemData, int cellX, int cellY, int width, int height)
    {
        // Overload cũ để tương thích
        SetGemInfo(gemData, cellX, cellY, width, height, width, height, false);
    }
    
    public void SetGemInfo(Gem gemData, int spriteCellX, int spriteCellY, int spriteWidth, int spriteHeight, int actualWidth, int actualHeight, bool rotated)
    {
        gemParent = gemData;
        gemCellX = spriteCellX; // Vị trí X trong sprite gốc
        gemCellY = spriteCellY; // Vị trí Y trong sprite gốc
        gemWidth = spriteWidth; // Width của sprite gốc
        gemHeight = spriteHeight; // Height của sprite gốc
        isRotated = rotated;
        
        // Tạo hoặc cập nhật gemImage để hiển thị phần gem của cell này
        SetupGemImage();
        
        // Cập nhật visual để hiển thị gem nếu cell đã được reveal
        UpdateVisual();
    }
    
    private void SetupGemImage()
    {
        if (gemParent == null || gemWidth == 0 || gemHeight == 0) return;
        
        // Tạo gemImage nếu chưa có
        if (gemImage == null)
        {
            GameObject gemImageObj = new GameObject("GemImage");
            gemImageObj.transform.SetParent(transform, false);
            
            RectTransform gemRect = gemImageObj.AddComponent<RectTransform>();
            RectTransform cellRect = GetComponent<RectTransform>();
            
            // Anchor và pivot ở góc trái trên để dễ tính toán offset
            gemRect.anchorMin = new Vector2(0f, 1f);
            gemRect.anchorMax = new Vector2(0f, 1f);
            gemRect.pivot = new Vector2(0f, 1f);
            // Size = cell size (sẽ được scale sau)
            gemRect.sizeDelta = new Vector2(cellRect.rect.width, cellRect.rect.height);
            gemRect.anchoredPosition = Vector2.zero;
            
            gemImage = gemImageObj.AddComponent<Image>();
            gemImage.raycastTarget = false; // Không block click
            
            // Tạo Material với shader để cắt UV
            CreateGemMaterial();
        }
        
        // Lấy gem sprite từ config
        Sprite gemSprite = GetGemSprite(gemParent.GemId);
        if (gemSprite != null)
        {
            gemImage.sprite = gemSprite;
            gemImage.color = Color.white;
            gemImage.type = Image.Type.Simple;
            gemImage.preserveAspect = false;
            
            // Cập nhật material với UV coordinates
            UpdateGemMaterialUV();
        }
        else
        {
            // Fallback: dùng màu
            gemImage.color = gemParent.GemColor;
        }
        
        // Ẩn gem image ban đầu, chỉ hiện khi cell được reveal
        // Nhưng nếu cell đã được reveal thì hiển thị ngay
        if (isRevealed)
        {
            gemImage.gameObject.SetActive(true);
        }
        else
        {
            gemImage.gameObject.SetActive(false);
        }
    }
    
    private void CreateGemMaterial()
    {
        if (gemImage == null) return;
        
        // Tạo Material với shader UI/Default
        Material gemMaterial = new Material(Shader.Find("UI/Default"));
        gemImage.material = gemMaterial;
    }
    
    private void UpdateGemMaterialUV()
    {
        if (gemImage == null || gemWidth == 0 || gemHeight == 0) return;
        
        RectTransform gemRect = gemImage.GetComponent<RectTransform>();
        RectTransform cellRect = GetComponent<RectTransform>();
        
        // Lấy kích thước thực tế của cell
        float cellWidth = cellRect.rect.width;
        float cellHeight = cellRect.rect.height;
        
        // Scale gem sprite lên để mỗi cell chỉ hiển thị 1 phần của gem
        // Ví dụ: gem 2x2, scale = 2x2, mỗi cell sẽ hiển thị 1/4 của gem sprite
        float scaleX = gemWidth;
        float scaleY = gemHeight;
        gemRect.localScale = new Vector3(scaleX, scaleY, 1f);
        
        // Tính toán offset để chỉ phần tương ứng với cell này hiển thị trong cell bounds
        // gemCellX, gemCellY là vị trí của cell trong gem (0-based từ top-left của gem)
        // 
        // Khi scale gem sprite lên gemWidth x gemHeight lần:
        // - Gem sprite sẽ lớn hơn gemWidth x gemHeight lần so với cell
        // - Cần offset để phần đúng của gem sprite nằm trong cell bounds
        //
        // Ví dụ gem 2x2 (scale 2x2):
        // - Cell (0,0): cần hiển thị phần top-left (0-50% theo X, 0-50% theo Y của sprite)
        //   Offset: (-0.5 * cellWidth, 0.5 * cellHeight)
        // - Cell (1,0): cần hiển thị phần top-right (50-100% theo X, 0-50% theo Y của sprite)
        //   Offset: (0.5 * cellWidth, 0.5 * cellHeight)
        // - Cell (0,1): cần hiển thị phần bottom-left (0-50% theo X, 50-100% theo Y của sprite)
        //   Offset: (-0.5 * cellWidth, -0.5 * cellHeight)
        // - Cell (1,1): cần hiển thị phần bottom-right (50-100% theo X, 50-100% theo Y của sprite)
        //   Offset: (0.5 * cellWidth, -0.5 * cellHeight)
        
        // Set size trước (size = cell size, sẽ được scale sau)
        gemRect.sizeDelta = new Vector2(cellWidth, cellHeight);
        
        // Tính toán offset để phần đúng của sprite nằm trong cell
        // Với anchor và pivot ở góc trái trên (0, 1):
        // - Khi scale lên gemWidth x gemHeight lần, sprite sẽ lớn hơn gemWidth x gemHeight lần từ pivot
        // - Sprite có size = gemWidth * cellWidth x gemHeight * cellHeight từ pivot
        // - Cell (gemCellX, gemCellY) cần hiển thị phần từ gemCellX/gemWidth đến (gemCellX+1)/gemWidth theo X
        //   và từ gemCellY/gemHeight đến (gemCellY+1)/gemHeight theo Y của sprite
        
        // Offset X: để phần gemCellX của sprite bắt đầu từ góc trái của cell
        // Với anchor và pivot ở góc trái trên (0, 1):
        // - Khi scale lên gemWidth lần, sprite sẽ lớn hơn gemWidth lần từ pivot
        // - Phần gemCellX của sprite bắt đầu ở vị trí: gemCellX * cellWidth từ pivot của sprite
        // - Cần offset sprite sang trái để phần này bắt đầu từ góc trái của cell
        // Ví dụ gem 3x1 (ngang):
        //   - Cell (0,0): offsetX = 0 (phần đầu)
        //   - Cell (1,0): offsetX = -cellWidth (phần giữa)
        //   - Cell (2,0): offsetX = -2*cellWidth (phần cuối)
        float offsetX = -gemCellX * cellWidth;
        
        // Offset Y: để phần gemCellY của sprite bắt đầu từ góc trên của cell
        // Lưu ý: Y trong UI đi xuống, anchor (0,1) là góc trái trên
        // - Khi scale lên gemHeight lần, sprite sẽ lớn hơn gemHeight lần từ pivot
        // - Phần gemCellY của sprite bắt đầu ở vị trí: gemCellY * cellHeight từ pivot của sprite (tính từ trên xuống)
        // - Cần offset sprite xuống để phần này bắt đầu từ góc trên của cell
        // Ví dụ gem 1x3 (dọc):
        //   - Cell (0,0): offsetY = 0 (phần đầu)
        //   - Cell (0,1): offsetY = cellHeight (phần giữa)
        //   - Cell (0,2): offsetY = 2*cellHeight (phần cuối)
        float offsetY = gemCellY * cellHeight;
        
        // Set offset để phần đúng của sprite nằm trong cell
        gemRect.anchoredPosition = new Vector2(offsetX, offsetY);
        
        // Thêm RectMask2D trên cell để clip gem image trong cell bounds
        // Điều này đảm bảo gem chỉ hiển thị trong cell của nó, không tràn ra ngoài
        RectMask2D mask = GetComponent<RectMask2D>();
        if (mask == null)
        {
            mask = gameObject.AddComponent<RectMask2D>();
        }
        mask.padding = Vector4.zero;
    }
    
    public void SetDynamite(bool value)
    {
        isDynamite = value;
        if (value)
        {
            cellType = CellType.Dynamite;
        }
    }
    
    public int Dig()
    {
        if (isRevealed)
            return 0;
            
        int pickaxesUsed = stoneLayers;
        stoneLayers = 0;
        
        if (stoneLayers == 0)
        {
            Reveal();
        }
        
        return pickaxesUsed;
    }
    
    public void Reveal()
    {
        isRevealed = true;
        UpdateVisual();
    }
    
    public void Clear()
    {
        cellType = CellType.Empty;
        gem = null;
        isDynamite = false;
        stoneLayers = 0;
        isRevealed = true;
        UpdateVisual();
    }
    
    private void UpdateVisual()
    {
        if (cellImage == null) return;
        
        Sprite spriteToUse = null;
        
        if (!isRevealed)
        {
            // Cell chưa được reveal
            if (isDynamite && dynamiteSprite != null)
            {
                spriteToUse = dynamiteSprite;
            }
            else if (stoneLayers == 2 && stone2LayersSprite != null)
            {
                spriteToUse = stone2LayersSprite;
            }
            else if (stoneLayers == 1 && stone1LayerSprite != null)
            {
                spriteToUse = stone1LayerSprite;
            }
        }
        else
        {
            // Cell đã được reveal - hiển thị background (không set sprite)
            spriteToUse = null;
            
            if (gem != null)
            {
                // Hiển thị gem image của cell này trên nền background
                if (gemImage != null)
                {
                    gemImage.gameObject.SetActive(true);
                }
            }
        }
        
        if (spriteToUse != null)
        {
            cellImage.sprite = spriteToUse;
            cellImage.color = Color.white; // Reset color khi có sprite
        }
        else if (isRevealed)
        {
            // Cell đã reveal - không có sprite, để background board hiện ra
            cellImage.sprite = null;
            cellImage.color = Color.clear; // Trong suốt để background board hiện ra
        }
        
        // gemImage đã được setup trong SetGemInfo và tự động hiển thị khi cell reveal
    }
    
    private Sprite GetGemSprite(int gemId)
    {
        // Lấy sprite từ GemConfigData
        if (StageManager.Instance != null && StageManager.Instance.CurrentStageConfig != null)
        {
            var gemConfigData = StageManager.Instance.GetGemConfigData();
            if (gemConfigData != null)
            {
                var config = gemConfigData.GetGemConfig(gemId);
                if (config != null && config.gemSprite != null)
                {
                    return config.gemSprite;
                }
            }
        }
        return null;
    }
    
    // Handle click event từ UI
    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DigCellUI(this);
        }
    }
    
    // Fade out gem image trong cell này
    public void FadeOutGemImage(float duration, System.Action onComplete = null)
    {
        if (gemImage == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        gemImage.DOFade(0f, duration)
            .OnComplete(() =>
            {
                onComplete?.Invoke();
            });
    }
    
    // Lấy gemImage để fade out (public getter)
    public Image GetGemImage()
    {
        return gemImage;
    }
}

