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
| `Assets/Scripts/Battle` | 最上層的流程指揮：BattleManager（戰鬥流程狀態機 Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk → Victory／Defeat），指揮 Boss、玩家與演出（播轉階段演出、結束時呼叫 `BossController.CompleteTransition`、換階段改音樂速度；勝利時慢動作與音樂淡出、送 `OnBattleResult`、`Retry` 重新載入場景）；BattleSettings（勝敗節奏） |
| `Assets/Scripts/Core` | 跨系統共用（最底層，不引用其他系統）：BattleSession（這次執行的挑戰次數與「看過開場」）、事件、傷害系統（DamageInfo、Hitbox、Hurtbox、IDamageable）、畫面停頓（HitStop）、AudioManager（戰鬥音樂與變速、音效）、競技場（ArenaData、ArenaGenerator、天色 ArenaAtmosphere／AtmosphereData）、LockOnTarget、可處決目標（ExecutionTarget）、打擊回饋的資料與播放（CombatFeedbackData、FeedbackEmitter：音效＋Cinemachine Impulse 鏡頭震動；Core 只有這裡用到 Unity.Cinemachine） |
| `Assets/Scripts/Player` | PlayerController、PlayerAttack、PlayerHealth、PlayerStamina、PlayerStats 定義、DaoDunDogAnimator、打擊回饋（PlayerFeedback：揮刀、命中、格擋、盾反、受擊等的音效與震動） |
| `Assets/Scripts/Camera` | CameraController（Cinemachine 第三人稱與鎖定鏡頭；鎖定哪個目標由 PlayerController 決定，鏡頭訂閱 `OnLockTargetChanged`） |
| `Assets/Scripts/Boss` | BossController 狀態機與選招、BossHealth、失衡值／頭暈／處決（BossStagger）、BossMove 基底類別、BossStats／BossMoveData／BossPhaseData 定義、預兆閃光（BossTelegraphFlash）、佔位模型的程序化姿勢（OiiaCatPoser）、命中火花（BossHitFeedback，Boss 打到玩家）、受擊反饋（BossDamageFeedback，玩家打到 Boss：火花、閃白、後仰、傷害數字）、頭暈星星（BossStunVisual）、強化階段外觀（BossPhaseVisual：紅色電光與殘影）、幻影分身的幻影（BossPhantom，一擊即碎）、招式衝擊與預兆的音效與震動（BossImpactFeedback，訂閱 `OnMoveTelegraph`／`OnMoveImpact`） |
| `Assets/Scripts/Boss/Moves` | 每招一個 BossMove 子類別；招式特有的數值寫成 BossMoveData 的子類別放在同一個資料夾；PounceRunner 是一次飛撲（扭屁股飛撲與幻影分身共用） |
| `Assets/Scripts/Hazards` | 場上判定物件（Hazard 基底、衝擊波 ShockwaveRing、落雷 LightningStrike、龍捲風 TornadoVortex、大迴旋的全場判定 GrandSpinField 與安全區 SafeZone）、地面範圍提示（GroundTelegraph，URP Decal）、場地中央的魔法陣（MagicCircle，URP Decal，出現進度／脈動／顏色／半徑、幻影分身的閃爍、大迴旋安全區的符文熄滅）、一次性特效（TimedFx、傷害數字 DamageNumber）、佔位電光（ElectricArcs，Boss 身上與轉階段的場地邊緣共用） |
| `Assets/Scripts/Cutscene` | CutsceneDirector（播一段演出的 Timeline、鎖輸入、長按跳過、字卡事件；開場與轉階段各一個物件）、CutsceneStage（轉階段時依玩家位置擺演出鏡頭）、IntroCutsceneSettings、Timeline 自訂 Track／Clip（CurveClip 加 CurveTrack／StormTrack／BossSpinTrack／MagicCircleTrack／ElectricArcsTrack、ActorWalk、ActorPush、BossActor、CameraMove、MusicCue、CutsceneEventMarker）、演出物件（ScreenFlash、IntroLightning、SmokePuffs、CameraShaker、ExpandingRing） |
| `Assets/Scripts/UI` | 只訂閱事件（uGUI）：BattleUI（HUD 何時出現、Boss 血條滑入）、PlayerHudUI（左下血條、耐力、骨頭）、BossHudUI（下方置中 Boss 名稱、血條與刻度、失衡條）、LockOnMarkerUI、BattleResultUI（勝敗畫面與重試按鈕）、ResourceBar（量條與延遲扣血）、BattleUIData；開場的字卡（TitleCardUI）、跳過提示（SkipPromptUI） |
| `Assets/Scripts/Debug` | Debug 快捷鍵 F1–F7 與右上角的 Boss 面板（BossDebugTools，F5 重新挑戰＝`BattleManager.Retry`、F6 Boss 血量歸零、F7 玩家血量歸零）、平衡面板（BalanceDebugPanel，F12：執行期覆寫 SO 數值、計時與統計，見下方「平衡面板」）、左上角的玩家狀態（PlayerDebugOverlay）、測試木樁（TrainingDummy） |
| `Assets/Scripts/Editor` | 編輯器工具（ArenaBuilder、DaoDunDogBuilder、CameraRigBuilder、TrainingDummyBuilder、BossBuilder、HazardBuilder、IntroCutsceneBuilder、TransitionCutsceneBuilder、BattleUIBuilder、PlaceholderAudioBuilder、BalancePanelBuilder、CombatFeedbackBuilder），不會進 build |
| `Assets/ScriptableObjects` | 數值資產（`.asset`），依系統分子資料夾：`Arena/`（ArenaData、Atmosphere 天色）、`Player/`、`Boss/`（BossStats）、`Boss/Phases/`、`Boss/Moves/Phase1/`（第一階段 5 招）、`Boss/Moves/Phase2/`（第二階段 4 個強化招，和第一階段同一招共用類別；新招龍捲風、幻影分身與終極技大迴旋）、`Cutscene/`（IntroCutsceneSettings）、`Battle/`（BattleSettings）、`UI/`（BattleUIData：血條刻度、動畫時間、顏色、文字）、`Feedback/`（CombatFeedback：打擊回饋的音效、音量、鏡頭震動）、`Debug/`（測試木樁） |
| `Assets/Timelines` | Timeline 資產：`Intro.playable`（開場 14.5 秒）、`Transition.playable`（轉階段 5.0 秒）；鏡頭與各效果的時間點都在這裡調 |
| `Assets/Prefabs` | 玩家、Boss（`OiiaCat`、幻影 `OiiaCatPhantom`，BossBuilder 產生）、UI 的 prefab；`Prefabs/Hazards` 是地面提示、Hazard 與特效，由 HazardBuilder 產生，重建會覆寫 |
| `Assets/Shaders` | 自製 shader（都是手寫 JSON 的 URP Decal Shader Graph，Custom Function 用 UV 畫圖）：`TelegraphDecal.shadergraph`（地面範圍提示：圓、環、長方形）、`MagicCircleDecal.shadergraph`（魔法陣：雙環、符文帶、倒五芒星，由中心描繪、外圈分段點亮；`_Holes` 加上 Size.y、Params.w 是大迴旋安全區的熄滅區） |
| `Assets/Scenes` | `BossArena.unity`（`SampleScene` 是範本殘留） |
| `Assets/Audio` | BGM 與音效；OIIA 音樂放 `Assets/Audio/oiia_bgm`，放進來後在場景 `AudioManager` 的 battleMusic 指定（重跑 IntroCutsceneBuilder 也會自動找路徑含 oiia_bgm 的音檔）；`Audio/Placeholder` 是 PlaceholderAudioBuilder 合成的佔位音效（戰鬥音效是 `Sfx_Combat_*`），重建會覆寫 |
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
- 依賴方向只能往下，不能互相引用（之後接回主遊戲、加 asmdef 才拆得開）：
  Core（最底層）← Hazards ← Player ← Camera ← Boss ← Cutscene ← Battle ← UI／Debug。Boss 不引用 Player、Player 不引用 Camera、Core 不引用任何系統。
  需要反方向的資訊時，由下層發事件或把結果放進回傳值（例如盾反失衡值走 `DamageOutcome`／`HitReport`）。新增 `using BossFight.*` 前先確認方向。
