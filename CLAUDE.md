# OIIA 貓 Boss 戰（Unity 原型）

## 專案目標

- 一場可獨立遊玩的 Boss 戰：玩家操作刀盾狗，在半徑 12 m 的圓形競技場挑戰兩階段的 OIIA 貓，目標通關時間 4–5 分鐘。
- 用途是先驗證戰鬥系統、Boss AI 框架與過場演出流程，之後接回關卡挑戰制的主遊戲；所以系統之間要好拆。
- T17 起接上主遊戲的外圍：休息區（Hub）、存檔、角色切換、數值管線、裝備、技能樹、任務，規格在 `docs/hub-progression.md`。
- **規格以 `docs/boss-oiia.md`（Boss 戰）與 `docs/hub-progression.md`（休息區與成長系統）為準。** 數值、招式、分鏡都照企劃書做；發現企劃書與程式衝突，或企劃書沒寫清楚時，先問，不要自行改規格。

## 環境

- Unity 6000.3.25f1、URP 17.3（`Assets/Settings/PC_RPAsset`）
- Input System 1.20：使用 Project-wide Actions `Assets/InputSystem_Actions.inputactions` 的 `Player` map，不用舊版 Input Manager
- Cinemachine 3.1：namespace 是 `Unity.Cinemachine`，元件是 `CinemachineCamera`（不是 2.x 的 `CinemachineVirtualCamera`）
- Timeline 1.8
- 場景：`Assets/Scenes/Hub.unity`（休息區，Build Settings 第 0 個，啟動遊戲先進這裡）、`Assets/Scenes/BossArena.unity`（Boss 戰，第 1 個）；兩邊都靠 Progression 的 SceneFlow 換場景
- 單位：距離為公尺、時間為秒、角度為度。俯視角度 0 = 北（+Z），順時針遞增；玩家從南側（180°）進場，Boss 在場地中心。

## 資料夾

