# 休息區與成長系統企劃書

Oct 23, 2026 · @Zucknight

## 專案概要

Boss 戰原型（`docs/boss-oiia.md`，T0–T13）已告一段落。這份企劃書規劃主遊戲的外圍系統：玩家從休息區出發、挑戰 Boss、帶著獎勵回來變強。這一輪**只做系統**，角色、裝備、技能都只放驗證用的測試資料。

- **範圍內**：存檔、場景流程、休息區場景、角色切換系統、數值套用管線、裝備系統（含 2 件測試裝備）、技能樹系統、村長任務與首通獎勵。
- **範圍外**：新的正式角色、正式裝備與技能內容、小怪段、標題畫面、多存檔欄位、商店與貨幣。
- **已決議**（Oct 23）：
  - 角色採「每個角色一個獨立 prefab，場景執行時生成」。
  - 裝備解鎖與技能點來自「Boss 首通掉落＋村長任務」，沒有貨幣。
  - 技能樹在營火開，跟角色切換同一個介面。
  - 新增 `ProfileStore` 作為第二個 static 例外（純資料、不放元件引用）。

## 遊戲流程

```text
啟動遊戲 ─→ 休息區（營火旁出生）
              │  村長：接任務／回報
              │  鐵匠鋪：查看、更換裝備
              │  營火：切換角色、技能樹
              ↓  傳送門：選擇挑戰
           BossArena（照舊：開場 → 兩階段 → 勝敗）
              │  勝利：首通獎勵、記錄擊敗
              ↓  勝敗畫面「返回休息區」／ESC「返回休息區」
           休息區（傳送門前出生）
```

- `Hub.unity` 改成 Build Settings 第 0 個，`BossArena.unity` 第 1 個。
- 換場景時黑幕淡出淡入（約 0.4 秒，數值放 `SceneFlowData`）。
- 從休息區進 BossArena 算新的一次挑戰（`BattleSession.BeginAttempt`）；這次執行看過開場就能跳過，跟重試一樣。
- 勝敗畫面保留「重新挑戰」，新增「返回休息區」；BossArena 的 ESC 選單也新增「返回休息區」（先確認）。休息區的 ESC 選單只有「繼續、設定、退出遊戲」。

## 休息區

風格是森林裡的營地：傍晚暖色光、薄霧、營火火光，外圍用 2–3 排樹木圍出邊界，樹後面放隱形牆。形狀照手繪草圖（`Boss Fight` 筆記第 2 頁）：北邊較寬的主區、往南收窄成一條通往傳送門的小路。

| 區域 | 位置（草圖） | 互動 | 內容 |
| --- | --- | --- | --- |
| 中心小屋 | 北側主區中央 | 對村長按 E | 開場劇情、接任務、回報任務、領獎勵；村長頭上顯示「!」（有新任務）或「?」（可回報） |
| 鐵匠鋪 | 東側 | 對鐵砧按 E | 裝備介面：看已解鎖／未解鎖的裝備、更換目前角色的裝備；新解鎖的標「新」 |
| 營火區 | 西側 | 對營火按 E | 角色坐下、鏡頭拉近；介面分「角色」「技能樹」兩頁 |
| 傳送門 | 南端窄路盡頭 | 對傳送門按 E | 挑戰清單（目前只有 OIIA 貓，之後加 Boss 就多一列），確認後換場景 |

暫定尺寸（以休息區中心為原點，數值放 `HubLayoutData`，Builder 讀它）：南北約 50 m、東西約 36 m；小屋約 (0, +14)、鐵匠鋪約 (+12, +6)、營火約 (−10, −4)；南側窄路寬約 6 m，傳送門約 (−2, −24)。邊界用一串折線點描出草圖輪廓，Builder 沿著它種樹、放牆。

