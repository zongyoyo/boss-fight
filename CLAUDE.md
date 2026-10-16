# OIIA 貓 Boss 戰（Unity 原型）

## 專案目標

- 一場可獨立遊玩的 Boss 戰：玩家操作刀盾狗，在半徑 12 m 的圓形競技場挑戰兩階段的 OIIA 貓，目標通關時間 4–5 分鐘。
- 用途是先驗證戰鬥系統、Boss AI 框架與過場演出流程，之後接回關卡挑戰制的主遊戲；所以系統之間要好拆。
- **規格以 `docs/boss-oiia.md` 為準。** 數值、招式、分鏡都照企劃書做；發現企劃書與程式衝突，或企劃書沒寫清楚時，先問，不要自行改規格。

## 環境

- Unity 6000.3.25f1、URP 17.3（`Assets/Settings/PC_RPAsset`）
- Input System 1.20：使用 Project-wide Actions `Assets/InputSystem_Actions.inputactions` 的 `Player` map，不用舊版 Input Manager
- Cinemachine 3.1：namespace 是 `Unity.Cinemachine`，元件是 `CinemachineCamera`（不是 2.x 的 `CinemachineVirtualCamera`）
- Timeline 1.8
- 主場景：`Assets/Scenes/BossArena.unity`（Build Settings 第 0 個）
- 單位：距離為公尺、時間為秒、角度為度。俯視角度 0 = 北（+Z），順時針遞增；玩家從南側（180°）進場，Boss 在場地中心。

## 資料夾

| 路徑 | 用途 |
| --- | --- |
| `Assets/Scripts/Core` | 跨系統共用：BattleManager、事件、傷害系統（DamageInfo、Hitbox、Hurtbox、IDamageable）、畫面停頓（HitStop）、AudioManager、競技場（ArenaData、ArenaGenerator）、LockOnTarget |
| `Assets/Scripts/Player` | PlayerController、PlayerAttack、PlayerHealth、PlayerStamina、PlayerStats 定義、DaoDunDogAnimator |
| `Assets/Scripts/Camera` | CameraController（Cinemachine 第三人稱與鎖定 Boss） |
| `Assets/Scripts/Boss` | BossController 狀態機與選招、BossHealth、BossMove 基底類別、BossStats／BossMoveData／BossPhaseData 定義、預兆閃光（BossTelegraphFlash） |
| `Assets/Scripts/Boss/Moves` | 每招一個 BossMove 子類別；招式特有的數值寫成 BossMoveData 的子類別放在同一個資料夾 |
| `Assets/Scripts/Hazards` | 衝擊波、落雷、龍捲風、安全區等場上判定物件 |
| `Assets/Scripts/Cutscene` | CutsceneDirector、Timeline 自訂 Track／Clip |
| `Assets/Scripts/UI` | BattleUI、字卡、勝敗畫面（只訂閱事件） |
| `Assets/Scripts/Debug` | Debug 快捷鍵 F1–F4 與右上角的 Boss 面板（BossDebugTools）、平衡面板（F12）、左上角的玩家狀態（PlayerDebugOverlay）、測試木樁（TrainingDummy） |
| `Assets/Scripts/Editor` | 編輯器工具（ArenaBuilder、DaoDunDogBuilder、CameraRigBuilder、TrainingDummyBuilder、BossBuilder），不會進 build |
| `Assets/ScriptableObjects` | 數值資產（`.asset`），依系統分子資料夾：`Arena/`、`Player/`、`Boss/`（BossStats）、`Boss/Phases/`、`Boss/Moves/`（`Moves/Test/` 是 T3 假招式）、`Debug/`（測試木樁） |
| `Assets/Prefabs` | 玩家、Boss、Hazard、UI 的 prefab |
| `Assets/Scenes` | `BossArena.unity`（`SampleScene` 是範本殘留） |
| `Assets/Audio` | BGM 與音效；OIIA 音樂放 `Assets/Audio/oiia_bgm` |
| `Assets/Models` | 模型、網格、材質；`Models/Arena`、`Models/DaoDunDog`、`Models/TrainingDummy`、`Models/OiiaCat` 由 Builder 自動產生，重建會覆寫網格 |
| `Assets/Settings` | URP 與 Volume 設定（範本自帶） |
| `docs/` | 企劃書 |

ScriptableObject 的「類別」寫在 `Scripts/` 對應的系統資料夾，「資產」放在 `ScriptableObjects/`。

## 程式規範

### 數值一律放 ScriptableObject，不寫死