| 路徑 | 用途 |
| --- | --- |
| `Assets/Scripts/Battle` | 最上層的流程指揮：BattleManager（戰鬥流程狀態機 Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk → Victory／Defeat），指揮 Boss、玩家與演出（播轉階段演出、結束時呼叫 `BossController.CompleteTransition`、換階段改音樂速度；勝利時慢動作與音樂淡出、送 `OnBattleResult`、`Retry` 重新載入場景；ESC 選單暫停時鎖住玩家與鏡頭、放開游標；Awake 時把難度修正交給玩家與 Boss；ESC 選單秘技的解鎖記在 BattleSession）；BattleSettings（勝敗節奏） |
| `Assets/Scripts/Core` | 跨系統共用（最底層，不引用其他系統）：BattleSession（這次執行的挑戰次數、「看過開場」「看過轉階段」與「秘技解鎖了開發者模式開關」）、事件、傷害系統（DamageInfo、Hitbox、Hurtbox、IDamageable）、畫面停頓（HitStop）、AudioManager（戰鬥音樂與變速、音效）、競技場（ArenaData、ArenaGenerator、天色 ArenaAtmosphere／AtmosphereData）、LockOnTarget、可處決目標（ExecutionTarget）、打擊回饋的資料與播放（CombatFeedbackData、FeedbackEmitter：音效＋Cinemachine Impulse 鏡頭震動；Core 只有這裡用到 Unity.Cinemachine）、玩家設定（GameOptions／GameOptionsData：音量、開發者模式、改鍵、難度，存 PlayerPrefs）、難度（Difficulty、DifficultyData／DifficultyModifiers：簡單模式的修正值）、暫停（GamePause：timeScale 0、音效暫停） |
| `Assets/Scripts/Player` | PlayerController、PlayerAttack、PlayerHealth、PlayerStamina、PlayerStats 定義、DaoDunDogAnimator、角色 prefab 根物件的 PlayerRig（公開玩家元件、鏡頭跟隨點、臉部錨點）、接收玩家的介面 IPlayerBinding、打擊回饋（PlayerFeedback：揮刀、命中、格擋、盾反、受擊等的音效與震動） |
| `Assets/Scripts/Progression` | 跨場景的進度與流程：SceneFlow（換場景、黑幕淡出淡入）、SceneFlowData、PlayerSpawner（生成玩家角色、依這次的出生點擺位置、交給 IPlayerBinding）；角色與數值管線（T19）：CharacterDefinition、CharacterRoster、GameDatabase（id 換回資產）、StatModifier／PlayerStatId、PlayerEffect／PlayerEffectBehaviour、PlayerLoadout；之後的 EquipmentData、EquipmentSlotType、SkillTreeData、QuestData、BossDefinition、RewardData、ProgressRecorder 也放這裡 |
| `Assets/Scripts/Hub` | 休息區：傳送門 Portal（T17 是站在觸發區按 Interact 的暫時版，T20 換成 Interactable）；之後的 HubManager、Interactable、InteractionScanner、Campfire、Blacksmith、VillageChief、HubLayoutData |
| `Assets/Scripts/Camera` | CameraController（Cinemachine 第三人稱與鎖定鏡頭；鎖定哪個目標由 PlayerController 決定，鏡頭訂閱 `OnLockTargetChanged`） |
| `Assets/Scripts/Boss` | BossController 狀態機與選招、BossHealth、失衡值／頭暈／處決（BossStagger）、BossMove 基底類別、BossStats／BossMoveData／BossPhaseData 定義、預兆閃光（BossTelegraphFlash）、佔位模型的程序化姿勢（OiiaCatPoser）、命中火花（BossHitFeedback，Boss 打到玩家）、受擊反饋（BossDamageFeedback，玩家打到 Boss：火花、閃白、後仰、傷害數字）、頭暈星星（BossStunVisual）、強化階段外觀（BossPhaseVisual：紅色電光與殘影）、幻影分身的幻影（BossPhantom，一擊即碎）、招式衝擊與預兆的音效與震動（BossImpactFeedback，訂閱 `OnMoveTelegraph`／`OnMoveImpact`） |
| `Assets/Scripts/Boss/Moves` | 每招一個 BossMove 子類別；招式特有的數值寫成 BossMoveData 的子類別放在同一個資料夾；PounceRunner 是一次飛撲（扭屁股飛撲與幻影分身共用） |
| `Assets/Scripts/Hazards` | 場上判定物件（Hazard 基底、衝擊波 ShockwaveRing、落雷 LightningStrike、龍捲風 TornadoVortex、大迴旋的全場判定 GrandSpinField 與安全區 SafeZone）、地面範圍提示（GroundTelegraph，URP Decal）、場地中央的魔法陣（MagicCircle，URP Decal，出現進度／脈動／顏色／半徑、幻影分身的閃爍、大迴旋安全區的符文熄滅）、一次性特效（TimedFx、傷害數字 DamageNumber）、佔位電光（ElectricArcs，Boss 身上與轉階段的場地邊緣共用） |
| `Assets/Scripts/Cutscene` | CutsceneDirector（播一段演出的 Timeline、鎖輸入、長按跳過、字卡事件；開場與轉階段各一個物件）、CutsceneStage（轉階段時依玩家位置擺演出鏡頭）、IntroCutsceneSettings、Timeline 自訂 Track／Clip（CurveClip 加 CurveTrack／StormTrack／BossSpinTrack／MagicCircleTrack／ElectricArcsTrack、ActorWalk、ActorPush、BossActor、CameraMove、MusicCue、CutsceneEventMarker）、演出物件（ScreenFlash、IntroLightning、SmokePuffs、CameraShaker、ExpandingRing） |
| `Assets/Scripts/UI` | 只訂閱事件（uGUI）：BattleUI（HUD 何時出現、Boss 血條滑入）、PlayerHudUI（左下血條、耐力、骨頭）、BossHudUI（下方置中 Boss 名稱、血條與刻度、失衡條、頭暈倒數）、LockOnMarkerUI、處決標記（ExecutionMarkerUI）、BattleResultUI（勝敗畫面與重試按鈕；勝利、戰敗各一組文字）、ResourceBar（量條與延遲扣血，置中模式給頭暈倒數）、BattleUIData；開場的字卡（TitleCardUI）、跳過提示（SkipPromptUI，開場與轉階段共用）；ESC 選單（PauseMenuUI：含難度頁與解鎖開發者模式的秘技；改鍵頁 KeyBindingPageUI／KeyBindingRowUI、PauseMenuData）、按鍵顯示名稱（InputNameData、BindingText）、按鍵提示顯示鍵鼠還是手把（InputHintDevice，放在 GameOptions 物件上） |
| `Assets/Scripts/Debug` | Debug 快捷鍵 F1–F7 與右上角的 Boss 面板（BossDebugTools，F5 重新挑戰＝`BattleManager.Retry`、F6 Boss 血量歸零、F7 玩家血量歸零）、平衡面板（BalanceDebugPanel，F12：執行期覆寫 SO 數值、計時與統計，見下方「平衡面板」）、左上角的玩家狀態（PlayerDebugOverlay）、開發者模式開關（DeveloperMode：開關上面三個元件，ESC 選單開著時也先停用）、測試木樁（TrainingDummy）、F12「玩家」頁開關的測試修正（TestModifierData） |
| `Assets/Scripts/Editor` | 編輯器工具（ArenaBuilder、DaoDunDogBuilder、CameraRigBuilder、TrainingDummyBuilder、BossBuilder、HazardBuilder、IntroCutsceneBuilder、TransitionCutsceneBuilder、BattleUIBuilder、PlaceholderAudioBuilder、BalancePanelBuilder、CombatFeedbackBuilder、PauseMenuBuilder、SceneFlowBuilder、HubBuilder、PlayerSpawnerBuilder、ProgressionBuilder、GameDatabaseValidator），不會進 build |
| `Assets/ScriptableObjects` | 數值資產（`.asset`），依系統分子資料夾：`Arena/`（ArenaData、Atmosphere 天色）、`Boss/`（BossStats）、`Boss/Phases/`、`Boss/Moves/Phase1/`（第一階段 5 招）、`Boss/Moves/Phase2/`（第二階段 4 個強化招，和第一階段同一招共用類別；新招龍捲風、幻影分身與終極技大迴旋）、`Cutscene/`（IntroCutsceneSettings）、`Battle/`（BattleSettings）、`UI/`（BattleUIData：血條刻度、動畫時間、顏色、文字；PauseMenuData：ESC 選單的文字、顏色、改鍵清單；InputNameData：按鍵顯示名稱）、`Options/`（GameOptionsData：玩家設定的預設值；Difficulty：各難度的修正值）、`Feedback/`（CombatFeedback：打擊回饋的音效、音量、鏡頭震動）、`Debug/`（測試木樁、T19 的測試修正 TestModifiers）、`Progression/`（SceneFlowData、GameDatabase、CharacterRoster；`Characters/` 是各角色的 CharacterDefinition 與基礎數值，刀盾狗的是 `DaoDunDog.asset`、`DaoDunDogStats.asset`（T18 以前的 `Player/PlayerStats.asset`，GUID 不變）；之後的 `Equipment/`、`SkillTrees/`、`Quests/`、`Bosses/`）、`Hub/`（T20 起：HubLayoutData、休息區天色、HubUIData） |
| `Assets/Timelines` | Timeline 資產：`Intro.playable`（開場 14.5 秒）、`Transition.playable`（轉階段 5.0 秒）；鏡頭與各效果的時間點都在這裡調 |
| `Assets/Prefabs` | 玩家角色（`Prefabs/Characters/DaoDunDog.prefab`，DaoDunDogBuilder 產生）、Boss（`OiiaCat`、幻影 `OiiaCatPhantom`，BossBuilder 產生）、UI 的 prefab；`Prefabs/Hazards` 是地面提示、Hazard 與特效，由 HazardBuilder 產生，重建會覆寫 |
| `Assets/Shaders` | 自製 shader（都是手寫 JSON 的 URP Decal Shader Graph，Custom Function 用 UV 畫圖）：`TelegraphDecal.shadergraph`（地面範圍提示：圓、環、長方形）、`MagicCircleDecal.shadergraph`（魔法陣：雙環、符文帶、倒五芒星，由中心描繪、外圈分段點亮；`_Holes` 加上 Size.y、Params.w 是大迴旋安全區的熄滅區） |
| `Assets/Scenes` | `Hub.unity`（休息區，HubBuilder 產生）、`BossArena.unity`（`SampleScene` 是範本殘留，不在 Build Settings） |
| `Assets/Audio` | BGM 與音效；OIIA 音樂放 `Assets/Audio/oiia_bgm`，放進來後在場景 `AudioManager` 的 battleMusic 指定（重跑 IntroCutsceneBuilder 也會自動找路徑含 oiia_bgm 的音檔）；`Audio/Placeholder` 是 PlaceholderAudioBuilder 合成的佔位音效（戰鬥音效是 `Sfx_Combat_*`），重建會覆寫 |
| `Assets/Models` | 模型、網格、材質；`Models/Arena`、`Models/DaoDunDog`、`Models/TrainingDummy`、`Models/OiiaCat`、`Models/Hazards`、`Models/Cutscene` 由 Builder 自動產生，重建會覆寫網格；`Models/UI` 是 BattleUIBuilder 產生的 UI 圖（處決標記的圓環），只在不存在時產生 |
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
- 難度（T15）不改也不複製 SO：`DifficultyData`（`ScriptableObjects/Options/Difficulty.asset`）的修正值由 BattleManager 在 Awake 交給 `PlayerController.SetDifficulty`、`BossController.SetDifficulty`，各元件讀數值時才加上（`Difficulty` 屬性）。新增修正項目時在 `DifficultyModifiers` 加欄位（預設值 = 不修正），在讀那個數值的地方加上去。

### 系統之間用 C# event 溝通

- 發送方宣告 `public event Action<...> OnXxx;`，命名用 `On` + 發生的事：`OnBossHealthChanged`、`OnPhaseChanged`、`OnParry`。
- 訂閱方在 `OnEnable` 訂閱、`OnDisable` 退訂。
- UI 與演出只訂閱事件，不直接讀其他元件的欄位。
- 引用用 `[SerializeField]` 在 Inspector 接好；同一個 GameObject 上的元件可以在 `Awake` 用 `GetComponent` 取。
- **執行期程式禁用** `FindObjectOfType`、`FindAnyObjectByType`、`FindFirstObjectByType`、`FindObjectsByType`、`GameObject.Find`、`FindWithTag`，也不用 static singleton 互相存取。`Scripts/Editor` 底下的編輯器工具不受此限。
- 依賴方向只能往下，不能互相引用（之後接回主遊戲、加 asmdef 才拆得開）：
  Core（最底層）← Hazards ← Player ← Camera ← Boss ← Cutscene ← Progression ← Battle／Hub ← UI／Debug。Boss 不引用 Player、Player 不引用 Camera、Core 不引用任何系統；Battle 與 Hub 互不引用，兩邊都只靠 Progression 的 SceneFlow 換場景。休息區的介面放 `BossFight.UI`，只訂閱 Hub 與 Progression 的事件。
  需要反方向的資訊時，由下層發事件或把結果放進回傳值（例如盾反失衡值走 `DamageOutcome`／`HitReport`）。新增 `using BossFight.*` 前先確認方向。