- **出生點**：啟動遊戲時在營火旁，從 BossArena 回來時在傳送門前。
- **行動**：休息區照常可以移動、跳、翻滾、揮刀、舉盾（純手感，沒有鎖定目標）；HUD（血條、耐力、骨頭）在休息區不顯示。
- **互動**：走進互動範圍時畫面下方顯示「按 [E] 對話／鍛造／休息／出發」，按鍵名稱用 `BindingText.FirstDisplay`。打開介面時鎖住玩家與鏡頭輸入、放開游標，對應的 Cinemachine 鏡頭提高優先度（營火特寫、鐵匠鋪越肩、村長雙人鏡頭）；Esc／手把 B 關閉。
- **佔位美術**：全部用基本形體，跟現在的 Builder 一樣可重建。小屋是方塊加三角屋頂、村長是戴帽子的膠囊、鐵匠鋪是棚子加鐵砧與爐火光、營火是圍成一圈的木頭加火焰粒子與坐的圓木、傳送門是石拱門加旋轉的紫色光面（顏色呼應 OIIA 貓的魔法陣）。

## 角色系統

### 每個角色一個 prefab

- 角色 prefab 的根物件掛 `PlayerRig`：公開這隻角色身上的 PlayerController、PlayerHealth、PlayerStamina、PlayerAttack、PlayerFeedback、LockOnTarget 等元件，以及鏡頭跟隨點、演出用的錨點。
- 這一輪所有角色共用現有的玩家元件（移動、血量、耐力、翻滾、舉盾、骨頭）；不同角色的差別在 prefab 的模型、`PlayerStats`、裝備欄、技能樹。招式結構完全不同的角色（例如沒有盾）之後再把攻擊拆成可替換的元件，列在待決議事項。
- 刀盾狗改成 `Assets/Prefabs/Characters/DaoDunDog.prefab`（DaoDunDogBuilder 改產 prefab）。
- **測試角色**「測試狗」：刀盾狗的 prefab variant，換顏色、數值不同（例如血量 80、跑速 6），裝備欄只有一格；`CharacterDefinition.developerOnly` 勾起來，只有開發者模式開著時才出現在營火清單。用來驗證切換、獨立數值、獨立裝備欄與技能樹。

### 生成與接線

場景裡不再放玩家物件，改放 `PlayerSpawner`：

1. `PlayerSpawner` 用 `[DefaultExecutionOrder(-1000)]`，在 `Awake` 依存檔的目前角色，從 `CharacterRoster` 找到 prefab，在出生點生成。
2. 套用數值（見下一節「數值套用管線」）。
3. 對 Inspector 裡接好的接收者清單呼叫 `IPlayerBinding.BindPlayer(PlayerRig)`。因為 spawner 的 Awake 最早跑，接收者自己的 `Awake`／`OnEnable` 執行時已經拿到玩家，**照原本的規則在 OnEnable 訂閱事件就好**。
4. 送出 `OnPlayerSpawned(PlayerRig)`。

細節：

- `IPlayerBinding` 與 `PlayerRig` 放在 `BossFight.Player`。Spawner 只認這個介面，Inspector 接線不算編譯期依賴，所以 spawner 可以把玩家交給 UI、Debug 這些上層元件，依賴方向不會反過來。
- 現在用 `[SerializeField]` 接玩家的元件都改成實作 `IPlayerBinding`：BattleManager、CameraController、CutsceneDirector、CutsceneStage、LockOnMarkerUI、PlayerHudUI、BalanceDebugPanel、BossDebugTools、PlayerDebugOverlay，以及新的休息區元件。
- 玩家身上原本引用場景物件的欄位（`cameraTransform`、`lockTargets`、`executionTargets`）由 spawner 生成後填入（`PlayerSpawner` 自己在 Inspector 接好這些場景物件）。
- Boss 不能引用 Player：`BossController` 新增 `SetTarget(Transform)`，由 spawner 呼叫。
- Cinemachine 鏡頭的 Follow／LookAt 由 CameraController 在 `BindPlayer` 時設定。
- **Timeline**：編輯模式需要一隻玩家才能預覽與綁軌道。PlayerSpawner 底下保留一隻 EditorOnly 的預覽模型（`previewCharacter`），Builder 把軌道綁在它身上；進 Play 模式時 spawner 先把預覽模型關掉，CutsceneDirector 在 `BindPlayer` 時用 `SetGenericBinding` 把原本綁預覽模型的軌道改綁到生成的角色。正式 build 不會有預覽模型。
- **換角色**（只在休息區）：銷毀目前的角色、在同一位置生成新角色，再對所有接收者呼叫一次 `BindPlayer`。所以 `BindPlayer` 要處理「換人」：元件已啟用時先退訂舊角色的事件，再訂閱新角色。
- 執行期仍然不用 Find 系列與 singleton。