- 唯一的 static 例外是 `Core/BattleSession`（第幾次挑戰、看過開場沒有）：重新載入場景後元件都是新的，跨場景的資料要放在活得比場景久的地方。它只放這兩個值、不放元件引用，進 Play 模式時清空；只有 BattleManager 讀寫，其他系統透過 BattleManager 的事件或參數得知。
  Debug 另有一個例外：`Debug/BalanceDebugPanel` 用 static 欄位保留 SO 複本與每場紀錄，讓 F12 的調整值在重試後還在；整個檔案包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`，進 Play 模式時清空、離開時銷毀複本。遊戲程式不能讀它。

### 命名空間

`BossFight.<資料夾>`，但下面三個例外是為了不遮蔽 Unity 的型別（在 `BossFight.*` 底下，`BossFight.Camera` 會讓所有 `Camera.main` 編譯失敗，`Debug`、`Editor` 同理）：

| 資料夾 | 命名空間 |
| --- | --- |
| Battle / Core / Player / Boss / Boss/Moves / Hazards / Cutscene / UI | `BossFight.Battle`、`BossFight.Core`、`BossFight.Player`、`BossFight.Boss`、`BossFight.Boss.Moves`、`BossFight.Hazards`、`BossFight.Cutscene`、`BossFight.UI` |
| Camera | `BossFight.Cameras` |
| Debug | `BossFight.DebugTools` |
| Editor | `BossFight.EditorTools` |

不要宣告名為 `Boss` 的類別（會跟 `BossFight.Boss` 命名空間衝突）。

### 其他

- 輸入只用新版 Input System，從 `InputSystem.actions` 讀 `Player` map；新動作加進 `InputSystem_Actions.inputactions`。`Player/SkipCutscene`（Space、手把 A）是長按跳過開場用的，和 Jump 同鍵；演出期間 PlayerController／CameraController 被 `SetInputLocked` 鎖住，不會跳起來。
- 演出（開場、之後的轉階段）由 CutsceneDirector 鎖輸入：`PlayerController.SetInputLocked`（不讀按鍵、不能切換鎖定，只照 `ScriptedMoveTo` 走）、`CameraController.SetInputLocked`（不轉視角），結束前先 `PlayerController.ClearLockTarget` 再用 `CameraController.ResetBehind` 把自由視角擺到玩家背後。
- 鎖定（中鍵）由 PlayerController 負責：鎖定清單 `lockTargets` 在玩家身上（Builder 會自動加），結果用 `OnLockTargetChanged` 送出，鏡頭與 Debug 面板訂閱。
- Timeline 自訂片段要算進度時用片段本身的長度（Track 在 `CreatePlayable` 填進 behaviour 的 `clipDuration`），不要用 `playable.GetDuration()`：片段設成 Hold 延伸時，playable 的長度會包含延伸的部分（約 100 萬秒）。
- Debug 程式整個檔案包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`。
- Layer：`ArenaWall`（8）用於外牆、入口門與門柱，Boss 撞牆判定用；`Hurtbox`（9）是受擊範圍的 trigger，Hitbox 只掃這一層。新增 Layer 時記在這裡。
- 傷害一律走 Hitbox → Hurtbox → `IDamageable.TakeDamage`；無敵、格擋、盾反由被打方自己結算，結果（`DamageResult`）用 `Hitbox.OnHit` 回報攻擊方。處決（F）也走這條路，`DamageInfo.isExecution` 為 true（固定傷害，不吃頭暈倍率）。
- 失衡值：玩家攻擊的值放在 `PlayerStats` 的 AttackSpec.imbalance，跟著 `DamageInfo` 送出；盾反的值是 `PlayerStats.parryImbalance`，由 `PlayerHealth.TakeDamage` 放進回傳的 `DamageOutcome.counterImbalance`，經 `HitReport` 回到攻擊方（BossStagger 從 `BossController.OnMoveHit` 拿）。Boss 端的 `BossStagger` 累積，衰減與頭暈倍率在 `BossStats`，上限與頭暈時間在 `BossPhaseData`。招式要自己加失衡值時用 `BossMove.AddImbalance`。
- 玩家的 CharacterController `minMoveDistance` 要保持 0：站著不動時每幀的重力位移比預設的 0.001 m 還小，`isGrounded` 會一幀有一幀沒有，舉盾、攻擊、跳躍都會失靈。
- 搬移或改名資產要在 Unity 裡做（或用 `AssetDatabase.MoveAsset`），保留 `.meta` 與 GUID，引用才不會斷。
- 註解用繁體中文，風格跟現有檔案一致：public 欄位加 `[Tooltip]`，用 `[Header]` 分組，數值欄位加 `[Min]`／`[Range]`。