- 第一個 static 例外是 `Core/BattleSession`（第幾次挑戰、看過開場沒有、看過轉階段沒有、ESC 選單的秘技解鎖了沒有）：重新載入場景後元件都是新的，跨場景的資料要放在活得比場景久的地方。它只放這四個值、不放元件引用，進 Play 模式時清空；只有 BattleManager 讀寫，其他系統透過 BattleManager 的事件或參數得知（選單呼叫 `BattleManager.UnlockDeveloperOptions`、訂閱 `OnDeveloperOptionsUnlocked`）。
- 第二個 static 例外是 `Core/ProfileStore`（存檔）：只放純資料，存的是 id 字串，不放 SO 或元件引用（之後 Progression 用 GameDatabase 把 id 換回資產）。存在 `Application.persistentDataPath/save.json`（JsonUtility），有變動就存、離開遊戲再存一次；讀檔失敗時把壞檔改名成 `save.bak.json`、開新檔並在 Console 警告。第一次讀到時才載入，進 Play 模式時清掉載入狀態（Domain Reload 關閉時也會重新讀檔）。「這次要在哪個出生點出現」（`SetArrival`／`PeekArrival`／`TakeArrival`）只活在這次執行、不寫進檔案：新場景的 PlayerSpawner 先看（決定生成位置），SceneFlow 再取走（決定要不要淡入）。玩家設定照舊存 PlayerPrefs（GameOptions），兩者分開。
  Debug 另有一個例外：`Debug/BalanceDebugPanel` 用 static 欄位保留 SO 複本與每場紀錄，讓 F12 的調整值在重試後還在；整個檔案包在 Debug 程式的編譯條件裡（見下方「其他」），進 Play 模式時清空、離開時銷毀複本。遊戲程式不能讀它。

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

- 玩家（T18 起）由 `Progression/PlayerSpawner` 生成，場景裡不放玩家物件：
  - 要用玩家的元件實作 `IPlayerBinding`（`BindPlayer(PlayerRig)`），不再用 `[SerializeField]` 接玩家。spawner 是 `[DefaultExecutionOrder(-1000)]`，在 Awake 生成角色後對接收者清單 `BindPlayer`，所以第一次呼叫時接收者的 Awake／OnEnable 都還沒跑：這裡只記引用（要用同物件的元件就自己 `GetComponent`），照原本的規則在 OnEnable 訂閱。
  - `BindPlayer` 要能處理換人（`PlayerSpawner.ReplaceCharacter`，T21 營火用）：用自己的 `subscribed` 旗標（OnEnable 設 true、OnDisable 設 false）判斷，已訂閱就先退訂舊角色、換引用、再訂閱新角色。不要用 `isActiveAndEnabled` 判斷（第一次呼叫時還沒 OnEnable）。
  - 接收者清單、Main Camera、LockOnTarget、ExecutionTarget、AudioManager、BossController 都由 `PlayerSpawnerBuilder.Wire` 自動收集（Tools > Player > Wire Player Spawner；會建立或重建這些物件的 Builder 結束時都會跑）。新增接收者後重跑一次 Wire 即可。
  - 角色身上原本引用場景物件的欄位（PlayerController 的 `cameraTransform`、`lockTargets`、`executionTargets`，FeedbackEmitter 的 `audioManager`）由 spawner 生成後經 `PlayerRig.SetSceneReferences` 填入；Boss 的目標用 `BossController.SetTarget`（Boss 不引用 Player）；鏡頭的 Follow／鎖定群組由 `CameraController.BindPlayer` 換成生成的角色（第一次 Bind 時把自由視角轉到角色背後，換角色時保留視角）。
  - spawner 底下有一隻 EditorOnly 的預覽模型（`PreviewCharacter`，角色 prefab 的實例）：編輯模式下 Timeline 軌道、演出鏡頭的狗臉位置、Cinemachine 的 Follow 都綁它（Builder 用 `PlayerSpawnerBuilder.Preview()` 拿）；進 Play 時 spawner 先把它關掉，`CutsceneDirector.BindPlayer` 把綁定型別是 PlayerController 的軌道（ActorWalk、ActorPush）用 `SetGenericBinding` 改綁生成的角色。正式 build 不會有預覽模型。
  - 出生點：換場景進來時（`ProfileStore.PeekArrival`）依 id 找 spawner 的 `spawnPoints`，否則生成在 spawner 自己的位置與朝向（BossArena 在 PlayerSpawn、休息區在 `Start`）。
- 角色、裝備、技能的數值（T19 起）只能透過 `StatModifier` 改，不在玩家元件裡加特例。數值管線（`Progression/PlayerLoadout`，PlayerSpawner 一份）：
  - 生成時：`CharacterDefinition.baseStats` → `Instantiate` 複本（`HideFlags.DontSave`）→ 所有加值 → 所有倍率（`StatModifierOp.Multiply` 的 value 是增加的比例，0.2 = ×1.2，同一項相加後一次乘）→ 夾回範圍（上限最少 1、格擋減傷 0..1、其他最少 0、骨頭上限四捨五入）→ `PlayerRig.ApplyStats`（換進 PlayerController／Health／Stamina／Attack，送 `OnStatsApplied`）→ 掛效果。舊的複本與效果元件在重新套用時銷毀。
  - 修正依來源登記：`spawner.Loadout.SetSource(key, modifiers, effects)`／`ClearSource(key)`，再 `spawner.ReapplyStats(refill)`（預設補滿血量、耐力與骨頭；F12 調值時不補滿、只夾到上限內）。來源在換角色時保留，由登記的一方自己換。
  - 新增可改的數值：`PlayerStatId` 往後加一項（資產存數字，不要插在中間）、`PlayerLoadout.Modify` 的 switch 加一行。「耐力回復」只對應 `staminaRegen`，舉盾加速回復不受影響。
  - 角色以外要讀玩家數值的元件（CameraController 的視角靈敏度）每次從 `PlayerRig.Stats` 讀，不要自己留引用（重新套用後舊複本會被銷毀）。
  - 效果：繼承 `PlayerEffect<TBehaviour>`（SO 只放參數），`TBehaviour : PlayerEffectBehaviour` 掛在角色根物件上、Awake 拿到 `Rig`、OnEnable 訂閱玩家事件；第一個實際效果（盾反回血）在 T22。
  - 資產由 ProgressionBuilder（Tools > Progression > Build Game Database）建立；GameDatabase 的 id 檢查在 Tools > Progression > Validate Game Database，打包前也會自動檢查（空 id、同類重複、角色沒接 prefab 或 baseStats 都會停止打包）。新增種類時在 `GameDatabase.Entries` 加。