## 數值套用管線

角色、裝備、技能都用同一套方式改數值，而且不寫入 SO：

```text
CharacterDefinition.baseStats
  → Instantiate 成複本
  → 套用所有「加值」修正（裝備＋已學技能）
  → 套用所有「倍率」修正（相加後一次乘：+10% 與 +20% = ×1.3）
  → PlayerRig.ApplyStats(複本)：換進 PlayerController／Health／Stamina／Attack／CameraController 的 stats 引用
  → 掛上裝備與技能的「效果」
```

- **`StatModifier`**：`PlayerStatId`（列舉）＋運算（加值／倍率）＋數值。列舉用 switch 對應到 `PlayerStats` 的欄位，不用反射。第一批支援：最大血量、最大耐力、耐力回復、跑速、翻滾耐力消耗、格擋減傷、盾反判定時間、盾反失衡值、骨頭持有上限、骨頭回血量、輕攻擊傷害（三段一起）、重擊傷害、處決傷害。新增時在列舉加一項、switch 加一行。
- 套用後超出範圍的值夾回合理範圍（例如格擋減傷最多 1、耐力消耗最少 0）。
- **效果**（`PlayerEffect` SO 的子類別）：數值以外的特殊規則。SO 只放參數，執行時產生一個小元件掛在角色上、訂閱玩家事件，角色銷毀時一起消失。第一個效果是「盾反成功回血」（訂閱 `PlayerHealth.OnParry`）。
- 在休息區換裝或點技能後立即重新套用，並補滿血量與耐力。
- **F12 平衡面板**：spawner 比面板早跑，面板 Awake 時複製的是「套用後」的那份，所以照常可用；面板顯示的是含裝備與技能的值，「存回 .asset」只寫面板上的值，不適合在有裝備時使用 —— 開了裝備或技能時面板標示提醒。

## 裝備系統

- **`EquipmentSlotType`**（SO）：欄位種類。先建「項圈」「護符」兩種，之後加種類不用改程式。
- **`EquipmentData`**（SO）：id、名稱、說明、圖示、欄位種類、`StatModifier` 清單、`PlayerEffect` 清單、可裝備的角色（空 = 全部）。
- **`CharacterDefinition.equipmentSlots`**：這個角色有哪幾格、各是什麼種類（企劃「每個職業的飾品欄位不同」）。刀盾狗：項圈 ×1、護符 ×1；測試狗：護符 ×1。
- 每個角色的穿戴分開記；換角色時各自保留。
- **測試裝備**：
  - 厚皮項圈（項圈）：最大血量 +20。來源：OIIA 貓首通。
  - 反擊護符（護符）：盾反成功時回 5 HP。來源：村長任務。
- **鐵匠鋪介面**：左邊是目前角色的裝備欄，右邊是所有裝備（已解鎖可選、未解鎖顯示剪影與「擊敗 ○○ 取得」之類的來源提示），下方顯示換裝前後的數值差異（綠色變好、紅色變差）。點裝備即穿上、再點卸下。
- 預設是獎勵「直接解鎖」，鐵匠鋪負責查看與更換；「獎勵給圖紙、到鐵匠鋪鍛造」列在待決議事項。

## 技能樹

- **`SkillTreeData`**（SO，每個角色一棵）：節點清單。
- **節點**：id、名稱、說明、圖示、花費（技能點）、前置節點（全部學會才能學）、`StatModifier` 清單、`PlayerEffect` 清單、介面上的格子座標。
- **技能點**：全帳號共用一個池，學在哪個角色身上就扣多少；每個角色的已學節點分開記。
- **重置**：在營火免費重置目前角色，點數全退回池子。
- **刀盾狗測試樹**（共 7 點，目前拿得到 5 點，故意學不完，用來測取捨）：

| 節點 | 效果 | 花費 | 前置 |
| --- | --- | --- | --- |
| 堅韌 | 最大血量 +10 | 1 | — |
| 鐵壁 | 格擋減傷 +0.05 | 1 | 堅韌 |
| 盾反專精 | 盾反失衡值 +5 | 2 | 鐵壁 |
| 輕盈 | 翻滾耐力消耗 −5 | 1 | — |
| 骨頭收藏家 | 骨頭持有上限 +1 | 2 | 輕盈 |

