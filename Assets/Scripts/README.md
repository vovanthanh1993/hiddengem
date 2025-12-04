# Hidden Gem Game - Scripts Documentation

## Tổng quan
Game "Hidden Gem" là một **game puzzle 2D** theo thời gian thực, người chơi sử dụng pickaxe để đào và tìm các gem ẩn dưới các ô đá.

## Cấu trúc Scripts

### 1. Data Scripts
- **GemConfig.cs**: Cấu hình các loại gem (kích thước, màu sắc)
- **StageConfig.cs**: Cấu hình các stage (kích thước board, số lượng gem, dynamite)
- **GameDataInitializer.cs**: Script để khởi tạo dữ liệu game (ScriptableObjects)

### 2. Core Scripts
- **CellUI.cs**: Quản lý từng ô trên board (UI Image, đá, gem, dynamite) - sử dụng UI mode
- **Gem.cs**: Quản lý gem và các cell UI mà nó chiếm giữ
- **BoardManager.cs**: Quản lý board game trong Canvas, đặt gem ngẫu nhiên, xử lý đào
- **PickaxeManager.cs**: Quản lý số lượng pickaxe
- **StageManager.cs**: Quản lý các stage, tạo gem theo config
- **GameManager.cs**: Điều khiển game chính
- **UIManager.cs**: Quản lý giao diện người dùng

## Cách thiết lập

### Bước 1: Tạo ScriptableObjects
1. Tạo một GameObject trống trong scene
2. Add component `GameDataInitializer`
3. Right-click vào component và chọn "Initialize Game Data"
4. Điều này sẽ tạo 2 file:
   - `Assets/GemConfigData.asset`
   - `Assets/StageConfigData.asset`

5. **Assign Sprites cho Gems** (Quan trọng):
   - Select `Assets/GemConfigData.asset` trong Project window
   - Trong Inspector, bạn sẽ thấy 9 Gem Configs (Gem 1 đến Gem 9)
   - Với mỗi Gem Config, kéo sprite tương ứng vào field `Gem Sprite`:
     - **Gem 1** (1x2): Sprite cho gem loại 1
     - **Gem 2** (1x3): Sprite cho gem loại 2
     - **Gem 3** (1x4): Sprite cho gem loại 3
     - **Gem 4** (1x5): Sprite cho gem loại 4
     - **Gem 5** (2x2): Sprite cho gem loại 5
     - **Gem 6** (2x3): Sprite cho gem loại 6
     - **Gem 7** (2x4): Sprite cho gem loại 7
     - **Gem 8** (3x3): Sprite cho gem loại 8
     - **Gem 9** (4x4): Sprite cho gem loại 9
   - **Lưu ý**: Nếu không assign sprite, gem sẽ hiển thị bằng màu (gemColor)

### Bước 2: Setup Scene

**Quan trọng**: Game sử dụng UI mode - board được tạo trong Canvas!

1. **Tạo Canvas** (nếu chưa có):
   - Unity sẽ tự động tạo Canvas khi bạn tạo UI element đầu tiên
   - Hoặc tạo thủ công: Right-click Hierarchy → UI → Canvas
   - Canvas sẽ tự động có CanvasScaler và GraphicRaycaster

2. **Tạo các Manager GameObjects**:
   - Tạo một GameObject tên "GameManager" và add component `GameManager`
   - Tạo GameObject "PickaxeManager" và add component `PickaxeManager`
   - Tạo GameObject "StageManager" và add component `StageManager`
   - Tạo GameObject "BoardManager" và add component `BoardManager`
   - Tạo GameObject "UIManager" và add component `UIManager`

### Bước 3: Kết nối References
1. **GameManager**:
   - Kéo StageManager vào field `stageManager`
   - Kéo BoardManager vào field `boardManager`
   - Kéo PickaxeManager vào field `pickaxeManager`

2. **StageManager**:
   - Kéo `GemConfigData.asset` vào field `gemConfigData`
   - Kéo `StageConfigData.asset` vào field `stageConfigData`
   - Kéo BoardManager vào field `boardManager`
   - (Optional) Kéo Gem prefab vào field `gemPrefab`

3. **BoardManager**:
   - **Cell Size**: Mặc định là 100 (mỗi cell 100x100 pixels), có thể điều chỉnh
   - (Optional) Kéo CellUI prefab vào field `Cell UI Prefab`
   - (Optional) Kéo RectTransform của Board vào field `Board Parent UI`:
     - Nếu không set, script sẽ tự động tạo GameObject "Board" trong Canvas
     - Board sẽ được tạo với GridLayoutGroup để tự động sắp xếp cells
   - **Lưu ý**: Board được tạo TRONG Canvas, không phải World Space

4. **UIManager**:
   - Tạo UI Canvas với các TextMeshProUGUI và Button
   - Kéo các UI elements vào các field tương ứng:
     - `pickaxeCountText`: Text hiển thị số pickaxe
     - `timerText`: Text hiển thị timer
     - `stageText`: Text hiển thị stage hiện tại
     - `stageButtons`: Array các button cho 5 stage
     - `addPickaxePopup`: Popup khi hết pickaxe
     - `rewardChestPopup`: Popup khi hoàn thành stage

### Bước 4: Setup Prefabs

#### 4.1. Tạo CellUI Prefab (Optional)

**Lưu ý**: Nếu không tạo prefab, BoardManager sẽ tự động tạo cells với Image component.