- 輸入只用新版 Input System，從 `InputSystem.actions` 讀 `Player` map；新動作加進 `InputSystem_Actions.inputactions`。`Player/SkipCutscene`（Space、手把 A）是長按跳過開場與轉階段用的，和 Jump 同鍵；演出期間 PlayerController／CameraController 被 `SetInputLocked` 鎖住，不會跳起來。
- `Player/Pause`（Esc、手把 Start）開關 ESC 選單，不在改鍵清單裡、也不能被綁到別的動作；選單裡的返回另外讀 `UI/Cancel`（Esc、手把 B）。CameraController 不再處理 Esc（以前 Esc 放開游標）。
- 改鍵是 `InputSystem.actions` 的 binding override，由 GameOptions 存成 PlayerPrefs 的 JSON、Awake 時套回去；遊戲程式照常 `FindAction` 就會拿到改過的鍵。鍵鼠頁的「次要」欄是 Keyboard&Mouse 控制組裡的第 2 個綁定，所以只有一個鍵鼠綁定的動作在 `.inputactions` 裡補了空綁定（path 空字串）；改鍵清單（PauseMenuData.bindings）加新動作時也要補。畫面上的按鍵提示用 `BindingText.FirstDisplay` 讀目前的鍵，不要寫死鍵名；控制組用 `BindingText.Group(gamepad)`，gamepad 來自 `InputHintDevice.OnDeviceChanged`（只顯示目前裝置的鍵，不要同時列鍵鼠與手把）。
- 演出（開場、之後的轉階段）由 CutsceneDirector 鎖輸入：`PlayerController.SetInputLocked`（不讀按鍵、不能切換鎖定，只照 `ScriptedMoveTo` 走）、`CameraController.SetInputLocked`（不轉視角），結束前先 `PlayerController.ClearLockTarget` 再用 `CameraController.ResetBehind` 把自由視角擺到玩家背後。
- 鎖定（中鍵）由 PlayerController 負責：鎖定清單 `lockTargets` 由 PlayerSpawner 生成時交給玩家（PlayerSpawnerBuilder.Wire 收集場景裡所有的 LockOnTarget），結果用 `OnLockTargetChanged` 送出，鏡頭與 Debug 面板訂閱。
- Timeline 自訂片段要算進度時用片段本身的長度（Track 在 `CreatePlayable` 填進 behaviour 的 `clipDuration`），不要用 `playable.GetDuration()`：片段設成 Hold 延伸時，playable 的長度會包含延伸的部分（約 100 萬秒）。
- Debug 程式整個檔案包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS`。`BOSSFIGHT_DEV_TOOLS` 是 Player Settings > Standalone 的 Scripting Define Symbols（T15 起加上：alpha 測試版要能用秘技打開開發者模式）；正式版要拿掉 Debug 程式時，從 define 刪掉它即可。Debug 程式裡用到 UnityEditor 的部分另外包 `#if UNITY_EDITOR`（player 編譯沒有 UNITY_EDITOR）。
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
- T0 的比例參考膠囊 `ScaleRef_2m`（EditorOnly，紅色，在 `Spawns` 底下）預設關閉，要對照尺寸時在 Hierarchy 打開（打開時編輯器 Play 會出現在開場的狗身邊）；左上、右上的 Debug 面板（IMGUI）會蓋在開場與字卡上面。正式 build 都不會有。
- 轉階段（T7）：Boss 血量過門檻時自己進入 Transition（無敵、打斷招式、清 Hazard）；BattleManager 播 TransitionCutscene，演出結束時呼叫 `BossController.CompleteTransition` 套用下一階段。有接演出時 BattleManager 會關掉 `BossController.AutoCompleteTransition`；沒接演出時才照 `BossPhaseData.transitionTime` 計時自動結束。演出由 TransitionCutsceneBuilder（Tools > Cutscene > Build Transition／Reset Transition Timeline）建立，鏡頭在執行時由 CutsceneStage 依玩家位置整組轉向；狗離貓不到 6 m 時第 1 步先退到 6 m（特寫才不會穿模），第 5 步推到 `ArenaData.transitionPushRadius`（10 m）。期間不另外設玩家無敵。T14 起轉階段也能跳過：同一次執行看過一次（`BattleSession.TransitionSeen`，BattleManager 在演出結束時記）之後，`CutsceneDirector.Play(true)`；transition 也接 IntroCutsceneSettings（只用 skipHoldTime）。轉階段 Timeline 沒有 SkipTarget 標記，跳過時 CutsceneDirector 在結尾前 0.001 秒 Evaluate 一次再 Pause，下一幀的 LateUpdate 才 Stop（玩家要在輸入還鎖著時照 ScriptedMoveTo 走到被推開的位置）；所以音樂「Restart」與狗的「Pushed Out」兩個片段設成 Hold（TransitionCutsceneBuilder 對已存在的 Timeline 也會補設），其他效果本來就是 Hold 或在結尾歸零。跳過會一幀推開 2 m 以上，所以 `PlayerController.SetInputLocked(false)` 在照腳本走過時會清掉移動速度（不然解除後會滑到牆邊）。
- 第二階段的魔法陣是紅色、鋪滿整個場地（Hold 在轉階段最後的值），地面的紅圈、衝刺路徑疊在上面；看不清楚的話 T10 調 `MagicCircle` 的亮度或 phase2Color。第二階段外觀（電光、殘影）讀 `BossPhaseData.electrified`。
- 失衡值在招式之間也會衰減：只靠盾反時，兩次貓掌連拍之間是 破綻 1.2 + 出招間隔 0.8–1.5 + 預兆 0.5 = 2.5–3.2 秒，間隔抽到超過約 1.3 秒，第 4 次盾反會停在 99（中間砍一刀 +3 就補回）。T10 平衡時再看要不要調整衰減延遲。
- `BossPhaseData.contactDamage`（接觸傷害）只有數值，規則（可否格擋、判定間隔、哪些狀態有效）待企劃書確定後再實作。
- 戰鬥 UI（T9）由 BattleUIBuilder（Tools > UI > Build Battle UI）建立 BattleUI Canvas 與 EventSystem（InputSystemUIInputModule 用內建的 DefaultInputActions，不動 Player map），並接好 BattleManager 的 T9 欄位；Build Intro 會重建 BattleManager，最後也會呼叫它。版面避開左上、右上的 Debug 面板；字型是內建 LegacyRuntime（uGUI Text），之後換 TextMeshPro 時一起換。勝敗畫面出現時 BattleManager 鎖住玩家與鏡頭輸入、`CameraController.SetCursorReleased(true)` 放開游標。Boss 倒地是 OiiaCatPoser.Fall 的佔位動作（往遠離最後一擊的方向倒 80°）。玩家倒下後 Boss 不停手、音樂照播，當作嘲諷（使用者決定；打到倒下的玩家是 Ignored，Hazard 照常放）。轉階段演出期間按 F7 讓玩家倒下的話，演出結束時會解除輸入鎖定（只有 Debug 會發生）。
- 平衡面板（T10）：`DebugTools` 物件上的 BalanceDebugPanel（BalancePanelBuilder 接線；BattleUIBuilder、BossBuilder 結束時也會重接，選單 Tools > Debug > Wire Balance Panel）。它是 `[DefaultExecutionOrder(-999)]`：PlayerSpawner（-1000）生成玩家之後、其他元件之前，Awake 時把 Boss、鏡頭底下元件引用的 BossStats／BossPhaseData／BossMoveData 換成 `Instantiate` 的複本（含階段、開場序列、進場招式的引用），面板只改複本，所以不違反「執行期不寫 SO」；「存回 .asset」只在 Editor、按兩次才用 SerializedObject 寫回面板上那幾個欄位。玩家（T19 起，使用者決定的做法）：面板不換角色身上的 PlayerStats（那是數值管線每次重建的複本），而是把 `PlayerLoadout.BaseStatsSource` 設成自己的基礎值複本再 `ReapplyStats`：面板調的是基礎值（跟難度一樣，修正疊在上面），改了就 `ReapplyStats(false)`（不補滿血量）；有修正時「玩家」頁上方標示提醒，欄位旁顯示「實際」值；存回 .asset 只寫基礎值，所以有裝備時也能用。「玩家」頁最上面是 T19 的測試修正開關（`TestModifiers.asset`：血量 +20、跑速 ×1.2，開關會補滿血量與耐力；跟調整值一樣重試後保留）。開著時預設暫停（timeScale 0，LateUpdate 壓住 HitStop 的還原）、鎖玩家與鏡頭輸入、放開游標。改 Boss 血量上限時門檻照資產比例縮放、目前血量維持比例。新增可調欄位：在 `BuildKnobs` 加一行 `AddKnob`（只支援 float）。試玩流程與回報格式在 `docs/playtest-checklist.md`。
- 打擊回饋（T11）：玩家的 PlayerFeedback、Boss 的 BossImpactFeedback 只訂閱同物件的事件，經同物件的 FeedbackEmitter 播 `CombatFeedbackData` 的 cue（音效經 AudioManager、震動用 CinemachineImpulseSource；自由視角與鎖定兩台鏡頭有 ImpulseListener，演出鏡頭沒有）。招式的衝擊時刻由招式呼叫 `BossMove.Impact(kind, position)`（PounceRunner 直接 `NotifyImpact`），新招要有聲音與震動時記得加。使用者決定不加新的畫面停頓（只有盾反 0.1 秒）；同一幀同一組 cue 只播一次（一波落雷同時劈下不疊加）；翻滾閃過的音效一次翻滾只響一次。CombatFeedbackBuilder（Tools > Feedback > Build Combat Feedback）合成音效、建資產並接線；CameraRigBuilder、BossBuilder、IntroCutsceneBuilder 結尾會重接。資產已存在時數值不動，音效只填空的或佔位的欄位。震動量測（相機離玩家約 4.6 m）：被打中約 0.15 m、處決約 0.18 m、大迴旋落地（9 m 外）約 0.5 m；太晃就調 `shakeScale`（0 = 關）。所有音效都是 2D、程序合成的佔位。
- 刀盾狗耐力與骨頭（T12，使用者指定）：翻滾要有一次的消耗量（25）才能滾（原本「耐力 > 0 就能滾」加上翻滾中回耐力，可以無限連滾）；重擊仍是「> 0 就能劈」。骨頭由 PlayerHealth 累積（訂閱同物件 PlayerAttack.OnAttackHit，結果是 Hit 才算，打碎幻影也算），進度用 `OnBoneProgressChanged` 送給 HUD。總回血量比原本每場 3 根多很多，T10 平衡時留意。
- ESC 選單（T13，使用者指定的項目：設定〔按鍵設定（鍵鼠、控制器）、音量設定、開發者模式〕、重新挑戰、退出遊戲；「繼續」與確認框是計畫的預設）：
  - PauseMenuBuilder（Tools > UI > Build Pause Menu）建 PauseMenu Canvas（sortingOrder 200）、GameOptions 物件（GameOptions＋GamePause）與資產，並接線（Wire：AudioManager、BattleManager、DeveloperMode、Boss 血條與跳過提示的按鍵名稱；Tools > UI > Wire Pause Menu 可單獨跑）。BattleUIBuilder 結束時會呼叫 Build，TransitionCutsceneBuilder、BossBuilder 結束時呼叫 Wire。頁面上的固定文字、按鈕顏色、改鍵頁大小是建立時寫進場景的，改 PauseMenuData 後要重跑；執行時的文字（開發者模式開關、改鍵提示、確認訊息、百分比）直接讀資產。
  - 暫停：GamePause 把 timeScale 設 0（LateUpdate 壓住）、`AudioListener.pause`；音樂與選單試聽的 AudioSource 設成 ignoreListenerPause，調音量時聽得到。BattleManager 在 LateUpdate 看 `GamePause.IsPaused` 鎖／解鎖玩家與鏡頭（解除那一幀玩家的 Update 已經跑完，手把 B／A 關選單不會翻滾或跳起來）；演出中交給演出解鎖，勝敗畫面已出現就維持鎖定。開場、轉階段 Timeline 用遊戲時間，暫停時停住，長按跳過也不累積。
  - 音量沒用 AudioMixer：主音量是 `AudioListener.volume`，音樂乘在 AudioManager.musicVolume 上，音效乘在 Sfx／Ui 與演出 Timeline 音軌的 AudioSource 原本的音量上（線性，不是 dB）。之後要分更多類或加效果再換 AudioMixer。
  - 改鍵：點一格後等按鍵放開才開始聽（點擊用的滑鼠左鍵、Enter、手把 A 不會被當成新鍵）；Esc／Start 取消、鍵鼠次要欄 Delete 清空。同一頁同鍵時互換；次要欄原本空著時，對方是次要鍵就移過來、是主要鍵就不換（主要欄不能空）；長按跳過開場不檢查。手把按鍵名稱用 Xbox 的（InputNameData）。
  - 開發者模式：DeveloperMode 依 GameOptions 開關 BossDebugTools、BalanceDebugPanel、PlayerDebugOverlay（關掉時順便清掉 F2 無敵與 F3 指定招；F12 已改的數值留著）；選單開著時也先停用（IMGUI 面板會蓋在選單上，開著的 F12 面板會被關掉）。沒有 Debug 程式的 build（沒有 `BOSSFIGHT_DEV_TOOLS` 的正式 build）選單不顯示這一項。T15 起預設是關，而且開關本身也藏起來（見下方 T15）。
  - 在背景驗收時：Unity 視窗不在前景，Play 模式不跑幀（`Application.runInBackground` 設 true 才會跑），而且 MCP 的模擬按鍵送不進 Input Action；T13 是用反射呼叫選單方法與按鈕驗收的，實際按鍵（Esc 開關、手把 B 返回、改鍵等待按鍵）要在前景手動試。
