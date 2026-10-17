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
| `Assets/Scripts/Core` | 跨系統共用：BattleManager（戰鬥流程，T6 只有 Intro → Phase1）、BattleSession（這次執行的挑戰次數與「看過開場」）、事件、傷害系統（DamageInfo、Hitbox、Hurtbox、IDamageable）、畫面停頓（HitStop）、AudioManager（戰鬥音樂與變速、音效）、競技場（ArenaData、ArenaGenerator、天色 ArenaAtmosphere／AtmosphereData）、LockOnTarget、可處決目標（ExecutionTarget） |
| `Assets/Scripts/Player` | PlayerController、PlayerAttack、PlayerHealth、PlayerStamina、PlayerStats 定義、DaoDunDogAnimator |
| `Assets/Scripts/Camera` | CameraController（Cinemachine 第三人稱與鎖定 Boss） |
| `Assets/Scripts/Boss` | BossController 狀態機與選招、BossHealth、失衡值／頭暈／處決（BossStagger）、BossMove 基底類別、BossStats／BossMoveData／BossPhaseData 定義、預兆閃光（BossTelegraphFlash）、佔位模型的程序化姿勢（OiiaCatPoser）、命中火花（BossHitFeedback，Boss 打到玩家）、受擊反饋（BossDamageFeedback，玩家打到 Boss：火花、閃白、後仰、傷害數字）、頭暈星星（BossStunVisual） |
| `Assets/Scripts/Boss/Moves` | 每招一個 BossMove 子類別；招式特有的數值寫成 BossMoveData 的子類別放在同一個資料夾 |
| `Assets/Scripts/Hazards` | 場上判定物件（Hazard 基底、衝擊波 ShockwaveRing、落雷 LightningStrike，之後的龍捲風、安全區）、地面範圍提示（GroundTelegraph，URP Decal）、場地中央的魔法陣（MagicCircle，URP Decal，出現進度／脈動／顏色／半徑）、一次性特效（TimedFx、傷害數字 DamageNumber） |
| `Assets/Scripts/Cutscene` | CutsceneDirector（播開場 Timeline、鎖輸入、長按跳過、字卡事件）、IntroCutsceneSettings、Timeline 自訂 Track／Clip（CurveClip 加 CurveTrack／StormTrack／BossSpinTrack／MagicCircleTrack、ActorWalk、CameraMove、MusicCue、CutsceneEventMarker）、演出物件（ScreenFlash、IntroLightning、SmokePuffs、CameraShaker） |
| `Assets/Scripts/UI` | BattleUI、字卡（TitleCardUI）、跳過提示（SkipPromptUI）、勝敗畫面（只訂閱事件） |
| `Assets/Scripts/Debug` | Debug 快捷鍵 F1–F5 與右上角的 Boss 面板（BossDebugTools，F5 重新載入場景＝重新挑戰）、平衡面板（F12）、左上角的玩家狀態（PlayerDebugOverlay）、測試木樁（TrainingDummy） |
| `Assets/Scripts/Editor` | 編輯器工具（ArenaBuilder、DaoDunDogBuilder、CameraRigBuilder、TrainingDummyBuilder、BossBuilder、HazardBuilder、IntroCutsceneBuilder、PlaceholderAudioBuilder），不會進 build |
| `Assets/ScriptableObjects` | 數值資產（`.asset`），依系統分子資料夾：`Arena/`（ArenaData、Atmosphere 天色）、`Player/`、`Boss/`（BossStats）、`Boss/Phases/`、`Boss/Moves/Phase1/`（第一階段 5 招）、`Boss/Moves/Test/`（T3 假招式）、`Cutscene/`（IntroCutsceneSettings）、`Debug/`（測試木樁） |
| `Assets/Timelines` | Timeline 資產：`Intro.playable`（開場 14.5 秒，鏡頭與各效果的時間點都在這裡調） |
| `Assets/Prefabs` | 玩家、Boss、UI 的 prefab；`Prefabs/Hazards` 是地面提示、Hazard 與特效，由 HazardBuilder 產生，重建會覆寫 |
| `Assets/Shaders` | 自製 shader（都是手寫 JSON 的 URP Decal Shader Graph，Custom Function 用 UV 畫圖）：`TelegraphDecal.shadergraph`（地面範圍提示：圓、環、長方形）、`MagicCircleDecal.shadergraph`（魔法陣：雙環、符文帶、倒五芒星，由中心描繪、外圈分段點亮） |
| `Assets/Scenes` | `BossArena.unity`（`SampleScene` 是範本殘留） |
| `Assets/Audio` | BGM 與音效；OIIA 音樂放 `Assets/Audio/oiia_bgm`，放進來後在場景 `AudioManager` 的 battleMusic 指定（重跑 IntroCutsceneBuilder 也會自動找路徑含 oiia_bgm 的音檔）；`Audio/Placeholder` 是 PlaceholderAudioBuilder 合成的佔位音效，重建會覆寫 |
| `Assets/Models` | 模型、網格、材質；`Models/Arena`、`Models/DaoDunDog`、`Models/TrainingDummy`、`Models/OiiaCat`、`Models/Hazards`、`Models/Cutscene` 由 Builder 自動產生，重建會覆寫網格 |
| `Assets/Settings` | URP 與 Volume 設定（範本自帶）；`PC_Renderer` 加了 Decal Renderer Feature（T4，地面範圍提示用） |
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
- 唯一的 static 例外是 `Core/BattleSession`（第幾次挑戰、看過開場沒有）：重新載入場景後元件都是新的，跨場景的資料要放在活得比場景久的地方。它只放這兩個值、不放元件引用，進 Play 模式時清空；只有 BattleManager 讀寫，其他系統透過 BattleManager 的事件或參數得知。

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