- 所有玩法數值（HP、傷害、速度、距離、時間、冷卻、耐力、失衡值、機率權重…）都定義在 ScriptableObject；MonoBehaviour 用 `[SerializeField]` 引用 SO 來讀。
- 程式裡不出現玩法相關的魔術數字。可以直接寫的只有 0、1、數學常數，以及純視覺或編輯器工具的參數（例如 Gizmo 顏色）。
- 調平衡只改 `.asset`，不改程式。
- 執行期不要寫入 SO 的欄位：SO 是共用資產，Play 模式中改了會留在檔案裡。需要可變的狀態（目前 HP、剩餘冷卻）就放在元件自己的欄位。
- 競技場半徑、牆高、出生點在 `ScriptableObjects/Arena/ArenaData.asset`；Boss 衝撞、安全區、衝擊波推人等玩法也讀這一份。

### 系統之間用 C# event 溝通

- 發送方宣告 `public event Action<...> OnXxx;`，命名用 `On` + 發生的事：`OnBossHealthChanged`、`OnPhaseChanged`、`OnParry`。
- 訂閱方在 `OnEnable` 訂閱、`OnDisable` 退訂。
- UI 與演出只訂閱事件，不直接讀其他元件的欄位。
- 引用用 `[SerializeField]` 在 Inspector 接好；同一個 GameObject 上的元件可以在 `Awake` 用 `GetComponent` 取。
- **執行期程式禁用** `FindObjectOfType`、`FindAnyObjectByType`、`FindFirstObjectByType`、`FindObjectsByType`、`GameObject.Find`、`FindWithTag`，也不用 static singleton 互相存取。`Scripts/Editor` 底下的編輯器工具不受此限。

### 命名空間

`BossFight.<資料夾>`，但下面三個例外是為了不遮蔽 Unity 的型別（在 `BossFight.*` 底下，`BossFight.Camera` 會讓所有 `Camera.main` 編譯失敗，`Debug`、`Editor` 同理）：

| 資料夾 | 命名空間 |
| --- | --- |
| Core / Player / Boss / Boss/Moves / Hazards / Cutscene / UI | `BossFight.Core`、`BossFight.Player`、`BossFight.Boss`、`BossFight.Boss.Moves`、`BossFight.Hazards`、`BossFight.Cutscene`、`BossFight.UI` |
| Camera | `BossFight.Cameras` |
| Debug | `BossFight.DebugTools` |
| Editor | `BossFight.EditorTools` |

不要宣告名為 `Boss` 的類別（會跟 `BossFight.Boss` 命名空間衝突）。

### 其他