- 戰鬥提示（T14，alpha 1.0.0 測試回饋）：
  - 頭暈倒數：BossHudUI 收到 `OnStunned(duration)` 後把失衡條切成 ResourceBar 的置中模式，用遊戲時間從 1 收到 0（跟 Boss 的頭暈計時一樣被暫停、畫面停頓停住；處決延長的部分停在 0），顏色固定 stunnedColor，不再閃爍。
  - 處決標記：ExecutionMarkerUI 訂閱 Boss 的 `ExecutionTarget.OnExecutableChanged`，頭暈期間（不看距離）在 Boss 的 LockOnTarget.AimPoint 顯示紅色圓環加中心點；送 `OnShownChanged` 讓 LockOnMarkerUI 先藏起鎖定菱形。
  - 按鍵提示：InputHintDevice 用 `InputSystem.onAnyButtonPress`（鍵盤、滑鼠、手把按鍵）加上每幀看滑鼠移動、搖桿推動判斷裝置，啟動時是鍵鼠；改鍵（`InputSystem.onActionChange` 的 BoundControlsChanged）後也會再送一次，讓提示重讀按鍵。改成依裝置顯示的：處決（BossHudUI）、啃骨頭（PlayerHudUI 的 healKeyText，原本寫死「R」）、跳過（SkipPromptUI）、選單每一頁下方的返回提示（PauseMenuData.navigationHintKeyboard／Gamepad）。勝敗畫面按鈕下的「Enter 確認」提示 T16 已經刪掉（勝敗共用一個元件，兩邊都不顯示），BattleResultUI 不再接 InputHintDevice。返回與確認讀的是 UI map（`*/{Cancel}` 這種 usage 綁定，顯示不出鍵名，也不能改鍵），所以這兩個寫成資產裡的文字。接線在 PauseMenuBuilder.Wire（GameOptions 物件上沒有 InputHintDevice 時會補上）。
  - 大迴旋安全區的漂浮盾牌：HazardBuilder 用圓柱、方塊、球拼的佔位圓盾（正反兩面都有十字紋，材質 `Models/Hazards/Materials/Fx_Shield*`，只在不存在時建立），SafeZone 在圈中央約 1.6 m 上下浮動、自轉，判定時消失。