- 測試狗放 2 個節點的小樹，驗證每個角色的樹是分開的。
- **介面**（營火的「技能樹」頁）：節點用格子排、前置用線連；已學亮、可學有外框、不能學變暗；右上顯示剩餘點數；點節點看說明，按「學習」確認。

## 村長與任務

- **`QuestData`**（SO）：id、標題、說明、接任務前的對白、接受後村長補的對白、進行中的對白、回報時的對白、目標、獎勵、前置任務。
- **目標**：先只做「擊敗某個 Boss」（引用 `BossDefinition`）。判定看存檔裡的「已擊敗」紀錄，所以接任務前就打贏也算數。
- **狀態**：未開放 → 可接 → 進行中 → 可回報 → 已完成。
- **對話介面**：畫面下方對話框，顯示說話者名稱與逐字出現的文字；點擊／E／手把 A 先顯示全文、再按下一句。對白有多句時依序播。
- **簡單劇情**：第一次進休息區時，村長自動走過來講開場對白（3–5 句，交代「森林裡出現會轉圈的怪貓」），講完直接接第一個任務的說明與「接受／再想想」（序章修正 I2）；選「再想想」時村長提醒「再來小屋前找我」、頭上亮「!」。對白寫在 `QuestData`（含接受後補的 `acceptedLines`）／`HubStoryData`（含 `declineLines`）資產裡。
- **測試任務**「轉暈那隻小笨貓！」：擊敗 OIIA 貓；獎勵 技能點 ×2、解鎖反擊護符。

## 獎勵

- **`RewardData`**（可序列化的結構）：種類（技能點／解鎖裝備／解鎖角色）＋數量或引用。首通獎勵與任務獎勵都用它。
- **`BossDefinition`**（SO）：id、名稱、場景名稱、首通獎勵清單、傳送門清單裡的說明文字。OIIA 貓首通：技能點 ×3、解鎖厚皮項圈。
- BossArena 勝利時，`BattleManager` 交給 `ProgressRecorder`（Progression）記錄擊敗；首次擊敗就發首通獎勵並存檔。勝利畫面多一區「首次擊敗獎勵」列出拿到的東西；不是首通就不顯示。失敗沒有獎勵。

## 存檔

- **`ProfileStore`**（`BossFight.Core`，static）：只放純資料，存的是 id 字串，不放 SO 或元件引用；Progression 用 `GameDatabase` 把 id 換回資產。
- **存的內容**：存檔版本、目前角色、已解鎖角色、已解鎖裝備（含「新」標記）、各角色的穿戴、技能點池、各角色已學節點、已擊敗的 Boss、各任務狀態、看過開場劇情沒有。
- **不存、只活在這次執行**：這次要在哪個出生點出現（啟動／從戰鬥回來）。
- **格式**：`Application.persistentDataPath/save.json`（JsonUtility）。有變動就存、離開遊戲再存一次；讀檔失敗時把壞檔改名成 `save.bak.json`，開新檔並在 Console 警告。玩家設定照舊存 PlayerPrefs，兩者分開。
- 進 Play 模式時重新從檔案讀（跟 BattleSession 一樣處理 Domain Reload 關閉的情況）。
- **開發者模式**：ESC 選單「開發者模式」下面加「重置存檔」（確認後清空、從序章重新開始）；Debug 面板加「給 10 技能點」「解鎖全部裝備」。
- **`GameDatabase`**（SO）：角色名冊、全部裝備、全部 Boss、全部任務；Editor 工具檢查 id 不重複、沒有空 id。

## 程式架構

### 命名空間與依賴方向

新增兩個系統：

- `BossFight.Progression`：CharacterDefinition、CharacterRoster、StatModifier、PlayerEffect、EquipmentData、EquipmentSlotType、SkillTreeData、QuestData、BossDefinition、RewardData、GameDatabase、PlayerSpawner、PlayerLoadout（數值管線）、ProgressRecorder、SceneFlow。
- `BossFight.Hub`：HubManager（休息區狀態：自由走動／介面開著）、Interactable、InteractionScanner、各站點（Campfire、Blacksmith、VillageChief、Portal）、HubLayoutData。