**Cách tạo CellUI Prefab**:

1. **Tạo GameObject cho CellUI**:
   - Trong Hierarchy, right-click → UI → Image
   - Đặt tên là "CellUI"
   - Add Component → `CellUI` (script)

2. **Tạo các Sprite cần thiết**:
   
   **Option A: Import sprite từ file ảnh**
   - Chuẩn bị 4 file ảnh (.png): stone1.png, stone2.png, dynamite.png, empty.png
   - Kéo thả vào thư mục `Assets/Sprites/`
   - Select từng sprite → Inspector → Texture Type = "Sprite (2D and UI)"
   - Click "Apply"
   
   **Option B: Tạo sprite đơn giản trong Unity**
   - Tạo 4 Texture2D mới (Assets → Create → Texture2D)
   - Đặt tên: `Stone1Layer`, `Stone2Layers`, `Dynamite`, `EmptyCell`
   - Vẽ hoặc import hình ảnh vào các texture
   - Select từng texture → Inspector → Texture Type = "Sprite (2D and UI)"
   - Click "Apply"

3. **Assign Sprites vào CellUI**:
   - Select GameObject "CellUI"
   - Trong Inspector, tìm component `CellUI`
   - Kéo các sprite vào các field tương ứng:
     - `Stone 1 Layer Sprite`
     - `Stone 2 Layers Sprite`
     - `Dynamite Sprite`
     - `Empty Cell Sprite`

4. **Tạo Prefab**:
   - Kéo GameObject "CellUI" từ Hierarchy vào thư mục `Assets/Prefabs/`
   - Kéo prefab này vào field `Cell UI Prefab` trong BoardManager

#### 4.2. Gem Sprites

**Quan trọng**: Mỗi loại gem cần có sprite riêng!

1. **Chuẩn bị Sprites**:
   - Tạo hoặc import 9 sprite cho 9 loại gem
   - Đặt tên rõ ràng: `Gem1`, `Gem2`, `Gem3`, ..., `Gem9`
   - Hoặc theo kích thước: `Gem_1x2`, `Gem_1x3`, `Gem_2x2`, etc.
   - Import vào `Assets/Sprites/`
   - Set Texture Type = "Sprite (2D and UI)"

2. **Assign Sprites vào GemConfigData**:
   - Select `Assets/GemConfigData.asset` trong Project window
   - Với mỗi Gem Config, kéo sprite tương ứng vào field `Gem Sprite`
   - Gem sẽ tự động hiển thị sprite này khi cell được reveal

3. **Gem Component**:
   - Gem chỉ là một **data/logic component** (MonoBehaviour)
   - Gem không có renderer riêng
   - Gem sprite được lưu trong GemConfigData và hiển thị qua CellUI
   - Gem được tạo tự động khi stage load, không cần prefab

**Cách Gem được hiển thị**:
- Khi một CellUI được reveal và có gem:
  - CellUI sẽ tự động lấy sprite từ GemConfigData dựa trên gemId
  - Hiển thị sprite trên Image của CellUI
  - Nếu không có sprite, sẽ dùng màu (gemColor) làm fallback

## Gameplay Mechanics

### Đào Cell
- Click trực tiếp vào một cell UI để đào (sử dụng IPointerClickHandler)
- Mỗi layer đá tốn 1 pickaxe
- Sau khi đào xong, cell sẽ được reveal
- Không cần Camera hoặc raycast - click được xử lý trực tiếp qua UI

### Gem Hit Ratio
- 30% cơ hội hit gem khi đào
- Nếu không hit gem nhưng không đủ gem để hoàn thành stage, sẽ force hit gem

### Dynamite
- Khi đào vào dynamite, sẽ nổ 3x3 area xung quanh
- Tất cả cell trong area sẽ bị clear

### Thu thập Gem
- Gem chỉ được thu thập khi toàn bộ shape của nó được reveal
- Sau khi thu thập, gem sẽ bay về slot tương ứng trên stage board

### Hoàn thành Stage
- Khi thu thập đủ tất cả gem của stage, sẽ nhận reward
- Reward sẽ được thêm vào pickaxe count
- Stage tiếp theo sẽ được unlock

## Lưu ý (UI Mode)

- **UI Mode**: 
  - Game sử dụng hoàn toàn UI mode - board được tạo trong Canvas
  - Mỗi cell là một UI Image với component `CellUI`
  - Không cần Camera để nhìn thấy board (Canvas tự render)
  
- **Input**: 
  - Click được xử lý trực tiếp qua `IPointerClickHandler` trong `CellUI`
  - Không cần raycast hoặc collider
  - Canvas cần có GraphicRaycaster để detect click (tự động có khi tạo Canvas)
  
- **Board Setup**: 
  - Board được tạo tự động trong Canvas với GridLayoutGroup
  - Mỗi cell mặc định là 100x100 pixels
  - Có thể điều chỉnh `cellSize` trong BoardManager Inspector
  
- **UI Elements**: 
  - Cần setup UI với TextMeshPro (TMP) package cho các text
  - Timer mặc định là 10 giờ, có thể điều chỉnh trong UIManager
  - Tất cả UI elements (board, text, buttons, popups) đều trong Canvas
  
- **Canvas**: 
  - Canvas render mode: Screen Space Overlay (mặc định)
  - Board và các UI elements khác đều trong cùng Canvas
  - Canvas tự động scale theo screen size