- 難度與選單秘技（T15，alpha 1.0.0 測試回饋；選項是計畫確認過的預設）：
  - 困難 = 企劃書原本的數值，Difficulty.asset 的 hard 全部不修正；簡單照企劃書「難度」一節。出招預兆 +0.15 加在 `BossMove.TelegraphTime`（每招開頭的預兆，預兆閃光、地面提示、法陣閃爍都跟著變長）；PawSwipeMove 覆寫成 + `pawSwipeFirstStrikeBonus`（取代，不疊加）。招式中段的預兆（幻影伏低、三連拍第三下、連環衝撞撞牆後蓄力、追蹤落雷第二波）不加。失衡值累積的倍率乘在 `BossStagger.AddImbalance`，所有來源都吃到。
  - GameOptions 存難度（PlayerPrefs `BossFight.Options.Difficulty`），預設 `GameOptionsData.defaultDifficulty`（困難）。ESC 主選單「難度」頁：選另一個先確認，確定後 `SetDifficulty` 再 `BattleManager.Retry`；這一場的難度是 BattleManager Awake 時讀到的（`BattleManager.Difficulty`、`BattleResult.difficulty`），F12 的每場紀錄也記難度。F12 調的是基礎值，簡單模式的修正疊在上面。
  - 秘技（`PauseMenuData.developerUnlockCode`）：選單開著、不在等改鍵時，鍵盤方向鍵與 A、B 字母鍵，或手把十字鍵與 A（South）、B（East）。結尾能對上秘技開頭幾個就算幾個（↑↑↑ 還是對到 2）；其他按鍵歸零、換頁或關選單也歸零。8 個方向輸完後先取消選取（手把 A 不會按下按鈕），這時按的 A、B 都算秘技、不當成返回；輸完或輸錯就還原選取。解鎖時選單下方顯示提示、播 `developerUnlockSfx`（佔位用 Sfx_Combat_BoneEarned），記在 BattleSession（重試後還顯示，重開遊戲要重輸）。開發者模式開著（例如存檔是開）時開關一直顯示；開關出現後這個場景不再藏起來。沒顯示時設定頁的「返回」補到它的位置。
  - 背景驗收：秘技是用 `InputSystem.QueueStateEvent` 加 `InputSystem.Update()` 模擬鍵盤與虛擬手把（`InputSystem.AddDevice<Gamepad>`）後反射呼叫 PauseMenuUI 驗的；實體鍵盤、手把要在前景手動試。
- 戰敗畫面（T16，alpha 1.0.0 測試回饋）：BattleUIBuilder 在 Result 底下建 `Victory`、`Defeat` 兩組文字，BattleResultUI 依勝敗只開一組，重試按鈕共用；按鈕下面沒有「Enter 確認」按鍵提示（勝敗都沒有）。戰敗：大字「菜」defeatColorDelay 秒後花 defeatColorTime 秒從 defeatTitleStartColor 轉成 defeatColor（真實時間，SmoothStep）、中字「就多練」，重試按鈕下面是剩餘血量／戰鬥時間／挑戰次數排成的一行小字（defeatStatsSeparator 隔開），再下面是灰色小字 defeatHint（`BattleResult.difficulty` 是簡單時隱藏）。「菜」與「就多練」的高度照字形實際範圍（TextGenerator 的 verts）算成：「菜」下緣到「就多練」上緣 = 「就多練」下緣到重試按鈕上緣 = 51（目前 defeatTitleY 176、defeatSubtitleY -44；改字級或字型時要重算）。戰敗的文字、顏色、字級與各行高度（defeatTitleY／SubtitleY／StatsY／HintY，畫面中心 = 0）都在 BattleUIData，Builder 建立時用、BattleResultUI 在 Awake 再套用一次（改資產不用重建）；重試按鈕的位置（-170、高 84）仍是 Builder 寫死、勝敗共用。勝利畫面版面沒變。背景驗收時 MCP 指令往返要好幾秒，「菜」剛出現的白色是用反射設定 shownAt 再呼叫 Update 檢查顏色曲線（0–0.6 秒白、約 2.6 秒全紅），沒有截到白色的畫面。
- 存檔與場景流程（T17，`docs/hub-progression.md`；選項是計畫確認過的預設）：
  - 休息區 `Hub.unity` 由 HubBuilder（Tools > Hub > Build Hub (T17)）整個重建：地板、隱形牆、紫色傳送門方塊、出生點 `Start`（之後營火的位置）與 `Portal`（傳送門前 5 m、面向北），以及 PlayerSpawner（T18；預設生成在 `Start`，出生點清單 Start、Portal）、鏡頭、GameOptions、AudioManager（沒有音樂）、SceneFlow；也把 Build Settings 設成 Hub 0、BossArena 1。T20 照 HubLayoutData 擴充。休息區還沒有 ESC 選單（T20 做「繼續、設定、退出」），也沒有 HUD。
  - 傳送門是暫時版（`Hub/Portal`）：站在門北側的觸發區按 `Player/Interact`，用 `WasPressedThisFrame`，所以 Interact 的 Hold 互動還在（T20 換成 Interactable 時拿掉）；沒有互動提示。
  - SceneFlow（SceneFlowBuilder，Tools > Progression > Build Scene Flow；PauseMenuBuilder.Wire 最後也會跑）：黑幕淡出 0.2 秒、淡入 0.2 秒（SceneFlowData），一幀最多推進 1/30 秒（載入後頭幾幀會卡約 0.45 秒，不夾的話淡入整段被吃掉）。ESC 選單的返回休息區與重置存檔在淡出期間維持暫停，全黑時 SceneFlow 才解除暫停、換場景。直接按 Play 與重試不淡入。換場景進來時由 PlayerSpawner 直接把玩家生成在出生點（T18），SceneFlow 只負責淡入。
  - 擊敗紀錄：Boss 一倒下 BattleManager 就把 `BattleSettings.bossId`（`oiia_cat`）記進 ProfileStore（F6 也算）；T24 改走 ProgressRecorder 並發首通獎勵。
  - 勝敗畫面：重試與「返回休息區」左右並排（x ±200、y −170），返回休息區不先確認；ESC 主選單在重新挑戰下面加「返回休息區」（先確認）；設定頁的「重置存檔」只在開發者模式開著時顯示（開關下面），確定後清空存檔、回休息區的 `Start`。
  - 背景驗收：傳送門是 `InputSystem.QueueStateEvent` 模擬 E 鍵後反射呼叫 `Portal.Update`；選單、勝敗按鈕用反射呼叫 onClick；黑幕用 `Application.onBeforeRender` 每幀記 alpha。實際按 E、Esc 要在前景手動試。