- 輸入只用新版 Input System，從 `InputSystem.actions` 讀 `Player` map；新動作加進 `InputSystem_Actions.inputactions`。`Player/SkipCutscene`（Space、手把 A）是長按跳過開場用的，和 Jump 同鍵；演出期間 PlayerController／CameraController 被 `SetInputLocked` 鎖住，不會跳起來。
- 演出（開場、之後的轉階段）由 CutsceneDirector 鎖輸入：`PlayerController.SetInputLocked`（不讀按鍵，只照 `ScriptedMoveTo` 走）、`CameraController.SetInputLocked`（不轉視角、不能鎖定），結束前用 `CameraController.ResetBehind` 把自由視角擺到玩家背後。
- Timeline 自訂片段要算進度時用片段本身的長度（Track 在 `CreatePlayable` 填進 behaviour 的 `clipDuration`），不要用 `playable.GetDuration()`：片段設成 Hold 延伸時，playable 的長度會包含延伸的部分（約 100 萬秒）。
- Debug 程式整個檔案包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`。
- Layer：`ArenaWall`（8）用於外牆、入口門與門柱，Boss 撞牆判定用；`Hurtbox`（9）是受擊範圍的 trigger，Hitbox 只掃這一層。新增 Layer 時記在這裡。
- 傷害一律走 Hitbox → Hurtbox → `IDamageable.TakeDamage`；無敵、格擋、盾反由被打方自己結算，結果（`DamageResult`）用 `Hitbox.OnHit` 回報攻擊方。處決（F）也走這條路，`DamageInfo.isExecution` 為 true（固定傷害，不吃頭暈倍率）。
- 失衡值：玩家攻擊的值放在 `PlayerStats` 的 AttackSpec.imbalance，跟著 `DamageInfo` 送出；盾反的值是 `PlayerStats.parryImbalance`，跟著 `PlayerHealth.OnParry` 送出。Boss 端的 `BossStagger` 累積，衰減與頭暈倍率在 `BossStats`，上限與頭暈時間在 `BossPhaseData`。招式要自己加失衡值時用 `BossMove.AddImbalance`。
- 玩家的 CharacterController `minMoveDistance` 要保持 0：站著不動時每幀的重力位移比預設的 0.001 m 還小，`isGrounded` 會一幀有一幀沒有，舉盾、攻擊、跳躍都會失靈。
- 搬移或改名資產要在 Unity 裡做（或用 `AssetDatabase.MoveAsset`），保留 `.meta` 與 GUID，引用才不會斷。
- 註解用繁體中文，風格跟現有檔案一致：public 欄位加 `[Tooltip]`，用 `[Header]` 分組，數值欄位加 `[Min]`／`[Range]`。

## 已知技術債

- 刀盾狗 prefab 是用基本形體拼的，沒有骨架，動畫是 DaoDunDogAnimator 的程序化姿勢。
- OIIA 貓（BossArena 中心的 `OiiaCat`，prefab `Assets/Prefabs/OiiaCat.prefab`）是 BossBuilder（Tools > Boss > Build Boss Placeholder）用膠囊拼的佔位物件，還沒有模型與動畫；舉爪、伏低、旋轉等姿勢是 OiiaCatPoser 的程序化動作。
- 第一階段 5 招已在 T4 完成；第二階段的招式池暫時還是 2 個 T3 假招式（`TestFlashMove`，`Test_P2_Near`／`Test_P2_Far`），讓 F1 進第二階段後 Boss 還有招可出。T7 做好強化招式後把它們移出 Boss，連同 `ScriptableObjects/Boss/Moves/Test/` 與 `TestFlashMove` 一起刪掉。
- 招式被打斷（Interrupt）時只會收掉「目前這招」放出的 Hazard；已經結束的招放出的衝擊波、落雷會跑完才消失。T7 轉階段要「清除所有 Hazard」時，要對所有招式呼叫 `ClearHazards`（或加一個 Hazard 總管）。
- 地面範圍提示的 Decal angle fade 沒有作用：紅圈碰到外牆時牆腳會染一條（投影深度已降到 0.3 m 減輕）。之後要乾淨的話改用 Rendering Layers 只投在地板上。
- 開場動畫（T6）由 IntroCutsceneBuilder（Tools > Cutscene > Build Intro）產生場景物件並綁定 Timeline：軌道依類型綁定，同類型的 CurveTrack／AudioTrack／CameraMoveTrack 依**軌道名稱**、鏡頭片段依**片段名稱**對應同名的鏡頭，改名後重建就要手動綁。Timeline 資產只在不存在時建立（Tools > Cutscene > Reset Intro Timeline 會重排），場景物件每次重建。
- Timeline 視窗在編輯模式預覽時，只看得到鏡頭（Cinemachine、CameraMove）、Activation 與聲音；天色、法陣、閃電、白屏、走位、旋轉這些自訂軌道只在 Play 模式套用（避免預覽改到場景存檔）。
- 開場的暴風雨天色會一直維持到戰鬥結束：主光源 1.1 → 0.35，戰鬥畫面比 T0–T5 暗；太暗就調 `ScriptableObjects/Arena/Atmosphere.asset` 的 storm。雷雲、煙霧、閃電柱都是佔位幾何。
- OIIA 音樂的 BPM 還不知道：魔法陣脈動先用每秒 2 下（`MagicCircle.pulseRate`）。音樂變速（T7 的 1.2、1.3 倍）是 `AudioManager.SetMusicSpeed` 改 pitch，音高會跟著變。
- 開場結束時貓的轉速（720 度/秒）留在 OiiaCatPoser 上，只有迴旋衝撞的預兆會從這個轉速接著加速；開場序列第一招以外（例如用 F3 指定別招當第一招）貓會一直轉到那一招 ResetPose。
- T0 的比例參考膠囊 `ScaleRef_2m`（EditorOnly）在編輯器 Play 時會出現在開場的狗身邊；左上、右上的 Debug 面板（IMGUI）也會蓋在開場與字卡上面。正式 build 都不會有。
- 轉階段目前是 `transitionTime` 計時自動結束（第二階段 5 秒），T7 改由轉階段演出呼叫 `BossController.CompleteTransition`。
- `BossStagger` 在 Inspector 引用玩家的 `PlayerHealth` 來訂閱 `OnParry`，是 Boss 唯一引用 Player 命名空間的地方；之後接回主遊戲（多職業）時可以改由 BattleManager 轉發。
- 失衡值在招式之間也會衰減：只靠盾反時，兩次貓掌連拍之間是 破綻 1.2 + 出招間隔 0.8–1.5 + 預兆 0.5 = 2.5–3.2 秒，間隔抽到超過約 1.3 秒，第 4 次盾反會停在 99（中間砍一刀 +3 就補回）。T10 平衡時再看要不要調整衰減延遲。
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
| T2 | 玩家攻防與傷害系統 | 對木樁打出 10 / 10 / 16 三段，第三段前踏；長按 0.8 秒重擊 35、耐力 −30；舉盾擋正面 120°、減傷 80%，耐力歸零破防 1 秒；命中前 0.15 秒內舉盾判定盾反（免傷、停頓 0.1 秒）；R 回 40 HP，每場 3 次；Space 跳躍約 1.2 m |
| T3 | Boss 狀態機、招式資料、選招 AI | 假招式跑完 Idle → ChooseMove → Telegraph → Active → Recovery 循環；選招依距離 → 冷卻 → 加權隨機，且不連出同一招；固定開場序列生效；F1–F4 都可用 |
| T4 | 第一階段 5 招與危害物件 | 用 F3 逐一觸發 5 招；可擋招閃白光、不可擋招閃紅光，格擋與盾反判定正確；迴旋衝撞撞牆後硬直 2 秒；開場固定序列為 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷 |
| T5 | 失衡值、頭暈、處決 | 盾反 4 次讓 Boss 頭暈；頭暈中傷害 ×1.5；F 處決造成 150；處決或頭暈結束後失衡值歸零；3 秒沒有增加後每秒 −8 |
| T6 | 開場動畫 | 14.5 秒照分鏡播完 7 個鏡頭，期間輸入鎖定；結束後直接接迴旋衝撞；第二次挑戰起長按 Space 1 秒可跳過 |
| T7 | 轉階段演出與第二階段強化招式 | F1 把 Boss 設為 1001 再打一下：清除 Hazard、Boss 無敵、輸入鎖定，播完 5 步驟演出；音樂 1.2 倍速；4 個強化招可用 |
| T8 | 龍捲風、幻影分身、終極技、狂暴 | HP 首次 ≤ 400 觸發大迴旋一次，落地頭暈 3 秒；之後進入狂暴（出招間隔 0.3 秒、冷卻 ×0.75、音樂 1.3 倍速）；龍捲風、幻影分身可用 |
| T9 | 戰鬥 UI 與勝敗流程 | 玩家血條、耐力條、骨頭次數、Boss 血條（50% 與 20% 刻度）、失衡條都隨事件更新；BOSS FIGHT 字卡；勝利畫面；失敗可重試，重試後可跳過開場 |
| T10 | 平衡測試與收尾 | F12 面板能即時調整數值；完整打通 3 次，通關時間 4–5 分鐘 |