依賴方向（只能往下）：

```text
Core ← Hazards ← Player ← Camera ← Boss ← Cutscene ← Progression ← Battle／Hub ← UI／Debug
```

- Battle 與 Hub 互不引用；兩邊都只靠 Progression 的 SceneFlow 換場景。
- 休息區的介面（營火、鐵匠鋪、對話、傳送門清單、互動提示）放 `BossFight.UI`，只訂閱 Hub 與 Progression 的事件。

### 新資料夾與資產

| 路徑 | 內容 |
| --- | --- |
| `Scripts/Progression` | 上面列的 Progression 類別 |
| `Scripts/Hub` | 上面列的 Hub 類別 |
| `ScriptableObjects/Progression/` | `GameDatabase`、`CharacterRoster`、`SceneFlowData` |
| `ScriptableObjects/Progression/Characters/` | 刀盾狗、測試狗的 CharacterDefinition 與各自的 PlayerStats |
| `ScriptableObjects/Progression/Equipment/` | 欄位種類、厚皮項圈、反擊護符 |
| `ScriptableObjects/Progression/SkillTrees/` | 刀盾狗、測試狗的技能樹 |
| `ScriptableObjects/Progression/Quests/`、`Bosses/` | 測試任務、OIIA 貓的 BossDefinition |
| `ScriptableObjects/Hub/` | HubLayoutData、休息區天色、HubUIData（介面文字與顏色） |
| `Prefabs/Characters/` | DaoDunDog、DaoDunDog_Test（variant） |
| `Scenes/Hub.unity` | 休息區 |
| `Models/Hub/` | HubBuilder 產生的佔位網格（重建會覆寫） |

### 輸入

- 用現有的 `Player/Interact`。互動要「按一下」就觸發：把它的 Hold 互動拿掉，鍵鼠綁 E、手把綁 Y；鍵鼠補一個空的次要綁定，並加進 `PauseMenuData.bindings`（照 CLAUDE.md 的改鍵規則）。
- 介面裡的確認／返回用 `UI/Submit`、`UI/Cancel`。

### CLAUDE.md 要補的規則（T17 一起改）

- static 例外第二條：`Core/ProfileStore`（規則同上方「存檔」）。
- 玩家改成由 `PlayerSpawner` 生成；要用玩家的元件實作 `IPlayerBinding`，不再用 `[SerializeField]` 直接接玩家；`BindPlayer` 要能處理換人。
- 依賴方向加入 Progression、Hub。
- 新的資料夾、資產、Build Settings 順序（Hub 0、BossArena 1）。
- 角色、裝備、技能的數值只能透過 `StatModifier` 改，不在玩家元件裡加特例。
- 任務驗收表加入 T17–T24；之後所有任務收尾都要確認 T0–T16 的驗收沒有壞掉（`docs/playtest-checklist.md` 快速跑一輪）。

## Claude Code 任務安排

順序的考量：T18（玩家改成生成）動到最多既有程式、風險最大，放在最前面做完並回歸測試，後面的系統都建在它上面。休息區的三個站點（T18–T21）互相獨立，可以依需要調整順序。