- 角色 prefab 化與生成接線（T18）：
  - 刀盾狗 prefab 在 `Assets/Prefabs/Characters/DaoDunDog.prefab`（DaoDunDogBuilder 第一次跑時用 `AssetDatabase.MoveAsset` 從 `Assets/Prefabs` 搬過來，GUID 不變），根物件掛 PlayerRig；DaoDunDogBuilder 不再把角色放進場景，改建 PlayerSpawner 與預覽模型（`PlayerSpawnerBuilder.Build`，沿用舊 spawner 的位置與出生點清單）再建鏡頭。
  - BossArena 從頭重建的順序：Build DaoDun Dog（PlayerSpawnerBuilder.Build 會先跑 `ProgressionBuilder.EnsureAssets` 建好 GameDatabase 與刀盾狗的定義）→ Build Boss Placeholder → Build Intro（會接著重建轉階段、戰鬥 UI、ESC 選單、SceneFlow、打擊回饋，每一步最後都重跑 `PlayerSpawnerBuilder.Wire`）；休息區跑 Build Hub。
  - T19 起 spawner 接的是 GameDatabase，依存檔的目前角色（`ProfileStore.CurrentCharacter`，空的或找不到時用名冊第一個）生成；`ReplaceCharacter(CharacterDefinition)` 已經能用（在目前位置生成新角色、銷毀舊的、套用數值、重新 BindPlayer），選單介面是 T21。
  - 背景驗收：Hub↔BossArena 用反射呼叫 `SceneFlow.GoToBattle`／`BattleManager.ReturnToHub`；開場跳過、鎖定、處決用反射呼叫 `CutsceneDirector.Skip`、`PlayerController.SetLockTarget`、`TryStartExecution`；換角色在 Hub 與 BossArena 各用反射呼叫一次 `ReplaceCharacter`。DeveloperMode 開場時會清掉 F2 無敵，背景驗收要等一兩秒再設 `ForcedInvulnerable`。Timeline 編輯模式預覽用 `capture_game_view --source camera` 截（Unity 在背景時 Game 視窗不重畫）。
- 數值管線（T19）背景驗收：測試修正開關用反射設 BalanceDebugPanel 的 static `testModifiersOn` 再呼叫 `ApplyTestModifiers(true)`；實際跑速是 `InputSystem.QueueStateEvent` 按住 D（不要手動 `InputSystem.Update()`，否則按鈕類動作的 WasPressedThisFrame 會被吃掉），用 `Application.onBeforeRender` 記錄最大 `PlanarSpeed`（關 5.000、開 6.000）；F12 面板用 `capture_game_view --save_path` 截圖。Boss 在背景會一直打玩家，量血量前先設 `ForcedInvulnerable`。
- 測試木樁 `TrainingDummy` 已經在 T3 從場景移除（程式與 `TrainingDummyData.asset` 還在 `Scripts/Debug`、`ScriptableObjects/Debug`）。要練格擋或盾反時用 Tools > Debug > Build Training Dummy 重建，它會自己加進 PlayerSpawner 的鎖定清單；正式 build 前要刪掉物件，否則會變成 Missing Script。

## 工作流程

1. 一個任務開一次對話。開始前先讀這份檔案，以及 `docs/boss-oiia.md`（T0–T16）或 `docs/hub-progression.md`（T17 起）的對應章節。
2. 先列計畫，確認後再實作。
3. 實作完依序確認：Unity 編譯沒有錯誤 → 進 Play 模式驗收（下表）→ 通過後 commit，訊息格式 `T<n>: <摘要>`，然後才做下一個任務。
4. 每個任務收尾都要檢查：
   - Console 沒有新的錯誤或警告。
   - 新程式沒有寫死的玩法數值。
   - 執行期程式沒有 Find 系列呼叫：`rg "Find(Any|First)?Object(s)?(OfType|ByType)|GameObject\.Find|FindWithTag" Assets/Scripts --glob "!Editor/**"` 應該沒有結果。
   - T17 起：T0–T16 的驗收沒有壞掉（照 `docs/playtest-checklist.md` 快速跑一輪）。

## 任務驗收

T0–T16 在 `BossArena` 場景進 Play 模式驗收；T17 起從 `Hub` 場景進 Play（要進 BossArena 時走傳送門）。