## 已知技術債

- 刀盾狗 prefab 是用基本形體拼的，沒有骨架，動畫是 DaoDunDogAnimator 的程序化姿勢。
- OIIA 貓（BossArena 中心的 `OiiaCat`，prefab `Assets/Prefabs/OiiaCat.prefab`）是 BossBuilder（Tools > Boss > Build Boss Placeholder）用膠囊拼的佔位物件，還沒有模型與動畫；舉爪、伏低、旋轉等姿勢是 OiiaCatPoser 的程序化動作。
- 第二階段 4 個強化招沿用第一階段的類別（PawSwipeMove、SpinChargeMove、SpinShockwaveMove、ThunderCircleMove），差別全在 `Boss/Moves/Phase2/` 的資料（最後一下強化、連撞次數、圈數、波數）。企劃書沒寫的距離、冷卻、權重先照第一階段同一招；三連拍第 2 → 3 下間隔 0.8 秒、連環衝撞每次撞牆後蓄力 0.6 秒、追蹤落雷第二波在第一波落雷同一刻出現，都是 T7 計畫的預設值，T10 平衡時再看。
- T8 新招都是計畫確認過的預設值，T10 平衡時再看：
  - 龍捲風：不限距離、冷卻 12 秒；判定半徑 1.8 m、高 4 m（跳不過，翻滾可閃）；追蹤時 Boss 穿過玩家；失衡 +40 在硬直開始時加（加滿直接頭暈）。玩家受擊無敵剛好 0.5 秒，所以閃過的那一跳會在下一幀重試（TornadoVortex），站在漩渦裡就是每 0.5 秒 10。
  - 幻影分身：距離 3–6 m、冷卻 5 秒照扭屁股飛撲；召喚 1.0 秒，幻影在本體左右各 3 m；幻影隨機順序先撲、本體最後，每隻伏低 0.6 秒、相隔 0.8 秒；三隻都可擋可盾反，盾反幻影也加 Boss 失衡；幻影被打一下就碎、不扣 Boss 血；飛撲判定用 `Hitbox.Filter` 排除 Boss 本體與幻影。項圈只在這招期間亮。
  - 大迴旋：狂暴 `BossPhaseData.entryMove`（BossBuilder 會在空的時候填上）。血量首次 ≤ 400 時打斷一切（含頭暈）、清 Hazard，立刻出（MoveChoiceSource.PhaseEntry）；上升 1.0 秒到 4 m、倒數 4 秒，浮空到落地無敵；安全區半徑 2 m、圓心離中心 4–8.5 m、彼此至少 8 m，玩家 Hurtbox 中心在圈內才安全；落地判定高 6 m，翻滾無敵仍可閃；落地頭暈 3 秒，頭暈結束（或被處決）才套用狂暴與 1.3 倍音樂。倒數數字是 DamageNumber 佔位（T9 沒改，之後要的話再做成 UI）。F3 也能單獨出大迴旋，但不會換階段。