| # | 任務 | 驗收方式 |
| --- | --- | --- |
| T17 | 存檔與場景流程 | 新增空的 Hub 場景（地板＋傳送門方塊）；Build Settings Hub 0、BossArena 1；從 Hub 走到傳送門按 E 進 BossArena；勝利後 `save.json` 記錄擊敗 OIIA 貓；勝敗畫面與 ESC 選單「返回休息區」可回到 Hub、出生在傳送門前；關掉重開紀錄還在；壞檔會備份並開新檔；開發者模式可重置存檔 |
| T18 | 角色 prefab 化與生成接線 | 刀盾狗變成 prefab，BossArena 與 Hub 都由 PlayerSpawner 生成；T0–T13 驗收全部照舊（開場、轉階段演出、鎖定、處決、HUD、F12、ESC 選單）；Timeline 編輯模式仍可預覽；執行期沒有 Find 系列呼叫 |
| T19 | 角色定義與數值管線 | CharacterRoster、CharacterDefinition、StatModifier、PlayerEffect；刀盾狗從定義載入；Debug 加一個測試修正（血量 +20、跑速 ×1.2）時 HUD 血條上限、實際跑速、F12 面板都反映；關掉後回到原值；`PlayerStats.asset` 沒有被改到 |
| T20 | 休息區場景 | HubBuilder 照草圖建出邊界、樹木、隱形牆、四個站點佔位、小路、傍晚天色與營火光；走不出邊界；靠近站點出現互動提示（顯示目前的鍵），離開消失；開介面時輸入鎖定、游標放開、鏡頭切到站點鏡頭；休息區 ESC 選單只有繼續、設定、退出 |
| T21 | 營火與角色切換 | 按 E 坐下、鏡頭拉近；「角色」頁列出已解鎖角色（開發者模式多出測試狗）；切換後外觀、數值、HUD 都換成新角色，位置不變；存檔記住目前角色，重開與進 BossArena 都用它 |
| T22 | 裝備系統與鐵匠鋪 | 鐵匠鋪介面顯示目前角色的欄位（刀盾狗 2 格、測試狗 1 格）；Debug 解鎖兩件測試裝備後可穿脫；厚皮項圈讓血量上限 +20；反擊護符盾反回 5 HP（F12 木樁或 Boss 驗證）；換角色後各自的穿戴保留；數值差異預覽正確 |
| T23 | 技能樹 | 營火「技能樹」頁顯示刀盾狗 5 個節點與連線；前置與點數不足時不能學；學「骨頭收藏家」後持有上限 4；重置退回全部點數；測試狗的樹與點數使用分開記錄 |
| T24 | 村長任務與首通獎勵 | 第一次進休息區村長自動講開場；之後村長頭上「!」；接「轉暈那隻小笨貓！」→ 打贏 OIIA 貓 → 勝利畫面顯示首通獎勵（技能點 ×3、厚皮項圈）→ 回休息區村長「?」→ 回報拿到技能點 ×2、反擊護符；第二次打贏不再給首通獎勵；鐵匠鋪新裝備標「新」 |

### T17 存檔與場景流程

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「遊戲流程」「存檔」「程式架構」。
1. 照「CLAUDE.md 要補的規則」更新 CLAUDE.md（static 例外、依賴方向、資料夾、T17–T24 驗收表）。
2. 新增 Core/ProfileStore（JSON 存檔、壞檔備份、進 Play 模式重新讀取）與 Progression/SceneFlow、SceneFlowData（換場景與黑幕淡入淡出）。
3. 新增空的 Hub 場景（地板＋一個傳送門方塊），Build Settings 改成 Hub 0、BossArena 1。傳送門先用最簡單的觸發方式，T20 再換成正式互動。
4. BattleManager 勝利時記錄擊敗；勝敗畫面與 BossArena 的 ESC 選單加「返回休息區」；ESC 開發者模式加「重置存檔」。
先列計畫給我確認再實作。
```

### T18 角色 prefab 化與生成接線

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「角色系統」。把刀盾狗改成 prefab，由 PlayerSpawner 生成：
1. PlayerRig、IPlayerBinding（BossFight.Player）；PlayerSpawner（DefaultExecutionOrder -1000，Awake 生成並 BindPlayer）。
2. 現在用 SerializeField 接玩家的元件全部改成 IPlayerBinding，BindPlayer 要能處理換人（退訂舊的、訂閱新的）；BossController 加 SetTarget。
3. 玩家身上的 cameraTransform、lockTargets、executionTargets 由 spawner 填。
4. Timeline：spawner 底下放 EditorOnly 預覽模型，Builder 綁它；CutsceneDirector 在 BindPlayer 時改綁生成的角色。
5. 所有 Builder 改成產 prefab／接 spawner，重跑一次 Build 流程後場景跟現在一樣能玩。
這是動到最多既有程式的任務：先列出會改的每個檔案與改法給我確認。做完照 docs/playtest-checklist.md 回歸 T0–T13。
```