- 輸入只用新版 Input System，從 `InputSystem.actions` 讀 `Player` map；新動作加進 `InputSystem_Actions.inputactions`。
- Debug 程式整個檔案包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`。
- Layer：`ArenaWall`（8）用於外牆、入口門與門柱，Boss 撞牆判定用；`Hurtbox`（9）是受擊範圍的 trigger，Hitbox 只掃這一層。新增 Layer 時記在這裡。
- 傷害一律走 Hitbox → Hurtbox → `IDamageable.TakeDamage`；無敵、格擋、盾反由被打方自己結算，結果（`DamageResult`）用 `Hitbox.OnHit` 回報攻擊方。
- 玩家的 CharacterController `minMoveDistance` 要保持 0：站著不動時每幀的重力位移比預設的 0.001 m 還小，`isGrounded` 會一幀有一幀沒有，舉盾、攻擊、跳躍都會失靈。
- 搬移或改名資產要在 Unity 裡做（或用 `AssetDatabase.MoveAsset`），保留 `.meta` 與 GUID，引用才不會斷。
- 註解用繁體中文，風格跟現有檔案一致：public 欄位加 `[Tooltip]`，用 `[Header]` 分組，數值欄位加 `[Min]`／`[Range]`。

## 已知技術債

- 刀盾狗 prefab 是用基本形體拼的，沒有骨架，動畫是 DaoDunDogAnimator 的程序化姿勢。
- OIIA 貓（BossArena 中心的 `OiiaCat`，prefab `Assets/Prefabs/OiiaCat.prefab`）是 BossBuilder（Tools > Boss > Build Boss Placeholder）用膠囊拼的佔位物件，還沒有模型與動畫。
- Boss 的招式清單目前只有 5 個 T3 假招式（`TestFlashMove`，資料在 `ScriptableObjects/Boss/Moves/Test/`），第一階段的開場序列也還是「假招式·遠 → 近 → 中」。T4／T7 做正式招式時，把假招式移出 BossController 的招式清單，並改 `Phase1.asset` 的開場序列。
- BossController 的 `startOnPlay` 是開著的（進 Play 就開打）。T6 有開場演出與 BattleManager 後要關掉，改在演出結束時呼叫 `StartBattle`。
- 轉階段目前是 `transitionTime` 計時自動結束（第二階段 5 秒），T7 改由轉階段演出呼叫 `BossController.CompleteTransition`。
- `BossPhaseData.contactDamage`（接觸傷害）只有數值，規則（可否格擋、判定間隔、哪些狀態有效）待企劃書確定後再實作。
- 測試木樁 `TrainingDummy` 已經在 T3 從場景移除（程式與 `TrainingDummyData.asset` 還在 `Scripts/Debug`、`ScriptableObjects/Debug`）。要練格擋或盾反時用 Tools > Debug > Build Training Dummy 重建，它會自己加進鎖定清單；正式 build 前要刪掉物件，否則會變成 Missing Script。

## 工作流程

1. 一個任務開一次對話。開始前先讀這份檔案，以及 `docs/boss-oiia.md` 的對應章節。
2. 先列計畫，確認後再實作。
3. 實作完依序確認：Unity 編譯沒有錯誤 → 進 Play 模式驗收（下表）→ 通過後 commit，訊息格式 `T<n>: <摘要>`，然後才做下一個任務。
4. 每個任務收尾都要檢查：
   - Console 沒有新的錯誤或警告。
   - 新程式沒有寫死的玩法數值。
   - 執行期程式沒有 Find 系列呼叫：`rg "Find(Any|First)?Object(s)?(OfType|ByType)|GameObject\.Find|FindWithTag" Assets/Scripts --glob "!Editor/**"` 應該沒有結果。

## 任務驗收

全部在 `BossArena` 場景進 Play 模式驗收。

| # | 任務 | 驗收方式 |
| --- | --- | --- |
| T0 | 專案骨架與 CLAUDE.md | Console 沒有錯誤；刀盾狗站在南側出生點（離中心 8 m）；從中心往 8 個方向打射線，都在約 12 m 處打到 `ArenaWall`；天空是暗灰藍的雷雲佔位 |
| T1 | 玩家移動、鏡頭鎖定、翻滾、耐力 | 依鏡頭方向以 5 m/s 移動；中鍵鎖定 20 m 內的假 Boss，鎖定時繞行，再按一次解除；Left Alt 翻滾 3 m、前 0.3 秒無敵、耐力 −25；耐力每秒回 30、舉盾時 10；數值都在 PlayerStats |
| T2 | 玩家攻防與傷害系統 | 對木樁打出 10 / 10 / 16 三段，第三段前踏；長按 0.8 秒重擊 35、耐力 −30；舉盾擋正面 120°、減傷 80%，耐力歸零破防 1 秒；命中前 0.15 秒內舉盾判定盾反（免傷、停頓 0.1 秒）；R 回 40 HP，每場 3 次；Space 跳躍約 1 m |
| T3 | Boss 狀態機、招式資料、選招 AI | 假招式跑完 Idle → ChooseMove → Telegraph → Active → Recovery 循環；選招依距離 → 冷卻 → 加權隨機，且不連出同一招；固定開場序列生效；F1–F4 都可用 |
| T4 | 第一階段 5 招與危害物件 | 用 F3 逐一觸發 5 招；可擋招閃白光、不可擋招閃紅光，格擋與盾反判定正確；迴旋衝撞撞牆後硬直 2 秒；開場固定序列為 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷 |
| T5 | 失衡值、頭暈、處決 | 盾反 4 次讓 Boss 頭暈；頭暈中傷害 ×1.5；F 處決造成 150；處決或頭暈結束後失衡值歸零；3 秒沒有增加後每秒 −8 |
| T6 | 開場動畫 | 14.5 秒照分鏡播完 7 個鏡頭，期間輸入鎖定；結束後直接接迴旋衝撞；第二次挑戰起長按 Space 1 秒可跳過 |
| T7 | 轉階段演出與第二階段強化招式 | F1 把 Boss 設為 1001 再打一下：清除 Hazard、Boss 無敵、輸入鎖定，播完 5 步驟演出；音樂 1.2 倍速；4 個強化招可用 |
| T8 | 龍捲風、幻影分身、終極技、狂暴 | HP 首次 ≤ 400 觸發大迴旋一次，落地頭暈 3 秒；之後進入狂暴（出招間隔 0.3 秒、冷卻 ×0.75、音樂 1.3 倍速）；龍捲風、幻影分身可用 |
| T9 | 戰鬥 UI 與勝敗流程 | 玩家血條、耐力條、骨頭次數、Boss 血條（50% 與 20% 刻度）、失衡條都隨事件更新；BOSS FIGHT 字卡；勝利畫面；失敗可重試，重試後可跳過開場 |
| T10 | 平衡測試與收尾 | F12 面板能即時調整數值；完整打通 3 次，通關時間 4–5 分鐘 |