- 大迴旋與幻影分身引用場景的 MagicCircle：BossBuilder 與 IntroCutsceneBuilder 都會用 `BossBuilder.WireMagicCircle` 接上；沒接時只是沒有閃爍與符文熄滅。
- 招式被打斷（Interrupt）時只會收掉「目前這招」放出的 Hazard；已經結束的招放出的衝擊波、落雷會跑完才消失。轉階段（`BeginTransition`）、大迴旋開始、Boss 倒下都會呼叫 `BossController.ClearAllHazards` 收掉所有招式的 Hazard。
- 預兆閃光訂閱 `BossController.OnMoveTelegraph`：進入預兆時依 `BossMoveData.canBlock` 送一次；一招裡某一下要換顏色（例如三連拍第三下紅光）由招式呼叫 `BossMove.Telegraph(canBlock, duration)`，那一下的地面範圍與 `DamageInfo` 由招式自己換（PawSwipeData 的 finisher 欄位）。
- Boss 走位逾時：為了走進某招距離範圍連續走位超過 `BossPhaseData.repositionTimeout`（目前 3 秒）還走不到，開場序列跳過那招、一般選招直接出正在走向的那招（Debug 面板顯示「走位逾時，直接出」）。企劃書沒寫這條，是 Boss 比玩家慢、玩家一直拉開或貼身時不會卡死的保險。
- 地面範圍提示的 Decal angle fade 沒有作用：紅圈碰到外牆時牆腳會染一條（投影深度已降到 0.3 m 減輕）。之後要乾淨的話改用 Rendering Layers 只投在地板上。
- 開場動畫（T6）由 IntroCutsceneBuilder（Tools > Cutscene > Build Intro）產生場景物件並綁定 Timeline（最後也會重建轉階段演出，因為它引用重建後的 MagicCircle、AudioManager、BattleManager；重建 Boss 之後要再跑一次 Build Intro）：軌道依類型綁定，同類型的 CurveTrack／AudioTrack／CameraMoveTrack 依**軌道名稱**、鏡頭片段依**片段名稱**對應同名的鏡頭，改名後重建就要手動綁。Timeline 資產只在不存在時建立（Tools > Cutscene > Reset Intro Timeline 會重排），場景物件每次重建。
- Timeline 視窗在編輯模式預覽時，只看得到鏡頭（Cinemachine、CameraMove）、Activation 與聲音；天色、法陣、閃電、白屏、走位、旋轉這些自訂軌道只在 Play 模式套用（避免預覽改到場景存檔）。
- 開場的暴風雨天色會一直維持到戰鬥結束：主光源 1.1 → 0.35，戰鬥畫面比 T0–T5 暗；太暗就調 `ScriptableObjects/Arena/Atmosphere.asset` 的 storm。雷雲、煙霧、閃電柱都是佔位幾何。
- OIIA 音樂的 BPM 還不知道：魔法陣脈動先用每秒 2 下（`MagicCircle.pulseRate`）。音樂變速（T7 的 1.2、1.3 倍）是 `AudioManager.SetMusicSpeed` 改 pitch，音高會跟著變。
- 開場結束時貓的轉速（720 度/秒）留在 OiiaCatPoser 上，只有迴旋衝撞的預兆會從這個轉速接著加速；開場序列第一招以外（例如用 F3 指定別招當第一招）貓會一直轉到那一招 ResetPose。
- T0 的比例參考膠囊 `ScaleRef_2m`（EditorOnly）在編輯器 Play 時會出現在開場的狗身邊；左上、右上的 Debug 面板（IMGUI）也會蓋在開場與字卡上面。正式 build 都不會有。
- 轉階段（T7）：Boss 血量過門檻時自己進入 Transition（無敵、打斷招式、清 Hazard）；BattleManager 播 TransitionCutscene，演出結束時呼叫 `BossController.CompleteTransition` 套用下一階段。有接演出時 BattleManager 會關掉 `BossController.AutoCompleteTransition`；沒接演出時才照 `BossPhaseData.transitionTime` 計時自動結束。演出由 TransitionCutsceneBuilder（Tools > Cutscene > Build Transition／Reset Transition Timeline）建立，鏡頭在執行時由 CutsceneStage 依玩家位置整組轉向；狗離貓不到 6 m 時第 1 步先退到 6 m（特寫才不會穿模），第 5 步推到 `ArenaData.transitionPushRadius`（10 m）。轉階段不能跳過，期間不另外設玩家無敵。
- 第二階段的魔法陣是紅色、鋪滿整個場地（Hold 在轉階段最後的值），地面的紅圈、衝刺路徑疊在上面；看不清楚的話 T10 調 `MagicCircle` 的亮度或 phase2Color。第二階段外觀（電光、殘影）讀 `BossPhaseData.electrified`。
- 失衡值在招式之間也會衰減：只靠盾反時，兩次貓掌連拍之間是 破綻 1.2 + 出招間隔 0.8–1.5 + 預兆 0.5 = 2.5–3.2 秒，間隔抽到超過約 1.3 秒，第 4 次盾反會停在 99（中間砍一刀 +3 就補回）。T10 平衡時再看要不要調整衰減延遲。
- `BossPhaseData.contactDamage`（接觸傷害）只有數值，規則（可否格擋、判定間隔、哪些狀態有效）待企劃書確定後再實作。
- 戰鬥 UI（T9）由 BattleUIBuilder（Tools > UI > Build Battle UI）建立 BattleUI Canvas 與 EventSystem（InputSystemUIInputModule 用內建的 DefaultInputActions，不動 Player map），並接好 BattleManager 的 T9 欄位；Build Intro 會重建 BattleManager，最後也會呼叫它。版面避開左上、右上的 Debug 面板；字型是內建 LegacyRuntime（uGUI Text），之後換 TextMeshPro 時一起換。勝敗畫面出現時 BattleManager 鎖住玩家與鏡頭輸入、`CameraController.SetCursorReleased(true)` 放開游標。Boss 倒地是 OiiaCatPoser.Fall 的佔位動作（往遠離最後一擊的方向倒 80°）。玩家倒下後 Boss 不停手、音樂照播，當作嘲諷（使用者決定；打到倒下的玩家是 Ignored，Hazard 照常放）。轉階段演出期間按 F7 讓玩家倒下的話，演出結束時會解除輸入鎖定（只有 Debug 會發生）。
- 平衡面板（T10）：`DebugTools` 物件上的 BalanceDebugPanel（BalancePanelBuilder 接線；BattleUIBuilder、BossBuilder 結束時也會重接，選單 Tools > Debug > Wire Balance Panel）。Awake 時把 Boss、玩家、鏡頭底下元件引用的 BossStats／BossPhaseData／BossMoveData／PlayerStats 換成 `Instantiate` 的複本（含階段、開場序列、進場招式的引用），面板只改複本，所以不違反「執行期不寫 SO」；「存回 .asset」只在 Editor、按兩次才用 SerializedObject 寫回面板上那幾個欄位。開著時預設暫停（timeScale 0，LateUpdate 壓住 HitStop 的還原）、鎖玩家與鏡頭輸入、放開游標。改 Boss 血量上限時門檻照資產比例縮放、目前血量維持比例。新增可調欄位：在 `BuildKnobs` 加一行 `AddKnob`（只支援 float）。試玩流程與回報格式在 `docs/playtest-checklist.md`。
- 打擊回饋（T11）：玩家的 PlayerFeedback、Boss 的 BossImpactFeedback 只訂閱同物件的事件，經同物件的 FeedbackEmitter 播 `CombatFeedbackData` 的 cue（音效經 AudioManager、震動用 CinemachineImpulseSource；自由視角與鎖定兩台鏡頭有 ImpulseListener，演出鏡頭沒有）。招式的衝擊時刻由招式呼叫 `BossMove.Impact(kind, position)`（PounceRunner 直接 `NotifyImpact`），新招要有聲音與震動時記得加。使用者決定不加新的畫面停頓（只有盾反 0.1 秒）；同一幀同一組 cue 只播一次（一波落雷同時劈下不疊加）；翻滾閃過的音效一次翻滾只響一次。CombatFeedbackBuilder（Tools > Feedback > Build Combat Feedback）合成音效、建資產並接線；CameraRigBuilder、BossBuilder、IntroCutsceneBuilder 結尾會重接。資產已存在時數值不動，音效只填空的或佔位的欄位。震動量測（相機離玩家約 4.6 m）：被打中約 0.15 m、處決約 0.18 m、大迴旋落地（9 m 外）約 0.5 m；太晃就調 `shakeScale`（0 = 關）。所有音效都是 2D、程序合成的佔位。
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
| T11 | 打擊回饋（音效、鏡頭震動、預兆音效） | 揮刀、命中（輕、第三段、重擊、處決分級）、格擋、破防、盾反、受擊、翻滾、跳躍、啃骨頭有音效；白光預兆「叮」、紅光預兆嗡嗡聲；Boss 撞牆、落地、衝擊波、落雷、龍捲風、大迴旋落地、頭暈、倒下有音效與鏡頭震動；盾反停頓與勝利慢動作照舊 |


## Skill 使用規則
- 打擊感、受擊回饋、畫面震動：參考 game-feel
- Boss AI、狀態機：參考 game-ai
- 鏡頭：參考 camera-systems
- 光效、後處理：參考 urp-postprocessing
- skill 建議與 docs/boss-oiia.md 衝突時，以企劃書為準