### T19 角色定義與數值管線

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「角色系統」「數值套用管線」。
新增 CharacterDefinition、CharacterRoster、GameDatabase、StatModifier（PlayerStatId 列舉＋switch）、PlayerEffect 基底與 PlayerLoadout。
PlayerSpawner 生成後依存檔與定義建出 stats 複本、套用修正、ApplyStats、掛效果。
Debug 加一個開關套用測試修正，驗證 HUD、跑速、F12 面板都反映，且 PlayerStats.asset 沒被改。
先列計畫給我確認再實作。
```

### T20 休息區場景

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「休息區」。做 HubBuilder（Tools > Hub > Build Hub）：
照 HubLayoutData 的折線輪廓種 2–3 排樹、放隱形牆；四個站點的佔位模型、連到中心的小路、傍晚天色、霧、營火光。
做 Interactable、InteractionScanner、HubManager 與互動提示 UI；Player/Interact 改成按一下觸發並加進改鍵清單。
每個站點一台 Cinemachine 鏡頭，開介面時切過去；休息區 ESC 選單只有繼續、設定、退出。
站點介面先開一個寫著站點名稱的空面板即可，內容在 T21–T24 做。先列計畫給我確認再實作。
```

### T21 營火與角色切換

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「角色系統」「休息區」。
營火互動：角色坐下（程序化姿勢）、鏡頭拉近、開營火介面（「角色」「技能樹」兩頁，技能樹頁 T23 再做）。
「角色」頁列出已解鎖角色，切換時 spawner 換人並重新 BindPlayer。建立測試狗（prefab variant＋CharacterDefinition，developerOnly）。
先列計畫給我確認再實作。
```

### T22 裝備系統與鐵匠鋪

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「裝備系統」「數值套用管線」。
新增 EquipmentSlotType、EquipmentData、各角色的裝備欄；兩件測試裝備（厚皮項圈、反擊護符，後者用 PlayerEffect 做盾反回血）。
鐵匠鋪介面：欄位、裝備清單（未解鎖顯示來源）、換裝前後數值差異。Debug 加「解鎖全部裝備」。
先列計畫給我確認再實作。
```

### T23 技能樹

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「技能樹」。
新增 SkillTreeData 與節點；刀盾狗 5 節點測試樹、測試狗 2 節點小樹；共用技能點池、各角色分開記已學節點、營火免費重置。
營火「技能樹」頁：格子排列、前置連線、三種狀態、學習確認。Debug 加「給 10 技能點」。
先列計畫給我確認再實作。
```

### T24 村長任務與首通獎勵

```text
讀 CLAUDE.md 和 docs/hub-progression.md 的「村長與任務」「獎勵」。
新增 QuestData、BossDefinition、RewardData、ProgressRecorder；對話介面（逐字、跳全文、下一句）；村長頭上的 ! 與 ?。
第一次進休息區播開場對白；測試任務「轉暈那隻小笨貓!」；OIIA 貓首通獎勵；勝利畫面顯示首通獎勵。
先列計畫給我確認再實作。
```

## 待決議事項

- [x] 角色切換：每個角色獨立 prefab、執行時生成（Oct 23）。
- [x] 獎勵來源：Boss 首通＋村長任務，沒有貨幣（Oct 23）。
- [x] 技能樹入口：營火（Oct 23）。
- [x] 存檔：`ProfileStore` 作為第二個 static 例外（Oct 23）。
- [ ] 鐵匠鋪的「解鎖」：本企劃預設獎勵直接解鎖、鐵匠鋪只負責更換；要不要改成獎勵給圖紙或素材、到鐵匠鋪才鍛造出來？
- [ ] 技能點：預設全帳號共用一池；要不要改成每個角色各自累積？
- [ ] 技能重置：預設在營火免費重置；要不要有代價或次數限制？
- [ ] 裝備欄種類：預設「項圈」「護符」兩種，正式要幾種、每個職業怎麼配？
- [ ] 招式結構不同的角色（沒有盾、遠程）：要做時再把 PlayerAttack／舉盾拆成可替換的元件，那時 PlayerRig 也要跟著調整。
- [ ] 新角色的解鎖方式：任務、首通，還是一開始全開？
- [ ] 休息區能不能揮刀：預設可以（純手感）；要不要在營地放一個常駐的木樁讓玩家練盾反？
- [ ] 開場劇情與村長對白的正式文本。
- [ ] 休息區正式美術：模型來源與 Boss 戰一樣待定（Blender＋Hunyuan3D 路線）。
- [ ] 標題畫面、多存檔欄位：目前範圍外，啟動直接進休息區、單一存檔。
