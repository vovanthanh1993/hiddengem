using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GemPanel : MonoBehaviour
{
    [SerializeField] private RectTransform content; // Content container cho các gem images
    [SerializeField] private GameObject gemItemPrefab; // Prefab cho mỗi gem item (optional, nếu null sẽ tạo động)
    [SerializeField] private int baseCellSize = 30; // Kích thước mỗi cell của gem (dùng để tính tỉ lệ)
    [SerializeField] private float dimmedAlpha = 0.3f; // Độ mờ của gem khi chưa được thu thập (0-1)
    [SerializeField] private float animationDuration = 0.5f; // Thời gian animation bay gem (giây)
    
    private List<GameObject> gemItems = new List<GameObject>(); // Danh sách các gem item đã tạo
    private Dictionary<int, List<GameObject>> gemItemsByGemId = new Dictionary<int, List<GameObject>>(); // Map gemId -> list of gem items
    private Dictionary<int, int> collectedCountByGemId = new Dictionary<int, int>(); // Track số lượng gem đã collected theo gemId
    private BoardManager boardManager;
    
    private void Start()
    {
        // Tìm BoardManager
        boardManager = FindObjectOfType<BoardManager>();
        if (boardManager != null)
        {
            // Subscribe vào event khi gem được collected
            boardManager.OnGemCollected += OnGemCollected;
        }
        
        // Subscribe vào event khi stage thay đổi
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged += UpdateGemPanel;
            // Cập nhật ngay nếu stage đã được load
            UpdateGemPanel(StageManager.Instance.CurrentStageId);
        }
        
        // Đảm bảo content có LayoutGroup để tự động sắp xếp
        if (content != null)
        {
            if (content.GetComponent<HorizontalLayoutGroup>() == null && 
                content.GetComponent<VerticalLayoutGroup>() == null &&
                content.GetComponent<GridLayoutGroup>() == null)
            {
                // Mặc định dùng HorizontalLayoutGroup
                HorizontalLayoutGroup layoutGroup = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layoutGroup.spacing = 10f;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe khi destroy
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged -= UpdateGemPanel;
        }
        
        if (boardManager != null)
        {
            boardManager.OnGemCollected -= OnGemCollected;
        }
    }
    
    private void OnGemCollected(Gem gem)
    {
        if (gem == null) return;
        
        // Tìm gem item tương ứng trong GemPanel
        GameObject targetGemItem = GetNextUncollectedGemItem(gem.GemId);
        if (targetGemItem == null) return;
        
        // Lấy vị trí của gem trên board (từ cell đầu tiên)
        Vector3 startPosition = GetGemBoardPosition(gem);
        if (startPosition == Vector3.zero) return;
        
        // Lấy vị trí đích trong GemPanel
        Vector3 targetPosition = GetGemItemPosition(targetGemItem);
        
        // Tạo animation bay gem từ board đến GemPanel
        StartCoroutine(AnimateGemToPanel(gem, startPosition, targetPosition, targetGemItem));
    }
    
    private GameObject GetNextUncollectedGemItem(int gemId)
    {
        if (!gemItemsByGemId.ContainsKey(gemId)) return null;
        
        var gemItemList = gemItemsByGemId[gemId];
        int collectedCount = collectedCountByGemId.ContainsKey(gemId) ? collectedCountByGemId[gemId] : 0;
        
        // Tìm gem item tiếp theo chưa được collected
        if (collectedCount < gemItemList.Count)
        {
            return gemItemList[collectedCount];
        }
        
        return null;
    }
    
    private Vector3 GetGemBoardPosition(Gem gem)
    {
        if (gem == null || gem.Cells == null || gem.Cells.Count == 0) return Vector3.zero;
        
        // Lấy cell đầu tiên của gem để làm vị trí bắt đầu
        CellUI firstCell = gem.Cells[0];
        if (firstCell == null) return Vector3.zero;
        
        RectTransform cellRect = firstCell.GetComponent<RectTransform>();
        if (cellRect == null) return Vector3.zero;
        
        // Convert UI position sang world position
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return cellRect.position;
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            {
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    cellRect,
                    RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, cellRect.position),
                    canvas.worldCamera,
                    out worldPos);
                return worldPos;
            }
        }
        
        return cellRect.position;
    }
    
    private Vector3 GetGemItemPosition(GameObject gemItem)
    {
        if (gemItem == null) return Vector3.zero;
        
        RectTransform rectTransform = gemItem.GetComponent<RectTransform>();
        if (rectTransform == null) return Vector3.zero;
        
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return rectTransform.position;
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            {
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    rectTransform,
                    RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
                    canvas.worldCamera,
                    out worldPos);
                return worldPos;
            }
        }
        
        return rectTransform.position;
    }
    
    private void UpdateGemPanel(int stageId)
    {
        // Xóa các gem items cũ
        ClearGemItems();
        
        // Reset collected gems tracking
        collectedCountByGemId.Clear();
        gemItemsByGemId.Clear();
        
        // Lấy stage config hiện tại
        var stageConfig = StageManager.Instance.CurrentStageConfig;
        if (stageConfig == null) return;
        
        var gemConfigData = StageManager.Instance.GetGemConfigData();
        if (gemConfigData == null) return;
        
        // Tạo gem items cho mỗi gem request trong stage
        foreach (var request in stageConfig.gemRequests)
        {
            var gemConfig = gemConfigData.GetGemConfig(request.gemId);
            if (gemConfig == null) continue;
            
            // Xác định width và height dựa trên orientation
            int displayWidth = gemConfig.width;
            int displayHeight = gemConfig.height;
            
            // Nếu orientation là Vertical và gem có thể xoay được (width != height)
            if (request.orientation == GemOrientation.Vertical && gemConfig.width != gemConfig.height)
            {
                // Xoay gem: swap width và height
                displayWidth = gemConfig.height;
                displayHeight = gemConfig.width;
            }
            
            // Tạo một gem item cho mỗi gem cần tìm (nếu count > 1, tạo nhiều items)
            for (int i = 0; i < request.count; i++)
            {
                CreateGemItem(gemConfig, request.gemId, displayWidth, displayHeight, request.orientation);
            }
        }
        
        // Sắp xếp ngẫu nhiên các gem items
        ShuffleGemItems();
        
        // Làm mờ tất cả gem items ban đầu
        DimAllGemItems();
    }
    
    private void ShuffleGemItems()
    {
        if (gemItems.Count <= 1) return;
        
        // Fisher-Yates shuffle algorithm trên list
        for (int i = gemItems.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            
            // Swap trong list
            GameObject temp = gemItems[i];
            gemItems[i] = gemItems[randomIndex];
            gemItems[randomIndex] = temp;
        }
        
        // Set lại sibling index theo thứ tự mới sau khi shuffle
        for (int i = 0; i < gemItems.Count; i++)
        {
            if (gemItems[i] != null)
            {
                gemItems[i].transform.SetSiblingIndex(i);
            }
        }
    }
    
    private void DimAllGemItems()
    {
        foreach (var gemItem in gemItems)
        {
            if (gemItem != null)
            {
                Image gemImage = gemItem.GetComponent<Image>();
                if (gemImage != null)
                {
                    Color color = gemImage.color;
                    color.a = dimmedAlpha;
                    gemImage.color = color;
                }
            }
        }
    }
    
    private void CreateGemItem(GemConfig gemConfig, int gemId, int displayWidth, int displayHeight, GemOrientation orientation)
    {
        GameObject gemItem;
        
        // Tính kích thước dựa trên tỉ lệ width x height
        float itemWidth = baseCellSize * displayWidth;
        float itemHeight = baseCellSize * displayHeight;
        
        if (gemItemPrefab != null)
        {
            gemItem = Instantiate(gemItemPrefab, content);
            
            // Cập nhật kích thước của prefab
            RectTransform rectTransform = gemItem.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(itemWidth, itemHeight);
                
                // Xoay gem nếu orientation là Vertical và gem có thể xoay được
                if (orientation == GemOrientation.Vertical && gemConfig.width != gemConfig.height)
                {
                    rectTransform.rotation = Quaternion.Euler(0, 0, 90f);
                }
            }
            
            // Cập nhật LayoutElement nếu có
            LayoutElement layoutElement = gemItem.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = itemWidth;
                layoutElement.preferredHeight = itemHeight;
            }
            
            // Cập nhật Image sprite và màu
            Image gemImage = gemItem.GetComponent<Image>();
            if (gemImage != null)
            {
                if (gemConfig.gemSprite != null)
                {
                    gemImage.sprite = gemConfig.gemSprite;
                    gemImage.color = Color.white;
                }
                else
                {
                    gemImage.color = gemConfig.gemColor;
                }
            }
        }
        else
        {
            // Tạo gem item động nếu không có prefab
            gemItem = new GameObject($"GemItem_{gemId}_{gemItems.Count}");
            gemItem.transform.SetParent(content, false);
            
            // Thêm RectTransform với kích thước theo tỉ lệ
            RectTransform rectTransform = gemItem.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(itemWidth, itemHeight);
            
            // Xoay gem nếu orientation là Vertical và gem có thể xoay được
            if (orientation == GemOrientation.Vertical && gemConfig.width != gemConfig.height)
            {
                rectTransform.rotation = Quaternion.Euler(0, 0, 90f);
            }
            
            // Thêm Image để hiển thị gem sprite
            Image gemImage = gemItem.AddComponent<Image>();
            
            // Set sprite nếu có, nếu không thì dùng màu
            if (gemConfig.gemSprite != null)
            {
                gemImage.sprite = gemConfig.gemSprite;
                gemImage.color = Color.white; // Dùng màu trắng để hiển thị sprite đúng màu
            }
            else
            {
                gemImage.color = gemConfig.gemColor; // Dùng màu nếu không có sprite
            }
            
            // Thêm LayoutElement để control size
            LayoutElement layoutElement = gemItem.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = itemWidth;
            layoutElement.preferredHeight = itemHeight;
        }
        
        gemItems.Add(gemItem);
        
        // Thêm vào dictionary để track theo gemId
        if (!gemItemsByGemId.ContainsKey(gemId))
        {
            gemItemsByGemId[gemId] = new List<GameObject>();
        }
        gemItemsByGemId[gemId].Add(gemItem);
    }
    
    private System.Collections.IEnumerator AnimateGemToPanel(Gem gem, Vector3 startPos, Vector3 targetPos, GameObject targetGemItem)
    {
        // Lấy sprite của gem
        Sprite gemSprite = GetGemSprite(gem.GemId);
        if (gemSprite == null) yield break;
        
        // Tạo GameObject tạm thời để animate
        GameObject flyingGem = new GameObject("FlyingGem");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        
        flyingGem.transform.SetParent(canvas.transform, false);
        
        RectTransform flyingRect = flyingGem.AddComponent<RectTransform>();
        Image flyingImage = flyingGem.AddComponent<Image>();
        
        flyingImage.sprite = gemSprite;
        flyingImage.color = Color.white;
        
        // Set kích thước dựa trên gem
        var gemConfigData = StageManager.Instance.GetGemConfigData();
        if (gemConfigData != null)
        {
            var gemConfig = gemConfigData.GetGemConfig(gem.GemId);
            if (gemConfig != null)
            {
                float width = baseCellSize * gemConfig.width;
                float height = baseCellSize * gemConfig.height;
                flyingRect.sizeDelta = new Vector2(width, height);
            }
        }
        
        flyingRect.position = startPos;
        
        // Animation bay từ startPos đến targetPos
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            
            // Ease out curve
            t = 1f - Mathf.Pow(1f - t, 3f);
            
            flyingRect.position = Vector3.Lerp(startPos, targetPos, t);
            
            // Scale animation (nhỏ dần khi bay)
            float scale = Mathf.Lerp(1f, 0.5f, t);
            flyingRect.localScale = Vector3.one * scale;
            
            yield return null;
        }
        
        // Brighten gem item trong panel
        BrightenGemItem(targetGemItem);
        
        // Cập nhật collected count
        if (!collectedCountByGemId.ContainsKey(gem.GemId))
        {
            collectedCountByGemId[gem.GemId] = 0;
        }
        collectedCountByGemId[gem.GemId]++;
        
        // Xóa gem khỏi board
        RemoveGemFromBoard(gem);
        
        // Destroy flying gem
        Destroy(flyingGem);
    }
    
    private Sprite GetGemSprite(int gemId)
    {
        var gemConfigData = StageManager.Instance?.GetGemConfigData();
        if (gemConfigData == null) return null;
        
        var gemConfig = gemConfigData.GetGemConfig(gemId);
        return gemConfig?.gemSprite;
    }
    
    private void BrightenGemItem(GameObject gemItem)
    {
        if (gemItem == null) return;
        
        Image gemImage = gemItem.GetComponent<Image>();
        if (gemImage != null)
        {
            Color color = gemImage.color;
            color.a = 1f;
            gemImage.color = color;
        }
    }
    
    private void RemoveGemFromBoard(Gem gem)
    {
        if (gem == null || gem.Cells == null) return;
        
        // Xóa gem khỏi tất cả cells
        foreach (var cell in gem.Cells)
        {
            if (cell != null)
            {
                cell.SetGem(null); // SetGem() sẽ tự động gọi UpdateVisual()
            }
        }
        
        // Xóa gem khỏi hiddenGems list trong BoardManager
        if (boardManager != null)
        {
            boardManager.RemoveGem(gem);
        }
    }
    
    private void ClearGemItems()
    {
        foreach (var item in gemItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        gemItems.Clear();
    }
}