| # | 任務 | 驗收方式 |
| --- | --- | --- |
| T0 | 專案骨架與 CLAUDE.md | Console 沒有錯誤；刀盾狗站在南側出生點（離中心 8 m）；從中心往 8 個方向打射線，都在約 12 m 處打到 `ArenaWall`；天空是暗灰藍的雷雲佔位 |
| T1 | 玩家移動、鏡頭鎖定、翻滾、耐力 | 依鏡頭方向以 5 m/s 移動；中鍵鎖定 20 m 內的假 Boss，鎖定時繞行，再按一次解除；Left Alt 翻滾 3 m、前 0.3 秒無敵、耐力 −25；耐力每秒回 30、舉盾時 10（T12 改為上限 75、每秒回 12、舉盾 1 秒後 30）；數值都在 PlayerStats |
| T2 | 玩家攻防與傷害系統 | 對木樁打出 10 / 10 / 16 三段，第三段前踏；長按 0.8 秒重擊 35、耐力 −30；舉盾擋正面 120°、減傷 80%，耐力歸零破防 1 秒；命中前 0.15 秒內舉盾判定盾反（免傷、停頓 0.1 秒）；R 回 40 HP，每場 3 次（T12 改為可累積、最多持有 3 根）；Space 跳躍約 1.2 m |
| T3 | Boss 狀態機、招式資料、選招 AI | 假招式跑完 Idle → ChooseMove → Telegraph → Active → Recovery 循環；選招依距離 → 冷卻 → 加權隨機，且不連出同一招；固定開場序列生效；F1–F4 都可用 |
| T4 | 第一階段 5 招與危害物件 | 用 F3 逐一觸發 5 招；可擋招閃白光、不可擋招閃紅光，格擋與盾反判定正確；迴旋衝撞撞牆後硬直 2 秒；開場固定序列為 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷 |
| T5 | 失衡值、頭暈、處決 | 盾反 4 次讓 Boss 頭暈；頭暈中傷害 ×1.5；F 處決造成 150；處決或頭暈結束後失衡值歸零；3 秒沒有增加後每秒 −8 |
| T6 | 開場動畫 | 14.5 秒照分鏡播完 7 個鏡頭，期間輸入鎖定；結束後直接接迴旋衝撞；第二次挑戰起長按 Space 1 秒可跳過 |
| T7 | 轉階段演出與第二階段強化招式 | F1 把 Boss 設為 1001 再打一下：清除 Hazard、Boss 無敵、輸入鎖定，播完 5 步驟演出；音樂 1.2 倍速；4 個強化招可用 |
| T8 | 龍捲風、幻影分身、終極技、狂暴 | HP 首次 ≤ 400 觸發大迴旋一次，落地頭暈 3 秒；之後進入狂暴（出招間隔 0.3 秒、冷卻 ×0.75、音樂 1.3 倍速）；龍捲風、幻影分身可用 |
| T9 | 戰鬥 UI 與勝敗流程 | 玩家血條、耐力條、骨頭次數、Boss 血條（50% 與 20% 刻度）、失衡條都隨事件更新；BOSS FIGHT 字卡；勝利畫面；失敗可重試，重試後可跳過開場 |
| T10 | 平衡測試與收尾 | F12 面板能即時調整數值；完整打通 3 次，通關時間 4–5 分鐘 |
| T11 | 打擊回饋（音效、鏡頭震動、預兆音效） | 揮刀、命中（輕、第三段、重擊、處決分級）、格擋、破防、盾反、受擊、翻滾、跳躍、啃骨頭有音效；白光預兆「叮」、紅光預兆嗡嗡聲；Boss 撞牆、落地、衝擊波、落雷、龍捲風、大迴旋落地、頭暈、倒下有音效與鏡頭震動；盾反停頓與勝利慢動作照舊 |
| T12 | 刀盾狗耐力與骨頭累積 | 耐力上限 75：滿耐力連續翻滾 3 次，第 4 次要等耐力回到 25；耐力每秒回 12，舉盾滿 1 秒後每秒 30（耐力條變青色）；刀命中 +0.05 根、處決 +0.3 根、被打中／格擋／破防 +0.02 根（盾反、閃過不算），湊滿一根有音效，持有 3 根時不累積；HUD 骨頭下方有累積進度條；F12「刀盾狗」可調這些值 |
| T13 | ESC 選單（設定、重新挑戰、退出） | Esc／手把 Start 開關選單，開著時遊戲暫停（演出也停住）、音效暫停、音樂照播；設定 → 按鍵設定（鍵鼠主要／次要兩欄、控制器一欄，改完立刻生效、重進 Play 還在、同鍵互換、恢復預設）、音量設定（主音量、音樂、音效即時生效並保存）、開發者模式開／關（關掉後 F1–F12 與 Debug 面板都沒有）；重新挑戰與退出遊戲先確認，重試後可跳過開場；頭暈與跳過提示顯示目前的鍵 |
| T14 | 戰鬥提示與演出（alpha 1.0.0 回饋） | Boss 頭暈時失衡條從左右往中間收縮，收到中間時頭暈剛好結束；頭暈時 Boss 胸口出現紅色處決標記（鎖定菱形先藏起來）；按鍵盤或滑鼠時處決、啃骨頭、跳過、選單返回的提示是鍵鼠的鍵，按手把時換成手把的鍵，改鍵後跟著變；轉階段第一次不能跳過，同一次執行第二次起可以長按跳過，跳過後法陣全紅、電光、音樂 1.2 倍、玩家在 10 m、進入第二階段；大迴旋兩個安全區中央各有一面漂浮盾牌 |
| T15 | 難度（簡單模式）與選單秘技（alpha 1.0.0 回饋） | 預設困難，行為與 alpha 1.0.0 相同；ESC 主選單「難度」頁標出目前難度（不列差異說明），選另一個先確認（確認框文字不超出面板）、確定後重新挑戰；重進 Play 難度還在；簡單模式下翻滾無敵 +0.1、盾反 +0.05、失衡值累積 ×1.15、衰減 ×0.85、出招預兆 +0.15（貓掌第一下 +0.2）、龍捲風速度 ×0.9、雙重衝擊波間隔 +0.1；開發者模式預設關、設定頁沒有開關，選單裡輸入 ↑↑↓↓←←→→ A B（鍵盤或手把）後出現開關與提示、重試後還在；player 編譯（有 `BOSSFIGHT_DEV_TOOLS`）包含 Debug 程式 |
| T16 | 戰敗畫面（alpha 1.0.0 回饋） | F7 讓玩家倒下：中央大字「菜」剛出現白色、逐漸轉紅；下面中字「就多練」（白），「菜」到「就多練」與「就多練」到重試按鈕的間距相等；重試按鈕下面是橫排成一行小字的剩餘血量、戰鬥時間、挑戰次數，再下面是灰色小字「真打不過我們選單裡有簡單模式喔 :))」（簡單模式不顯示）；勝敗畫面都沒有「Enter 確認」提示；沒有「刀盾狗倒下了」；重試照常；F6 勝利畫面版面不變 |
| T17 | 存檔與場景流程 | 新增空的 Hub 場景（地板＋傳送門方塊）；Build Settings Hub 0、BossArena 1；從 Hub 走到傳送門按 E 進 BossArena；勝利後 `save.json` 記錄擊敗 OIIA 貓；勝敗畫面與 ESC 選單「返回休息區」可回到 Hub、出生在傳送門前；關掉重開紀錄還在；壞檔會備份並開新檔；開發者模式可重置存檔 |
| T18 | 角色 prefab 化與生成接線 | 刀盾狗變成 prefab，BossArena 與 Hub 都由 PlayerSpawner 生成；T0–T13 驗收全部照舊（開場、轉階段演出、鎖定、處決、HUD、F12、ESC 選單）；Timeline 編輯模式仍可預覽；執行期沒有 Find 系列呼叫 |
| T19 | 角色定義與數值管線 | CharacterRoster、CharacterDefinition、StatModifier、PlayerEffect；刀盾狗從定義載入；Debug 加一個測試修正（血量 +20、跑速 ×1.2）時 HUD 血條上限、實際跑速、F12 面板都反映；關掉後回到原值；`PlayerStats.asset`（T19 起是 `Progression/Characters/DaoDunDogStats.asset`）沒有被改到 |
| T20 | 休息區場景 | HubBuilder 照草圖建出邊界、樹木、隱形牆、四個站點佔位、小路、傍晚天色與營火光；走不出邊界；靠近站點出現互動提示（顯示目前的鍵），離開消失；開介面時輸入鎖定、游標放開、鏡頭切到站點鏡頭；休息區 ESC 選單只有繼續、設定、退出 |
| T21 | 營火與角色切換 | 按 E 坐下、鏡頭拉近；「角色」頁列出已解鎖角色（開發者模式多出測試狗）；切換後外觀、數值、HUD 都換成新角色，位置不變；存檔記住目前角色，重開與進 BossArena 都用它 |
| T22 | 裝備系統與鐵匠鋪 | 鐵匠鋪介面顯示目前角色的欄位（刀盾狗 2 格、測試狗 1 格）；Debug 解鎖兩件測試裝備後可穿脫；厚皮項圈讓血量上限 +20；反擊護符盾反回 5 HP（F12 木樁或 Boss 驗證）；換角色後各自的穿戴保留；數值差異預覽正確 |
| T23 | 技能樹 | 營火「技能樹」頁顯示刀盾狗 5 個節點與連線；前置與點數不足時不能學；學「骨頭收藏家」後持有上限 4；重置退回全部點數；測試狗的樹與點數使用分開記錄 |
| T24 | 村長任務與首通獎勵 | 第一次進休息區村長自動講開場；之後村長頭上「!」；接「轉暈那隻貓」→ 打贏 OIIA 貓 → 勝利畫面顯示首通獎勵（技能點 ×3、厚皮項圈）→ 回休息區村長「?」→ 回報拿到技能點 ×2、反擊護符；第二次打贏不再給首通獎勵；鐵匠鋪新裝備標「新」 |


## Skill 使用規則
- 打擊感、受擊回饋、畫面震動：參考 game-feel
- Boss AI、狀態機：參考 game-ai
- 鏡頭：參考 camera-systems
- 光效、後處理：參考 urp-postprocessing
- skill 建議與 docs/boss-oiia.md 衝突時，以企劃書為準