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
- 場景：`Assets/Scenes/Boot.unity`（開機分流，Build Settings 第 0 個：BootRouter 看存檔，序章沒完成就進 Prologue、否則進 Hub）、`Assets/Scenes/Hub.unity`（休息區，第 1 個）、`Assets/Scenes/BossArena.unity`（Boss 戰，第 2 個）、`Assets/Scenes/Prologue.unity`（序章，第 3 個）；之間都靠 Progression 的 SceneFlow 換場景。編輯器裡直接在 Hub、BossArena 或 Prologue 按 Play 就從那個場景開始（不分流）
- 單位：距離為公尺、時間為秒、角度為度。俯視角度 0 = 北（+Z），順時針遞增；玩家從南側（180°）進場，Boss 在場地中心。

## 資料夾

| 路徑 | 用途 |
| --- | --- |
| `Assets/Scripts/Battle` | 最上層的流程指揮（開發者工具的入口介面 IBattleDebugTarget 也在這層，BattleManager 與序章的 PrologueCutsceneFlow 各自實作）：BattleManager（戰鬥流程狀態機 Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk → Victory／Defeat），指揮 Boss、玩家與演出（播轉階段演出、結束時呼叫 `BossController.CompleteTransition`、換階段改音樂速度；勝利時慢動作與音樂淡出、送 `OnBattleResult`、`Retry` 重新載入場景；ESC 選單暫停時鎖住玩家與鏡頭、放開游標；Awake 時把難度修正交給玩家與 Boss；ESC 選單秘技的解鎖記在 BattleSession）；BattleSettings（勝敗節奏）；序章（T25–T30）：教學 TutorialDirector／TutorialData／TutorialStepData（每句台詞的動作與鏡頭）、大樹守衛的流程 GuardianPhaseController／GuardianPhaseTwo／GuardianSummonController／GuardianBattleData、結局 PrologueOutcome、演出協調 PrologueCutsceneFlow（開場、登場、轉階段、擊倒、勝利）、教學角色的玩家綁定 PrologueActorBinding |
| `Assets/Scripts/Core` | 跨系統共用（最底層，不引用其他系統）：預兆分類色 TelegraphPaletteData、BattleSession（這次執行的挑戰次數、「看過開場」「看過轉階段」與「秘技解鎖了開發者模式開關」）、事件、傷害系統（DamageInfo、Hitbox、Hurtbox、IDamageable）、畫面停頓（HitStop）、AudioManager（戰鬥音樂與變速、音效）、競技場（ArenaData、ArenaGenerator、天色 ArenaAtmosphere／AtmosphereData）、LockOnTarget、可處決目標（ExecutionTarget）、打擊回饋的資料與播放（CombatFeedbackData、FeedbackEmitter：音效＋Cinemachine Impulse 鏡頭震動；Core 只有這裡用到 Unity.Cinemachine）、玩家設定（GameOptions／GameOptionsData：音量、開發者模式、改鍵、難度，存 PlayerPrefs）、難度（Difficulty、DifficultyData／DifficultyModifiers：簡單模式的修正值）、暫停（GamePause：timeScale 0、音效暫停） |
| `Assets/Scripts/Player` | PlayerController、PlayerAttack、PlayerHealth、PlayerStamina、PlayerStats 定義、DaoDunDogAnimator（T21 起有營火的坐姿 `SetSitting`）、角色 prefab 根物件的 PlayerRig（公開玩家元件、鏡頭跟隨點、臉部錨點）、接收玩家的介面 IPlayerBinding、打擊回饋（PlayerFeedback：揮刀、命中、格擋、盾反、受擊等的音效與震動） |
| `Assets/Scripts/Progression` | 跨場景的進度與流程：SceneFlow（換場景、黑幕淡出淡入）、SceneFlowData、開機分流 BootRouter（Boot 場景）、休息區與序章共用的輸入鎖仲裁 InputLockArbiter、PlayerSpawner（生成玩家角色、依這次的出生點擺位置、交給 IPlayerBinding）；角色與數值管線（T19）：CharacterDefinition、CharacterRoster、GameDatabase（id 換回資產）、StatModifier／PlayerStatId、PlayerEffect／PlayerEffectBehaviour、PlayerLoadout；裝備（T22）：EquipmentSlotType、EquipmentData、EquipmentLoadout（存檔的穿戴 → 數值管線的「equipment」來源）、效果 ParryHealEffect／ParryHealBehaviour（盾反回血）；技能樹（T23）：SkillTreeData（節點 SkillNode）、SkillTreeLoadout（學習與重置的規則、節點狀態 SkillNodeState、已學節點 → 數值管線的「skills」來源）；任務與獎勵（T24）：RewardData（RewardKind：技能點／裝備／角色）、BossDefinition（Boss 的 id、場景、首通獎勵、傳送門說明）、QuestData（對白、目標 QuestObjective、獎勵、前置；狀態 QuestState）、ProgressRecorder（static、沒有狀態：擊敗與首通、任務狀態、接任務與回報、發獎勵） |
| `Assets/Scripts/Hub` | 休息區（T20）：HubManager（自由走動／介面開著、站點鏡頭、輸入鎖定、秘技解鎖）、站點 Interactable（站點種類 HubStationKind、站位點、站點鏡頭）與傳送門 Portal（子類別，T24 起挑戰清單 `OnBossesChanged` 讀 GameDatabase.bosses、`Depart(boss)` 換到那隻 Boss 的場景）、InteractionScanner（範圍內最近的站點）、版面 HubLayoutData、天色 HubAtmosphere／HubAtmosphereData、營火光閃爍 FireFlicker、旋轉 Spin；營火 Campfire（T21，子類別：坐下、角色清單 `OnCharactersChanged`、`SelectCharacter` 換角色）；營火的技能樹（T23：`OnSkillTreeChanged`、`Learn`、`ResetSkills`、`PreviewNode` 數值差異、`DebugGiveSkillPoints`）；鐵匠鋪 Blacksmith（T22，子類別：`OnViewChanged`、`Toggle`／`Unequip` 換裝、`PreviewItem`／`PreviewUnequip` 數值差異、`DebugUnlockAll`）；村長 VillageChief（T24，子類別：對話 `OnDialogue`、頭上標記 `OnMarkerChanged`、`Accept` 接任務、開場劇情 `BeginIntro` 走過來）與劇情資料 HubStoryData |
| `Assets/Scripts/Camera` | CameraController（Cinemachine 第三人稱與鎖定鏡頭；鎖定哪個目標由 PlayerController 決定，鏡頭訂閱 `OnLockTargetChanged`） |
| `Assets/Scripts/Boss` | BossController 狀態機與選招、BossHealth、失衡值／頭暈／處決（BossStagger）、BossMove 基底類別、BossStats／BossMoveData／BossPhaseData 定義、預兆閃光（BossTelegraphFlash）、佔位模型的程序化姿勢（OiiaCatPoser）、命中火花（BossHitFeedback，Boss 打到玩家）、受擊反饋（BossDamageFeedback，玩家打到 Boss：火花、閃白、後仰、傷害數字）、頭暈星星（BossStunVisual）、強化階段外觀（BossPhaseVisual：紅色電光與殘影）、幻影分身的幻影（BossPhantom，一擊即碎）、招式衝擊與預兆的音效與震動（BossImpactFeedback，訂閱 `OnMoveTelegraph`／`OnMoveImpact`）；序章：菊花 DaisyMentor（DaisyMentorVisual、DaisyMentorData，說話的動作 DaisyGesture）、小樹精 TrainingSapling（TrainingSaplingVisual、TrainingSaplingData，示範甩枝 Demonstrate、表情 SaplingGesture）、教學角色回饋 TeachingActorFeedback、守衛外觀 GuardianPoser／GuardianFullPowerVisual、命中火花的來源介面 IHitReportSource |
| `Assets/Scripts/Boss/Moves` | 每招一個 BossMove 子類別；招式特有的數值寫成 BossMoveData 的子類別放在同一個資料夾；PounceRunner 是一次飛撲（扭屁股飛撲與幻影分身共用） |
| `Assets/Scripts/Hazards` | 場上判定物件（Hazard 基底、衝擊波 ShockwaveRing、落雷 LightningStrike、龍捲風 TornadoVortex、大迴旋的全場判定 GrandSpinField 與安全區 SafeZone）、地面範圍提示（GroundTelegraph，URP Decal）、場地中央的魔法陣（MagicCircle，URP Decal，出現進度／脈動／顏色／半徑、幻影分身的閃爍、大迴旋安全區的符文熄滅）、一次性特效（TimedFx、傷害數字 DamageNumber）、佔位電光（ElectricArcs，Boss 身上與轉階段的場地邊緣共用）；序章守衛的範圍攻擊 AreaStrike（AttackArea、AttackAreaFx） |
| `Assets/Scripts/Cutscene` | CutsceneDirector（播一段演出的 Timeline、鎖輸入、長按跳過、字卡事件；開場與轉階段各一個物件）、CutsceneStage（轉階段時依玩家位置擺演出鏡頭）、IntroCutsceneSettings、Timeline 自訂 Track／Clip（CurveClip 加 CurveTrack／StormTrack／BossSpinTrack／MagicCircleTrack／ElectricArcsTrack、ActorWalk、ActorPush、BossActor、CameraMove、MusicCue、CutsceneEventMarker）、演出物件（ScreenFlash、IntroLightning、SmokePuffs、CameraShaker、ExpandingRing）；序章：Timeline 表現接收者 PrologueCinematicStage、PrologueCutsceneData、提示點 CutsceneCueMarker（CutsceneCue enum）、教學對白鏡頭 TutorialStage／TutorialStagingData |
| `Assets/Scripts/UI` | 只訂閱事件（uGUI）：BattleUI（HUD 何時出現、Boss 血條滑入）、PlayerHudUI（左下血條、耐力、骨頭）、BossHudUI（下方置中 Boss 名稱、血條與刻度、失衡條、頭暈倒數）、LockOnMarkerUI、處決標記（ExecutionMarkerUI）、BattleResultUI（勝敗畫面與重試按鈕；勝利、戰敗各一組文字）、ResourceBar（量條與延遲扣血，置中模式給頭暈倒數）、BattleUIData；開場的字卡（TitleCardUI）、跳過提示（SkipPromptUI，開場與轉階段共用）；ESC 選單（PauseMenuUI：含難度頁與解鎖開發者模式的秘技；改鍵頁 KeyBindingPageUI／KeyBindingRowUI、PauseMenuData）、按鍵顯示名稱（InputNameData、BindingText）、按鍵提示顯示鍵鼠還是手把（InputHintDevice，放在 GameOptions 物件上）；休息區（T20）的互動提示（InteractionPromptUI）、傳送門的挑戰清單（HubStationUI，T24 起只剩傳送門）、村長的對話框（DialogueUI，T24）與頭上的「!」「?」（QuestMarkerUI，T24）、營火面板（CampfireUI，T21：「角色」「技能樹」頁；T23 的技能樹頁與一個節點 SkillNodeCellUI）、鐵匠鋪面板（BlacksmithUI 與一格的 EquipmentCellUI，T22）、HubUIData；序章：操作提示 TutorialHudUI／TutorialUIData（「2 / 6　啃骨頭」進度）、小樹精頭上的量條 TrainingSaplingHudUI、守衛 HUD GuardianBattleUI／GuardianPhaseFeedbackUI；村長的任務選項轉接 HubDialogueUI |
| `Assets/Scripts/Debug` | 任何 Boss 戰共用的開發者工具（按鍵表見下方「開發者工具」）：Debug 快捷鍵 F1–F7 與右上角的 Boss 面板（BossDebugTools，流程相關的鍵交給 `Battle/IBattleDebugTarget`）、平衡面板（BalanceDebugPanel，F12：執行期覆寫 SO 數值、計時與統計，見下方「平衡面板」）、左上角的玩家狀態（PlayerDebugOverlay）、序章專屬的 F8–F11 與跳關面板（PrologueDebugTools）、開發者模式開關（DeveloperMode：每個場景一個，開關這個場景的所有 Debug 工具，實作 IDebugCheats 的在關掉時清作弊；ESC 選單開著時也先停用）、測試木樁（TrainingDummy）、F12「玩家」頁開關的測試修正（TestModifierData） |
| `Assets/Scripts/Editor` | 編輯器工具（DebugToolsBuilder〔每個 Boss 戰場景的開發者工具〕、BootBuilder、ArenaBuilder、DaoDunDogBuilder、CameraRigBuilder、TrainingDummyBuilder、BossBuilder、HazardBuilder、IntroCutsceneBuilder、TransitionCutsceneBuilder、BattleUIBuilder、PlaceholderAudioBuilder、BalancePanelBuilder、CombatFeedbackBuilder、PauseMenuBuilder、SceneFlowBuilder、HubBuilder、HubUIBuilder、PlayerSpawnerBuilder、ProgressionBuilder、GameDatabaseValidator），不會進 build |
| `Assets/ScriptableObjects` | 數值資產（`.asset`），依系統分子資料夾：`Arena/`（ArenaData、Atmosphere 天色）、`Boss/`（BossStats）、`Boss/Phases/`、`Boss/Moves/Phase1/`（第一階段 5 招）、`Boss/Moves/Phase2/`（第二階段 4 個強化招，和第一階段同一招共用類別；新招龍捲風、幻影分身與終極技大迴旋）、`Cutscene/`（IntroCutsceneSettings）、`Battle/`（BattleSettings）、`UI/`（BattleUIData：血條刻度、動畫時間、顏色、文字；PauseMenuData：ESC 選單的文字、顏色、改鍵清單；InputNameData：按鍵顯示名稱）、`Options/`（GameOptionsData：玩家設定的預設值；Difficulty：各難度的修正值）、`Feedback/`（CombatFeedback：打擊回饋的音效、音量、鏡頭震動）、`Debug/`（測試木樁、T19 的測試修正 TestModifiers）、`Progression/`（SceneFlowData、GameDatabase、CharacterRoster；`Characters/` 是各角色的 CharacterDefinition 與基礎數值，刀盾狗的是 `DaoDunDog.asset`、`DaoDunDogStats.asset`（T18 以前的 `Player/PlayerStats.asset`，GUID 不變），測試狗（T21）是 `TestDog.asset`、`TestDogStats.asset`；`Equipment/`（T22）是欄位種類 `Slot_Collar`／`Slot_Amulet`、厚皮項圈 `ThickCollar`、反擊護符 `CounterAmulet` 與它的效果 `ParryHeal`；`SkillTrees/`（T23）是刀盾狗 `DaoDunDogTree`、測試狗 `TestDogTree`；`Bosses/`（T24）是 OIIA 貓 `OiiaCat`；`Quests/`（T24）是測試任務 `SpinTheCat`）、`Hub/`（T20 起：HubLayout 版面、HubAtmosphere 休息區天色、HubUIData 介面文字與顏色；T24 的 HubStory 村長名稱、開場與閒聊對白）；序章：`Tutorial/`（Tutorial、`Steps/` 六步、對白鏡頭 TutorialStaging）、`Prologue/`（DaisyMentor、TrainingSapling、呼喚孩子的 AngrySapling）、`Boss/Guardian/`（守衛的 Stats、Arena、Phases、Moves）、`Battle/GuardianBattle`、`Cutscene/PrologueCutscene`（與 `Cutscene/Prologue/` 後處理）、`Progression/PrologueLayout`、`Feedback/TelegraphPalette` |
| `Assets/Timelines` | Timeline 資產：`Intro.playable`（開場 14.5 秒）、`Transition.playable`（轉階段 5.0 秒）；鏡頭與各效果的時間點都在這裡調；`Timelines/Prologue/`：序章開場 `PrologueOpening`（3.5 秒）、`GuardianEntrance`、`GuardianTransition`、`Knockdown`、`GuardianVictory`、休息區醒來 `HubWake`（PrologueCutsceneBuilder 只在不存在時建立） |
| `Assets/Prefabs` | 玩家角色（`Prefabs/Characters/DaoDunDog.prefab` 與測試狗的 prefab variant `DaoDunDog_Test.prefab`，DaoDunDogBuilder 產生）、Boss（`OiiaCat`、幻影 `OiiaCatPhantom`，BossBuilder 產生）、UI 的 prefab；`Prefabs/Hazards` 是地面提示、Hazard 與特效，由 HazardBuilder 產生，重建會覆寫 |
| `Assets/Shaders` | 自製 shader（都是手寫 JSON 的 URP Decal Shader Graph，Custom Function 用 UV 畫圖）：`TelegraphDecal.shadergraph`（地面範圍提示：圓、環、長方形、扇形、十字、全場挖最多 3 個安全圈，可裁切到場地內；序章修正 C1 擴充；圓／環可切成等角度的小段，參數借用 Size.zw 與 Field.w）、`MagicCircleDecal.shadergraph`（魔法陣：雙環、符文帶、倒五芒星，由中心描繪、外圈分段點亮；`_Holes` 加上 Size.y、Params.w 是大迴旋安全區的熄滅區） |
| `Assets/Scenes` | `Hub.unity`（休息區，HubBuilder 產生）、`BossArena.unity`（`SampleScene` 是範本殘留，不在 Build Settings）；`Boot.unity`（開機分流，BootBuilder）、`Prologue.unity`（序章，PrologueBuilder） |
| `Assets/Audio` | BGM 與音效；OIIA 音樂放 `Assets/Audio/oiia_bgm`，放進來後在場景 `AudioManager` 的 battleMusic 指定（重跑 IntroCutsceneBuilder 也會自動找路徑含 oiia_bgm 的音檔）；`Audio/Placeholder` 是 PlaceholderAudioBuilder 合成的佔位音效（戰鬥音效是 `Sfx_Combat_*`），重建會覆寫 |
| `Assets/Models` | 模型、網格、材質；`Models/Arena`、`Models/DaoDunDog`、`Models/TrainingDummy`、`Models/OiiaCat`、`Models/Hazards`、`Models/Cutscene`、`Models/Hub` 由 Builder 自動產生，重建會覆寫網格；`Models/UI` 是 BattleUIBuilder 產生的 UI 圖（處決標記的圓環），`Models/UI/Equipment` 是 ProgressionBuilder 產生的裝備佔位圖示（T22），都只在不存在時產生 |
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
- 第一個 static 例外是 `Core/BattleSession`（第幾次挑戰、看過開場沒有、看過轉階段沒有、ESC 選單的秘技解鎖了沒有）：重新載入場景後元件都是新的，跨場景的資料要放在活得比場景久的地方。它只放這四個值、不放元件引用，進 Play 模式時清空；只有 BattleManager 讀寫，其他系統透過 BattleManager 的事件或參數得知（選單呼叫 `BattleManager.UnlockDeveloperOptions`、訂閱 `OnDeveloperOptionsUnlocked`）。唯一的例外是休息區的 HubManager（T20，使用者決定）：秘技解鎖也讀寫（`HubManager.UnlockDeveloperOptions`／`OnDeveloperOptionsUnlocked`），兩個場景共用。
- 第二個 static 例外是 `Core/ProfileStore`（存檔）：只放純資料，存的是 id 字串，不放 SO 或元件引用（之後 Progression 用 GameDatabase 把 id 換回資產）。存在 `Application.persistentDataPath/save.json`（JsonUtility），有變動就存、離開遊戲再存一次；讀檔失敗時把壞檔改名成 `save.bak.json`、開新檔並在 Console 警告。第一次讀到時才載入，進 Play 模式時清掉載入狀態（Domain Reload 關閉時也會重新讀檔）。「這次要在哪個出生點出現」（`SetArrival`／`PeekArrival`／`TakeArrival`）只活在這次執行、不寫進檔案：新場景的 PlayerSpawner 先看（決定生成位置），SceneFlow 再取走（決定要不要淡入）。玩家設定照舊存 PlayerPrefs（GameOptions），兩者分開。存檔只加欄位時版本號不用改（舊檔讀到的是空清單，Load 補 null）；T22 加了已解鎖裝備、「新」標記與各角色的穿戴（`CharacterEquipmentSave`：角色 id 加上照 `equipmentSlots` 順序的裝備 id）；T23 加了技能點池 `skillPoints`（全帳號共用）與各角色學過的技能（`CharacterSkillsSave`：角色 id、節點 id、付過的點數 `spent`）；T24 加了看過開場劇情 `storyIntroSeen`、已接任務 `acceptedQuests` 與已完成任務 `completedQuests`（「可回報」不存，由擊敗紀錄算）。
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
  - 技能（T23）：`SkillTreeLoadout.Register` 把目前角色學過的節點（只算目前樹裡還有的節點，前置不再檢查）登記成 `skills` 來源，PlayerSpawner 生成與換角色時跟裝備一起登記；營火學習或重置後 `PlayerSpawner.ReapplySkills`（重新登記 → `ReapplyStats(true)`）。
  - 效果：繼承 `PlayerEffect<TBehaviour>`（SO 只放參數），`TBehaviour : PlayerEffectBehaviour` 掛在角色根物件上、Awake 拿到 `Rig`、OnEnable 訂閱玩家事件；第一個實際效果是 T22 的盾反回血（`ParryHealEffect`）。差異預覽用 `PlayerLoadout.Preview(key, modifiers)`（換掉一個來源後的數值複本，用完要 Destroy）與 `PlayerLoadout.Read`（跟 `Modify` 同一組對應，新增 `PlayerStatId` 時兩個 switch 都要加）。
  - 資產由 ProgressionBuilder（Tools > Progression > Build Game Database）建立；GameDatabase 的 id 檢查在 Tools > Progression > Validate Game Database，打包前也會自動檢查（空 id、同類重複、角色沒接 prefab 或 baseStats 都會停止打包）。新增種類時在 `GameDatabase.Entries` 加。
- 輸入只用新版 Input System，從 `InputSystem.actions` 讀 `Player` map；新動作加進 `InputSystem_Actions.inputactions`。`Player/SkipCutscene`（Space、手把 A）是長按跳過開場與轉階段用的，和 Jump 同鍵；演出期間 PlayerController／CameraController 被 `SetInputLocked` 鎖住，不會跳起來。
- `Player/Interact`（E、手把 Y，T20 拿掉 Hold，按一下就觸發）是休息區站點的互動；手把 Y 跟處決同鍵（休息區沒有處決、Boss 戰沒有互動），所以改鍵清單裡這一列 `checkConflict` 關掉，改鍵時不跟別的動作互換。
- `Player/Pause`（Esc、手把 Start）開關 ESC 選單，不在改鍵清單裡、也不能被綁到別的動作；選單裡的返回另外讀 `UI/Cancel`（Esc、手把 B）。CameraController 不再處理 Esc（以前 Esc 放開游標）。
- 改鍵是 `InputSystem.actions` 的 binding override，由 GameOptions 存成 PlayerPrefs 的 JSON、Awake 時套回去；遊戲程式照常 `FindAction` 就會拿到改過的鍵。鍵鼠頁的「次要」欄是 Keyboard&Mouse 控制組裡的第 2 個綁定，所以只有一個鍵鼠綁定的動作在 `.inputactions` 裡補了空綁定（path 空字串）；改鍵清單（PauseMenuData.bindings）加新動作時也要補。畫面上的按鍵提示用 `BindingText.FirstDisplay` 讀目前的鍵，不要寫死鍵名；控制組用 `BindingText.Group(gamepad)`，gamepad 來自 `InputHintDevice.OnDeviceChanged`（只顯示目前裝置的鍵，不要同時列鍵鼠與手把）。
- 演出（開場、之後的轉階段）由 CutsceneDirector 鎖輸入：`PlayerController.SetInputLocked`（不讀按鍵、不能切換鎖定，只照 `ScriptedMoveTo` 走）、`CameraController.SetInputLocked`（不轉視角），結束前先 `PlayerController.ClearLockTarget` 再用 `CameraController.ResetBehind` 把自由視角擺到玩家背後。場景卸載（返回休息區、重試、離開 Play）時 PlayableDirector 被銷毀也會送 `stopped`，CutsceneDirector 看 `gameObject.scene.isLoaded` 不把它當成演出結束（不收尾、不送 `OnCutsceneFinished`：玩家可能已經先被銷毀，Boss 也不該在卸載中開打）；所以「看過開場／轉階段」由 BattleManager 在開始播時就記，中途離開下次照樣可以跳過。
- 鎖定（中鍵）由 PlayerController 負責：鎖定清單 `lockTargets` 由 PlayerSpawner 生成時交給玩家（PlayerSpawnerBuilder.Wire 收集場景裡所有的 LockOnTarget），結果用 `OnLockTargetChanged` 送出，鏡頭與 Debug 面板訂閱。
- Timeline 自訂片段要算進度時用片段本身的長度（Track 在 `CreatePlayable` 填進 behaviour 的 `clipDuration`），不要用 `playable.GetDuration()`：片段設成 Hold 延伸時，playable 的長度會包含延伸的部分（約 100 萬秒）。
- Debug 程式整個檔案包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS`。`BOSSFIGHT_DEV_TOOLS` 是 Player Settings > Standalone 的 Scripting Define Symbols（T15 起加上：alpha 測試版要能用秘技打開開發者模式）；正式版要拿掉 Debug 程式時，從 define 刪掉它即可。Debug 程式裡用到 UnityEditor 的部分另外包 `#if UNITY_EDITOR`（player 編譯沒有 UNITY_EDITOR）。
- Layer：`ArenaWall`（8）用於外牆、入口門與門柱，Boss 撞牆判定用（大樹守衛本體用 Default，不放這層）；`Hurtbox`（9）是受擊範圍的 trigger，Hitbox 只掃這一層。新增 Layer 時記在這裡。
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
- 轉階段（T7）：Boss 血量過門檻時自己進入 Transition（無敵、打斷招式、清 Hazard）；BattleManager 播 TransitionCutscene，演出結束時呼叫 `BossController.CompleteTransition` 套用下一階段。有接演出時 BattleManager 會關掉 `BossController.AutoCompleteTransition`；沒接演出時才照 `BossPhaseData.transitionTime` 計時自動結束。演出由 TransitionCutsceneBuilder（Tools > Cutscene > Build Transition／Reset Transition Timeline）建立，鏡頭在執行時由 CutsceneStage 依玩家位置整組轉向；狗離貓不到 6 m 時第 1 步先退到 6 m（特寫才不會穿模），第 5 步推到 `ArenaData.transitionPushRadius`（10 m）。期間不另外設玩家無敵。T14 起轉階段也能跳過：同一次執行看過一次（`BattleSession.TransitionSeen`，BattleManager 在演出開始播時記，跟開場一樣）之後，`CutsceneDirector.Play(true)`；transition 也接 IntroCutsceneSettings（只用 skipHoldTime）。轉階段 Timeline 沒有 SkipTarget 標記，跳過時 CutsceneDirector 在結尾前 0.001 秒 Evaluate 一次再 Pause，下一幀的 LateUpdate 才 Stop（玩家要在輸入還鎖著時照 ScriptedMoveTo 走到被推開的位置）；所以音樂「Restart」與狗的「Pushed Out」兩個片段設成 Hold（TransitionCutsceneBuilder 對已存在的 Timeline 也會補設），其他效果本來就是 Hold 或在結尾歸零。跳過會一幀推開 2 m 以上，所以 `PlayerController.SetInputLocked(false)` 在照腳本走過時會清掉移動速度（不然解除後會滑到牆邊）。
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
  - 開發者模式：DeveloperMode 依 GameOptions 開關這個場景的 Debug 工具（BossDebugTools、BalanceDebugPanel、PlayerDebugOverlay，序章再加 PrologueDebugTools；關掉時清掉 F2 無敵、F3 指定招、F11 小樹精無敵；F12 已改的數值留著）；選單開著時也先停用（IMGUI 面板會蓋在選單上，開著的 F12 面板會被關掉）。沒有 Debug 程式的 build（沒有 `BOSSFIGHT_DEV_TOOLS` 的正式 build）選單不顯示這一項。T15 起預設是關，而且開關本身也藏起來（見下方 T15）。
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
  - 休息區 `Hub.unity` 由 HubBuilder（Tools > Hub > Build Hub）整個重建（T20 起的內容見下方「休息區場景」），也把 Build Settings 設成 Boot 0、Hub 1、BossArena 2、Prologue 3（`HubBuilder.SetBuildScenes`；Boot 場景是 Tools > Progression > Build Boot Scene）。
  - SceneFlow（SceneFlowBuilder，Tools > Progression > Build Scene Flow；PauseMenuBuilder.Wire 最後也會跑）：黑幕淡出 0.2 秒、淡入 0.2 秒（SceneFlowData），一幀最多推進 1/30 秒（載入後頭幾幀會卡約 0.45 秒，不夾的話淡入整段被吃掉）。ESC 選單的返回休息區與重置存檔在淡出期間維持暫停，全黑時 SceneFlow 才解除暫停、換場景。直接按 Play 與重試不淡入。換場景進來時由 PlayerSpawner 直接把玩家生成在出生點（T18），SceneFlow 只負責淡入。
  - 擊敗紀錄：Boss 一倒下 BattleManager 就記進存檔（F6 也算）；T24 起改成 `ProgressRecorder.RecordBossDefeat(BattleSettings.boss)`（BossDefinition，id `oiia_cat`）並發首通獎勵。
  - 勝敗畫面：重試與「返回休息區」左右並排（x ±200、y −170），返回休息區不先確認；ESC 主選單在重新挑戰下面加「返回休息區」（先確認）；設定頁的「重置存檔」只在開發者模式開著時顯示（開關下面），確定後清空存檔、從序章重新開始（`SceneFlow.ResetProfile` 載入 `SceneFlowData.prologueScene`，不看「略過序章」）。
  - 背景驗收：傳送門是 `InputSystem.QueueStateEvent` 模擬 E 鍵後反射呼叫 `Portal.Update`；選單、勝敗按鈕用反射呼叫 onClick；黑幕用 `Application.onBeforeRender` 每幀記 alpha。實際按 E、Esc 要在前景手動試。
- 角色 prefab 化與生成接線（T18）：
  - 刀盾狗 prefab 在 `Assets/Prefabs/Characters/DaoDunDog.prefab`（DaoDunDogBuilder 第一次跑時用 `AssetDatabase.MoveAsset` 從 `Assets/Prefabs` 搬過來，GUID 不變），根物件掛 PlayerRig；DaoDunDogBuilder 不再把角色放進場景，改建 PlayerSpawner 與預覽模型（`PlayerSpawnerBuilder.Build`，沿用舊 spawner 的位置與出生點清單）再建鏡頭。
  - BossArena 從頭重建的順序：Build DaoDun Dog（PlayerSpawnerBuilder.Build 會先跑 `ProgressionBuilder.EnsureAssets` 建好 GameDatabase 與刀盾狗的定義）→ Build Boss Placeholder → Build Intro（會接著重建轉階段、戰鬥 UI、ESC 選單、SceneFlow、打擊回饋，每一步最後都重跑 `PlayerSpawnerBuilder.Wire`）；休息區跑 Build Hub。
  - T19 起 spawner 接的是 GameDatabase，依存檔的目前角色（`ProfileStore.CurrentCharacter`，空的或找不到時用名冊第一個）生成；`ReplaceCharacter(CharacterDefinition)` 已經能用（在目前位置生成新角色、銷毀舊的、套用數值、重新 BindPlayer），選單介面是 T21。
  - 背景驗收：Hub↔BossArena 用反射呼叫 `SceneFlow.GoToBattle`／`BattleManager.ReturnToHub`；開場跳過、鎖定、處決用反射呼叫 `CutsceneDirector.Skip`、`PlayerController.SetLockTarget`、`TryStartExecution`；換角色在 Hub 與 BossArena 各用反射呼叫一次 `ReplaceCharacter`。DeveloperMode 開場時會清掉 F2 無敵，背景驗收要等一兩秒再設 `ForcedInvulnerable`。Timeline 編輯模式預覽用 `capture_game_view --source camera` 截（Unity 在背景時 Game 視窗不重畫）。
- 數值管線（T19）背景驗收：測試修正開關用反射設 BalanceDebugPanel 的 static `testModifiersOn` 再呼叫 `ApplyTestModifiers(true)`；實際跑速是 `InputSystem.QueueStateEvent` 按住 D（不要手動 `InputSystem.Update()`，否則按鈕類動作的 WasPressedThisFrame 會被吃掉），用 `Application.onBeforeRender` 記錄最大 `PlanarSpeed`（關 5.000、開 6.000）；F12 面板用 `capture_game_view --save_path` 截圖。Boss 在背景會一直打玩家，量血量前先設 `ForcedInvulnerable`。
- 休息區場景（T20，`docs/hub-progression.md`「休息區」；選項是計畫確認過的預設）：
  - HubBuilder 照 `ScriptableObjects/Hub/HubLayout.asset` 重建：邊界折線（看不到草圖，照企劃書暫定尺寸：北邊 36 m 寬的主區、往南收窄成寬 6 m 的小路，傳送門在 (−2, −24)；照草圖調整就改 `boundary` 的點）上種第一排樹（只往外偏移），往外再種 2 排；第一排後面 `wallOffset` 一圈 BoxCollider 隱形牆（玩家可以從樹幹之間鑽到牆前，但出不去）。樹幹有碰撞，佔位模型裡會擋路的部分（小屋、村長、鐵砧與棚柱、爐子、營火堆、傳送門的柱子與光面）有碰撞，小路、圓木、石頭沒有。網格在 `Models/Hub/Meshes`（圓錐樹冠、三角屋頂，每次覆寫），材質在 `Models/Hub/Materials`（只在不存在時建立）。HubBuilder 一開始就開新場景再載入資產（`NewScene` 會卸載先載入、沒被場景引用的資產，引用會變成 null）。
  - 天色：HubAtmosphere 在 Awake 套用 HubAtmosphere.asset（天空盒 `Hub_Sky`、低角度暖色太陽、Trilight 環境光、指數平方薄霧）；營火點光源由 FireFlicker 閃爍、不投陰影（點光源陰影會擠爆陰影圖集）；鐵匠鋪爐火是固定點光源。
  - 出生點：啟動在營火旁 `Start`（`startSpawn`）、從 Boss 戰回來在傳送門前 `Portal`（`portalSpawnDistance` 5 m，面向北）。休息區不建 HUD。
  - 互動：InteractionScanner 每幀找水平距離在 `interactRadius` 內最近的站點（清單由 HubBuilder 接），InteractionPromptUI 在畫面下方顯示「按 [E] 對話／鍛造／休息／出發」（鍵名 `BindingText.FirstDisplay("Player/Interact", …)`、依 InputHintDevice 換鍵鼠或手把）；HubManager 收到 Interact（站在地上才行）打開站點：站點鏡頭優先度 0 → 20、玩家照腳本以 `approachSpeed` 走到站位點並面向站點（使用者決定：越肩與雙人鏡頭才對得準；T21 坐下也用這個點）、LateUpdate 鎖玩家與鏡頭並放開游標（ESC 選單暫停的鎖定也是 HubManager 處理）。四個站點都有站點鏡頭（傳送門那台是使用者決定加的），主體擺在畫面左半。
  - 站點面板（HubStationUI，畫面右邊）：T21–T24 營火、鐵匠鋪、村長都換成自己的介面，現在只剩傳送門的挑戰清單（T24 起照 GameDatabase.bosses 一隻一列、擊敗過的加「（已擊敗）」、下方是選到的 Boss 的說明）→ 確認 → `Portal.Depart(boss)`。返回讀 `UI/Cancel`：確認框開著時回清單，否則 `HubManager.CloseStation`（鏡頭還原、自由視角擺到玩家背後面向站點）。站點開著時（含關掉的那一幀）PauseMenuUI 不開選單，所以 Esc 只關介面。
  - ESC 選單：PauseMenuBuilder 在場景沒有 BattleManager、有 HubManager 時建休息區版（主選單只有繼續、設定、退出遊戲，不建難度頁；PauseMenuUI 的難度、重新挑戰、返回休息區欄位留空），設定頁照舊。BossArena 的 ESC 選單 T20 重建過一次（改鍵頁多了「互動」一列）。
  - 背景驗收：按 E、Esc 用 `InputSystem.QueueStateEvent` 模擬鍵盤（按住約 0.4 秒再放開；MCP 的 `simulate_key` 送不進 Input Action），手把用 `InputSystem.AddDevice<Gamepad>` 檢查提示換成 Y；邊界是按住 W／A／S／D 各十秒左右確認停在牆內；傳送門出發與回來用反射呼叫面板按鈕的 onClick、`BattleManager.ReturnToHub`。鏡頭構圖用 `capture_game_view --save_path` 截圖確認。
- 營火與角色切換（T21，`docs/hub-progression.md`「角色系統」；選項是計畫確認過的預設）：
  - 站點的掛鉤：HubManager 打開介面時呼叫 `Interactable.Opened(rig)`、開著時每幀 `Tick(rig, arrived)`（arrived = 這一幀走到站位點）、關掉時 `Closed(rig)`；T22–T24 的站點子類別也用這三個。
  - 坐下：`Campfire` 在 arrived 後呼叫 `DaoDunDogAnimator.SetSitting(true)`（模型抬高 `sitHeight`、雙腿往前抬、手放膝上，數值在元件的「坐下」欄位），關掉時站起來。HubBuilder 把營火玩家這一邊的圓木 `SeatLog_Front` 橫放在站位點正下方（使用者決定：坐在圓木上；沒有碰撞）。鏡頭沿用 T20 的營火站點鏡頭（本來就是隔著火的近景）。
  - 已解鎖角色（使用者決定）：`CharacterDefinition.unlockedByDefault` 或存檔的 `unlockedCharacters`（`ProfileStore.IsCharacterUnlocked`／`UnlockCharacter`，之後的解鎖角色獎勵用），`CharacterDefinition.IsUnlocked` 合併兩者。刀盾狗、測試狗都是 unlockedByDefault。
  - 角色清單（Campfire）：照名冊順序列出解鎖過的角色，`developerOnly` 的只在開發者模式開著時列（沒有 Debug 程式的正式 build 一律不列）；目前角色一定列出（使用者決定：開發者模式關掉時，存檔是測試狗就照樣生成測試狗，清單裡也看得到，只是切走後就不能切回來）。開著時切換開發者模式，清單立刻更新（訂閱 GameOptions）。
  - 切換（使用者決定：點了就換，不確認）：`ProfileStore.SetCurrentCharacter` → `PlayerSpawner.ReplaceCharacter`（原位換人，HubManager 重新鎖住新角色），新角色直接坐好（`SetSitting(true, true)`），換的那一刻在角色身上放 `Prefabs/Hazards/Fx_HitSpark`（佔位的煙，Campfire 的視覺欄位）。BossArena 與重開遊戲照存檔生成，不用另外處理。
  - CampfireUI（HubUIBuilder 建，跟站點面板同位置）：「角色」「技能樹」頁籤（目前頁換底色 `tabActiveColor`），角色頁每列一個按鈕（樣板列複製、VerticalLayoutGroup 排），目前角色標「（目前）」換字色；技能樹頁見下方 T23。返回讀 `UI/Cancel` → `CloseStation`。HubStationUI 遇到營火不顯示。文字與顏色在 HubUIData 的「營火」。
  - 測試狗：`DaoDunDogBuilder.BuildTestVariant` 建刀盾狗的 prefab variant（只換材質：灰藍毛、暗紅盾面，`Models/DaoDunDog/Materials/TestDog_*` 只在不存在時建立）；Build DaoDun Dog 重建刀盾狗後會跟著重建 variant（子物件 fileID 會變，舊覆寫會斷；同路徑所以 GUID 不變）。ProgressionBuilder 建 `TestDog.asset`（id `test_dog`、developerOnly）與 `TestDogStats.asset`（建立時複製刀盾狗的數值，血量 80、跑速 6；之後在資產調），排在名冊第 2 個。裝備欄、技能樹是 T22／T23。
  - 背景驗收：打開營火用反射呼叫 `HubManager.Open`（也用 `InputSystem.QueueStateEvent` 按 E、Esc 試過一次：站在互動半徑 3 m 內才開得起來），點列用按鈕的 `onClick.Invoke`；跑速是按住 D 用 `Application.onBeforeRender` 記最大 `PlanarSpeed`（測試狗 6.000）；HUD 在 BossArena 讀 PlayerHudUI 的 healthText（80 / 80）。換角色的煙只有 0.3 秒，MCP 往返太慢截不到。`capture_game_view --save_path` 是相對 Assets 的路徑（`Temp/x.png` 會存到 `Assets/Temp`），截完要刪掉。
- 裝備系統與鐵匠鋪（T22，`docs/hub-progression.md`「裝備系統」；選項是計畫確認過的預設）：
  - 資料：`EquipmentSlotType`（項圈、護符，只有顯示名稱）、`EquipmentData`（id、名稱、說明、圖示、來源提示 `sourceHint`、欄位種類、StatModifier、PlayerEffect、可裝備角色〔空 = 全部〕）、`CharacterDefinition.equipmentSlots`（刀盾狗：項圈、護符；測試狗：護符）。GameDatabase.equipment 列出所有裝備（鐵匠鋪照這個順序），Validator 也檢查裝備的欄位種類與角色欄位裡的空格。ProgressionBuilder 建資產（數值只在建立時寫入：項圈血量 +20、護符盾反回 5 HP）。
  - 套用：PlayerSpawner 生成與換角色時 `EquipmentLoadout.Register`，把那個角色的穿戴登記成 Loadout 的 `equipment` 來源（換角色時換成新角色自己的）；只算有效的（解鎖了、欄位種類相符、角色可穿），存檔本身不改。鐵匠鋪換裝後 `PlayerSpawner.ReapplyEquipment`（重新登記 → `ReapplyStats(true)` 補滿血量、耐力與骨頭）。
  - 換裝規則：點裝備穿進第一個種類相符的空欄位，都滿了就換掉第一格；穿著的再點卸下；點左邊的欄位也是卸下。角色穿不了的（沒有對應欄位）變淡、下方寫「測試狗沒有項圈欄，不能裝備。」；未解鎖的是黑色剪影加「？？？」，下方寫來源（「來源：擊敗 OIIA 貓」）。
  - 差異預覽（BlacksmithUI 下方）：選到（鍵盤、手把導覽，或滑鼠移上去，`EquipmentCellUI.OnFocused`）的那一格，「穿上後：」或「卸下後：」列出變動的數值「名稱　前 → 後」（綠變好、紅變差；翻滾耐力消耗是 `lowerIsBetter`）與加上（＋，綠）、拿掉（－，紅）的效果名稱（`PlayerEffect.displayName`）。數值名稱、格式、顏色、文字都在 HubUIData 的「鐵匠鋪」。
  - 「新」標記：`ProfileStore.UnlockEquipment` 會標新，關掉鐵匠鋪時全部清掉（打開過就算看過；使用者確認的時機）。
  - Debug「解鎖全部裝備」（使用者確認的位置）：鐵匠鋪面板右上角的按鈕，只在開發者模式開著時顯示（沒有 Debug 程式的正式 build 沒有），F12 面板只在 BossArena 所以不放那裡。解鎖的會標「新」。T23 的「給 10 技能點」照這樣放在技能樹頁。
  - 面板（HubUIBuilder 建）：980×760，在畫面右邊（比站點面板大），左邊欄位（VerticalLayoutGroup）、右邊裝備格子（GridLayoutGroup 一列 4 格）、下方說明框；HubStationUI 遇到鐵匠鋪不顯示。改了 HubBuilder 的站點或 HubUIBuilder 要重跑 Build Hub。
  - 背景驗收：打開鐵匠鋪用反射呼叫 `HubManager.Open`，點格子用 `Button.onClick.Invoke`、選取用 `EventSystem.SetSelectedGameObject`（會觸發預覽）；盾反回血在 BossArena 用反射把 PlayerController 的 `IsBlocking` 設 true、`blockStartTime` 設成現在，再對 PlayerHealth 呼叫 `TakeDamage`（正面、canParry），結果 Parried、血量 100 → 105（沒有在背景用 Boss 的實際攻擊盾反：時機對不準）。跑 Builder 前先存場景（場景有未存的變更時 HubBuilder 會跳「場景已變更」對話框，Unity 主執行緒被擋住，CLI 只會回 Main thread operation timed out）。
- 技能樹（T23，`docs/hub-progression.md`「技能樹」；選項是計畫確認過的預設）：
  - 資料：`SkillTreeData`（每個角色一棵，`CharacterDefinition.skillTree`，可留空 → 技能樹頁顯示「○○沒有技能樹」）的節點 `SkillNode`：id、名稱、說明、圖示、花費、前置（同一棵樹的節點 id，全部學會才能學）、StatModifier、PlayerEffect、`gridPosition`（x = 欄、y = 列，0 在最上面）。ProgressionBuilder 在樹是空的時候寫入節點（之後在資產調）：刀盾狗照企劃書 5 個節點共 7 點（左欄 堅韌 → 鐵壁 → 盾反專精，右欄 輕盈 → 骨頭收藏家）；測試狗是計畫自訂的小樹：疾風（跑速 ×1.1，1 點）→ 厚毛（最大血量 +15，1 點）。Validator 檢查每棵樹的節點 id（空、重複）、前置（不存在、自己、成環）與格子座標重複；節點 id 也算進「檢查通過：N 個 id」。
  - 規則（`SkillTreeLoadout`）：狀態是 已學／可學／前置不足／點數不足；學會扣池子的點數。重置（營火免費）清掉目前角色學過的節點，退回的是存檔記的 `spent`（學的時候實際付的點數，之後改資產的花費也不會多退少退）。點數池全帳號共用、已學節點各角色分開記。
  - 介面（CampfireUI 技能樹頁）：T23 起整個營火面板跟鐵匠鋪一樣 980×760（角色頁也跟著變大）。上方一排是「重置」、開發者模式的「給 10 技能點（Debug）」、右上剩餘點數；左邊節點依 `gridPosition` 排（大小與間距 `HubUIData.skillNodeSize`／`skillNodeSpacing`，整組水平置中），前置連線是旋轉的細 Image、只畫在兩格的邊之間（前置學過時亮）；節點學過亮（填 `skillLearnedColor`）、可學有外框、不能學整格半透明（點了照樣看說明）。右邊說明框：名稱、狀態（需要先學○○／技能點不足，紅字）、說明、「學習後：」數值差異（`Campfire.PreviewNode`，含裝備的值）與「學習」按鈕（使用者決定：按下就學，不另外確認；不能學時停用）。「重置」先跳不透明的確認框（退回幾點），返回鍵先關確認框。節點只在被選到（點擊、導覽）時更新說明，不像鐵匠鋪滑鼠移上去就選（從節點移到「學習」的路上會經過別的節點）。圖示欄位有接才顯示，目前沒有佔位圖示。文字與顏色在 HubUIData 的「技能樹」。
  - Debug「給 10 技能點」：`Campfire.debugSkillPoints`，只在開發者模式開著時顯示（沒有 Debug 程式的正式 build 沒有）；實際發點數（首通、任務）是 T24，用 `ProfileStore.AddSkillPoints`。
  - 背景驗收：打開營火用反射呼叫 `HubManager.Open`，切頁、學習、重置、確認框都是按鈕的 `onClick.Invoke`，選節點用 `EventSystem.SetSelectedGameObject`；骨頭上限在 BossArena 讀 PlayerHudUI 的 `bones` 長度（4）。改了 CampfireUI 這類腳本時不要在 Play 模式中讓 Unity 重新編譯：重載後沒序列化的欄位會變 null（BossTelegraphFlash 每幀丟 ArgumentNullException），要先停止 Play。
- 村長任務與首通獎勵（T24，`docs/hub-progression.md`「村長與任務」「獎勵」；任務名稱「轉暈那隻小笨貓！」與「接受／再想想」是使用者決定的，其他是計畫確認過的預設）：
  - 獎勵：`RewardData`（技能點 ×amount／解鎖裝備／解鎖角色）由 `ProgressRecorder.Grant` 發：技能點走 `ProfileStore.AddSkillPoints`、裝備走 `UnlockEquipment`（會標「新」）、角色走 `UnlockCharacter`。OIIA 貓首通：技能點 ×3、厚皮項圈；任務：技能點 ×2、反擊護符（只在 ProgressionBuilder 建立資產時寫入，之後在資產調）。
  - 首通：BattleManager 在 Boss 倒下時呼叫 `ProgressRecorder.RecordBossDefeat`，回傳這次發的獎勵放進 `BattleResult.firstClearRewards`；BattleResultUI 在勝利那一組按鈕下面開「首次擊敗獎勵」一區（BattleUIBuilder 建的 `Victory/FirstClear`，文字、顏色、高度在 BattleUIData），不是首通或失敗都不顯示。
  - 任務狀態（`QuestState`）由存檔算：已完成 → 已接且目標達成（存檔擊敗過那隻 Boss，接任務前就打贏也算）是可回報、否則進行中 → 沒接時看過開場劇情且前置任務都完成是可接、否則未開放。村長照 GameDatabase.quests 的順序挑：可回報 ＞ 可接 ＞ 進行中 ＞ 閒聊（HubStoryData.idleLines）。回報時一打開就先發獎勵並存檔（中途 Esc 也不會漏），再講回報對白，最後補一句「（獲得：…）」；可接的講完顯示任務名稱、說明、獎勵與「接受／再想想」，接受後村長再講 `questAcceptedFormat` 與任務的 `acceptedLines`（傳送門在哪裡）；再想想或在選項時按 Esc 先講 `HubStoryData.declineLines` 再關，維持可接（序章修正 I）。
  - 頭上的標記：有可回報「?」、有可接「!」（VillageChief.OnMarkerChanged，Start 時也送一次）；QuestMarkerUI 用 Main Camera 把村長的 `MarkerAnchor`（頭上約 2.95 m）投到畫面上浮動，站點介面開著時藏起來。
  - 開場劇情：存檔沒看過（`storyIntroSeen`）時，HubManager 在 Start 後 `HubStoryData.introDelay` 秒（期間已鎖輸入）呼叫 `VillageChief.BeginIntro`，再照一般站點打開村長；村長直線走到玩家面前（不避障，穿過東西），走的路上 `Interactable.ReadyToTalk` 是 false：HubManager 先不切站點鏡頭、自由視角擺到玩家背後看著村長、玩家站著面向他。走到後村長的站位點剛好在玩家腳下，他的站點鏡頭（子物件）就是雙人鏡頭；講完開場對白（關掉時才記看過）開放第一個任務，村長走回原位（路上也能找他講話，講話時停下）。從營火旁的 Start 出生時，村長會站在營火的圓木旁邊，身體跟圓木有一點重疊（佔位，之後要的話調走過來的方向）。
  - 對話框（DialogueUI，HubUIBuilder 建在畫面下方，大小、位置、逐字速度在 HubUIData 的「村長的對話框」）：前進讀 UI/Click、UI/Submit（Enter、手把 A）與 Player/Interact（E、手把 Y），字還在跑時先顯示全文；對話開始那一幀的按鍵不算。返回讀 UI/Cancel。
  - 背景驗收：刪掉 `save.json` 後進 Hub 的 Play 模式看開場；E、Enter 用 `InputSystem.QueueStateEvent` 模擬（`Teleport` 到村長或傳送門的 `StandPoint` 再按 E），傳送門確認框的「出發」用 `onClick.Invoke`，Boss 用 `BossHealth.DebugSetHealth(0)`（F6 的做法）、`BattleManager.Retry`／`ReturnToHub` 用反射。Input System 的 `editorInputBehaviorInPlayMode` 是 PointersAndKeyboardsRespectGameViewFocus：Game 視窗失去焦點後（換過幾次場景之後發生過）模擬的鍵盤事件就不再觸發任何 Action（`Keyboard.current` 的狀態有變），這時改用反射呼叫（例如 PauseMenuUI 的 `Open`／`Close`）。
- 測試木樁 `TrainingDummy` 已經在 T3 從場景移除（程式與 `TrainingDummyData.asset` 還在 `Scripts/Debug`、`ScriptableObjects/Debug`）。要練格擋或盾反時用 Tools > Debug > Build Training Dummy 重建，它會自己加進 PlayerSpawner 的鎖定清單；正式 build 前要刪掉物件，否則會變成 Missing Script。

## 戰鬥預兆與互動回饋（T26 驗收補充，Oct 27）

- 後續所有 Boss／戰鬥敵人的攻擊都沿用 OIIA 貓的紅白分類：白色預兆可格擋（能否盾反依招式資料）；紅色預兆不可格擋、不可盾反，提示玩家閃避。角色外觀、種子本體或場地氣氛的顏色不代表攻擊分類。
- 預兆閃光、地面範圍與填充進度、預兆音效、實際 `DamageInfo.canBlock`／`canParry` 必須使用同一段攻擊的設定，提示填滿對齊判定開啟。多段招式逐段處理；中途改成不可格擋時，該段同步改紅光與音效，不能只看整招的初始設定。招式取消、頭暈或清場立即收掉未完成預警。
- 玩家與 Boss 的攻擊、命中、受擊、格擋、盾反及成功閃避，每一類都必須有對應的可見特效與音效；佔位資產也必須實際接線，不能只有空欄位或單純 Log。優先重用 OIIA 的 `PlayerFeedback`、`BossHitFeedback`、`BossDamageFeedback`、`BossImpactFeedback`、`FeedbackEmitter` 與 `CombatFeedbackData`。
- 格擋沿用藍色、盾反沿用金色、成功閃避沿用綠色回饋。空滾與真正避開攻擊分開：只在有效攻擊窗內受到威脅且成功避開時播放成功回饋／計教學次數，同次攻擊或同次翻滾避免重複播放，不把距離外空滾或強制無敵冒充成功。
- 回饋以事件與實際 `HitReport`／攻擊結算結果驅動；VFX／SFX 不反向決定傷害或教學完成。新增玩法參數仍放 SO，保留既有盾反停頓規則，不因新增回饋自行添加其他畫面停頓或改戰鬥數值。
- 小樹精的 `SaplingMode.Attack` 是白色格擋訓練，`SaplingMode.DodgeAttack` 是紅色閃避訓練；T27 必須依步驟切換模式。T28／T29 新招、衍生危害物件與召喚物也須遵守上述分類與 VFX／SFX 驗收。

## 序章修正 A–C 之後的規則（Oct 29，`docs/prologue-fix-list.md`）

- 成功閃避只在被打方結算一次：`Core/IDodgeable`（玩家的 `PlayerHealth` 實作）。翻滾無敵中被打到（Evaded）由 PlayerHealth 自己算；攻擊方只在「目標在範圍裡翻滾（`Hurtbox.Dodger.IsDodging`）、判定窗結束沒打中」時呼叫 `NotifyNearMiss`。強制無敵、受擊後無敵、倒下不算，同一次翻滾只送一次 `PlayerHealth.OnDodgeSucceeded`；綠色火花、閃避聲（PlayerFeedback）與教學計次（TutorialDirector）都訂閱它。攻擊方不再自己判斷無敵種類，也不經上層灌翻滾狀態（`BossController.TargetRolling`／`TargetDodgeEligible`、`TrainingSapling.SetTargetRolling` 已刪）。
- 命中火花（`BossHitFeedback`）只認 `Boss/IHitReportSource`（`OnHitReported`）：BossController、TrainingSapling、DaisyMentor 都實作，欄位名稱沿用 `boss`（OIIA 的 prefab 與 BossBuilder 不用改）。BossHitFeedback 不畫 Evaded（綠色在玩家那邊）。新的攻擊角色實作這個介面，照 `PrologueActorsBuilder.WireHitFeedback` 複製 OIIA 的設定，不另開特例。
- 玩家的可見回饋都在角色身上的 PlayerFeedback（三個場景、換角色都一樣）：刀光 `Fx_DogSlash`／`Fx_DogSlashHeavy`（HazardBuilder.BuildExtraFx 建，輕攻擊每段換傾斜與大小、重擊與處決由上往下）、成功閃避的綠色火花；CombatFeedbackBuilder 接到角色 prefab。不要在場景物件上補角色的表現。
- `LockOnTarget` 自帶構圖：`aimPoint`（鏡頭看的點）、`markerPoint`（鎖定菱形與處決標記，空的用 aimPoint）、`framingRadius`、`lockOnPitch`（預設 12°）、`lockOnDistance`（0 = 鏡頭原本的距離）。CameraController 鎖定時平滑轉到目標的俯仰與距離，解除時自由視角回到鎖定前的俯仰。大樹守衛的值在 `GuardianBattleData`（看樹幹中段、標記在樹洞），小樹精的在 `TrainingSaplingData`（看身體中心、32°），由 `GuardianBuilder.WireLockFraming`／`PrologueActorsBuilder.WireLockFraming` 寫入。不要在鏡頭或場景路徑上寫特例。
- 大樹守衛的地面預兆用共用的 `GroundTelegraph`（`Prefabs/Hazards/GroundTelegraph`）：AreaStrike 執行時複製幾份當主範圍、環波前方預警帶（`ShowLead`）與安全圈；紅、白、安全區綠讀 `TelegraphPaletteData`（見下方 D＋F）。填色一律由攻擊源往外推，填滿那一刻開判定；範圍裁切在場地內。
- 多段招在招式預兆開始時就把每一段畫出來、依序填滿，段與段照 `interval`；只有每段開預兆時才鎖玩家或重新瞄準的段（藤蔓連砸最後一條、樹根突刺／追獵）邊打邊畫，預兆長度用 `GuardianMoveData.warning`，不得低於該階段的預兆下限（一階段 0.4、二階段 0.3 秒）。不要拿段與段的間隔充當預兆。
- 守衛招式的衝擊音效照表現樣式：`BossImpactKind.VineStrike`／`LeafBlade`／`RootBurst`／`SeedBlast`／`Summon`（`CombatFeedbackData` 同名欄位，PrologueAudioBuilder 合成佔位音，CombatFeedbackBuilder 一起填）。新招要有自己的聲音就加 BossImpactKind，不要借 OIIA 的。
- 萬根齊發的安全圈找不到時逐步放寬（可走到 → 整個場地 → 不檢查路徑 → 玩家位置），一定會放出、一定會結束，只留 Warning，不再 `SuspendCombat`。
- 小樹精預兆開始後 `TrainingSaplingData.telegraphTrackTime`（0.5 秒）還會轉向，之後鎖住方向，判定照鎖住的方向打。
- 舊的 `GuardianTelegraph.shader`、`AreaTelegraph.mat`、`AreaQuad.asset`、`Battle/TutorialPlayerVisual`、`PrologueLayoutData.lockOnPitch`、`TutorialBuilder.ApplyLockOnCamera` 已刪。

## 序章修正 D＋F 之後的規則（Oct 29，`docs/prologue-fix-list.md`）

- 預兆分類色只有一份：`Core/TelegraphPaletteData`（`ScriptableObjects/Feedback/TelegraphPalette.asset`；地面的白、紅、安全區綠，模型閃光的白、紅）。GroundTelegraph、BossTelegraphFlash、TrainingSaplingVisual、TrainingDummy 都只引用它，不自己存顏色；新的預兆元件也接這份（`CombatFeedbackBuilder.EnsureTelegraphPalette`／`WireTelegraphPalette`）。
- 同一個值只存一份：守衛的名字與轉階段台詞在 `GuardianBattleData`（guardianName、phase2Line），菊花的名字在 `TutorialData.speaker`，轉階段推開的距離在 `PrologueCutsceneData.pushRadius`；演出的視覺值（暗紅天色的飽和度與濾色、景深、樹冠枯萎、衝擊波厚度）都在 `PrologueCutsceneData`，`Cutscene/PrologueCinematicStage` 不寫死。
- 序章只走正式演出：教學結尾鑽回 → 登場 Timeline → `GuardianPhaseController.StartBattle`；活過萬根齊發 → `GuardianPhaseTwo.OnTransitionPresentationRequested` → 轉階段 Timeline → `CompleteTransition`；結局由 `PrologueOutcome` 要求、`PrologueCutsceneFlow` 播完才提交。T29 的暫代轉場、暫代勝利、`startAfterTutorial` 與 `ProfileStore.MarkPrologueDone` 已刪。
- Timeline 的提示點是 enum：`Cutscene/CutsceneCueMarker.kind`（`CutsceneCue`，往後加、不要插在中間），`CutsceneDirector.OnCue`／`JumpToCue` 都用 enum，不比字串。
- 輸入鎖仲裁：`Progression/InputLockArbiter`（原 PrologueSession）。休息區與序章的每個流程用自己當 owner 上鎖、放開游標，暫停與換場景由它自己鎖；HubManager 只走這一條路。BossArena 仍由 BattleManager 自己上鎖（D7 只改休息區與序章，使用者決定）。開發者模式的「略過序章」在序章裡由 SceneFlow（`prologueSkippable`）處理，開機時由 Boot 的 BootRouter 處理。
- 開機分流：`Progression/BootRouter`（Boot 場景）。Hub 不再有 startupContents／備用黑幕相機，`ProfileStore.TakeLaunchRouting` 已刪。
- `PlayerRig.HurtCollider`／`BodyRadius` 給上層交給 Boss（`BossController.TargetHurtbox`／`TargetBodyRadius`），不要從玩家身上 GetComponentInChildren。
- 種子炸彈是可格擋、不可盾反（使用者決定，`SeedBomb.asset` 的 canParry 0）。
- 序章 UI：自損白段是 Builder 建好的 `ResourceBar.loss`（`BattleUIBuilder.AddSelfCostSegment`，顏色 `BattleUIData.selfCostColor`）；守衛血條滑入沿用 BattleUI 的緩動與 `BattleUIData.bossBarSlideTime`／`bossBarSlideDistance`（守衛的 BattleUIData 是 0.5 秒、180）；序章勝利畫面只有一顆按鈕時放在 `BattleUIData.singleButtonX`。
- 全力外觀（`GuardianFullPowerVisual`）由上層呼叫 `SetFullPower`（轉階段的 Power cue、`GuardianPhaseTwo.CompleteTransition`），裂縫的底色與 Emission 一起換；自損播 `CombatFeedbackData.barkPeel`。
- 開發者工具（F）：
  - Battle 層的 `IBattleDebugTarget` 是 Debug 工具認得的唯一戰鬥流程入口：BattleManager（OIIA）與 PrologueCutsceneFlow（大樹守衛）各自實作。BossDebugTools 與 BalanceDebugPanel 的 `battle` 欄位接它；Debug 程式不直接呼叫 Retry、DebugSetHealth 這類流程。
  - 每個 Boss 戰場景一個 `DebugTools` 物件、一個 DeveloperMode，由 `Editor/DebugToolsBuilder`（Tools > Debug > Wire Debug Tools）建立與接線；GuardianBuilder、GuardianPhaseTwoBuilder、TutorialBuilder、PrologueActorsBuilder 結束時會跑，BossArena 由 PauseMenuBuilder.Wire 呼叫 `WireDeveloperMode`。不要在各 Builder 自己建 DeveloperMode 或 Debug 工具。
  - Debug 按鍵直接讀 `Keyboard.current`，不放進 Input Actions（`Player` map 裡的 Debug 動作已刪）。按鍵表（兩場戰鬥意義相同）：

    | 按鍵 | OIIA 貓（BossArena） | 大樹守衛（Prologue） |
    | --- | --- | --- |
    | F1 下一階段 | Boss 血量設成下一階段門檻 + 1 | 還沒開戰：略過教學與登場直接開戰；一階段：放萬根齊發（頭暈、處決中等結束）；萬根中：中止萬根、進轉階段演出 |
    | F2 玩家無敵 | 切換 | 切換（菊花藤蔓抽打照樣打得到） |
    | F3 指定下一招 | 依序切換 | 依序切換，萬根齊發也在清單裡（走一階段的萬根流程） |
    | F4 面板 | Boss 面板（右上） | Boss 面板（多階段計時、血量鎖、自損）＋序章面板（左邊玩家狀態下面：跳到教學第 N 步、直接完成教學、略過登場開戰、從守衛戰開始重來） |
    | F5 重新挑戰 | `BattleManager.Retry` | 重新載入序章（從教學開頭） |
    | F6 Boss 血量歸零 | 勝利 | 越過一階段血量鎖，接勝利演出 |
    | F7 玩家倒下 | 失敗 | 接擊倒演出 |
    | F8–F11 | — | F8 小樹精模式、F9 菊花抽打、F10 菊花巴掌、F11 小樹精無敵 |
    | F12 平衡面板 | 照舊 | 多守衛流程（Boss 頁）、守衛招式的段數／間隔／預兆（招式頁）、小樹精與菊花（教學頁）；每場紀錄記一階段、萬根齊發（活過沒有）、轉階段、二階段 |

  - 序章的 F12 面板經 InputLockArbiter 上鎖（`inputLock`），複本機制照「平衡面板」：`extraRoots` 是守衛流程與教學角色的根物件，GuardianBattleData／TrainingSaplingData／DaisyMentorData 也換成複本。「從守衛戰開始重來」用 PrologueDebugTools 的 static 旗標（Debug 例外，進 Play 時清空）。
  - 面板位置：Boss 面板右上、玩家狀態左上（10–340）、序章面板在左邊 350 以下；F12 開著時蓋在最上面。
- Builder 選單（D5）：Build 與 Validate 兩類，名稱不帶任務編號。序章從頭重建的順序：Tools > Prologue > Build Prologue（會接著跑 Build Teaching Actors → Build Tutorial →〔有二階段資產時〕Build Guardian → Build Guardian Phase 2 → 演出接回 → Wire Debug Tools）→ Tools > Prologue > Build Cutscenes（第一次建演出或改了演出時）；只改守衛就跑 Build Guardian（會接著補二階段、演出與 Debug 工具）。檢查跑 Tools > Prologue > Validate Prologue（只查接線，不查玩法數值）。Boot 場景是 Tools > Progression > Build Boot Scene。
- 背景驗收（D＋F）：F 鍵用反射呼叫 BossDebugTools 的 `NextPhase`／`CycleForcedMove`／`DefeatBoss`、BalanceDebugPanel 的 `SetOpen`、PrologueDebugTools 的 `RestartAtBattle`，教學跳步用 `TutorialDirector.DebugJumpToStep`／`DebugComplete`。守衛在背景幾秒就會打倒站著不動的玩家，開戰後先設 `ForcedInvulnerable`。**F6 勝利會寫進真的存檔**（OIIA 首通、序章結局），背景測試前先備份 `save.json`，測完還原。

## 序章修正 G＋H＋I 之後的規則（Oct 29，`docs/prologue-fix-list.md`）

- 教學的台詞、每句的動作與鏡頭都在資產：`TutorialLine` 有 `gesture`（菊花，`Boss/DaisyGesture`）、`saplingGesture`（小樹精，`SaplingGesture`）、`shot`（這句的鏡頭，None = 雙人鏡頭）、`after`（說完執行的動作）、`cueShot`（動作期間的鏡頭）；空白台詞不開對話框、只執行動作。`TutorialCue` 往後加：Demo（小樹精照這一步的紅／白示範甩一次，`TrainingSapling.Demonstrate`，不開判定、不計閃避）、ShowBars（切標靶模式讓量條出現）、Rumble（地鳴，`TutorialData.rumble`、`rumbleTime`）；GiveBone 改成 `DaisyMentor.TossBone`，骨頭飛到才給、送 `TutorialDirector.OnBonesGranted`（TutorialHudUI 叫 PlayerHudUI.FlashBones，只有序章教學會閃）。劇本初版在 `TutorialBuilder.ApplyScript`（資產建立時跑一次，之後在資產調）。
- 教學對白鏡頭：`Cutscene/TutorialStage`（Tutorial 底下兩台 CinemachineCamera 輪流，優先度 `TutorialStagingData.priority`）依 `TutorialStagingData` 的框架（原點 anchor、前方 frameFrom → frameTo）在 Show 那一刻擺好；開放操作時 Release 並 `CameraController.ResetBehind` 面向小樹精；鑽回交給登場 Timeline 前也 Release。新的鏡頭種類加在 `TutorialShot` 最後、資產補一列。
- 序章開場：`PrologueCutsceneFlow.Start` 先播 `PrologueOpening`（`PrologueCutsceneData.openingDuration`；MoundPeek cue 讓菊花 `Peek`：只有菊花 prefab 的土包鼓起、噴土，小花模型不出現），播完（或被中止）才 `TutorialDirector.StartApproach`。有接演出時 PrologueCutsceneBuilder 把 `TutorialDirector.autoStart` 關掉、`roarSource` 接守衛本體；沒接演出時教學在 Start 自己開始。守衛破土處的淡紅裂縫（`ForeshadowCracks`，試玩回饋起沒有直立樹根）由 PrologueCinematicStage 的 Rise 收掉。
- 守衛伏筆的聲音：地鳴與處決後的低吼由 Tutorial 物件上的 FeedbackEmitter 播（`TutorialData.rumble`／`distantRoar`／`boneToss`，佔位音 Tremor、Roar、Toss 由 TutorialBuilder 只填空欄位）。巴掌不加慢動作（使用者決定，不加盾反以外的畫面停頓），改用特寫與軀幹條閃白（TrainingSaplingHudUI）。
- 村長交接（I2，使用者決定）：開場（擊倒版／勝利版／沒有序章結果）講完，`VillageChief.CompleteIntroDialogue` 記看過並回傳可接的任務，HubDialogueUI 接著 `VillageChief.Offer` 講任務說明，講完問「接受／再想想」。三種開場的最後一句只負責引出任務，怪貓與雷雨的說明只在 `QuestData.offerLines`，不要在開場重講。互動提示依頭上標記換動詞（`HubUIData.chiefNewQuestVerb`／`chiefTurnInVerb`，InteractionPromptUI 訂閱 `VillageChief.OnMarkerChanged`）；村長在原位、有標記時轉身看 `HubStoryData.greetRadius` 內的玩家（VillageChief 是 IPlayerBinding）。回原位揮手延後到有模型。
- 鎖定標記 `LockOnMarkerUI.executionMarkers` 是清單，任一個處決標記顯示時藏起菱形（序章有小樹精與大樹守衛兩個）。Build Tutorial、Build Guardian 結尾都呼叫 `BattleUIBuilder.WireAllExecutionMarkers` 重接，哪個先重建都一樣（BossArena 的 Build Battle UI 只接自己那一個）；Validate Prologue 檢查數量與場景裡的處決標記相同。
- 背景驗收（G＋H＋I）：教學用 `EditorApplication.update` 掛一個驅動，反射呼叫 `BeginStory`、`AddProgress`，對白用 `DialogueSession.Advance`；休息區先備份 `save.json`，改成 `prologueDone: true`、`storyIntroSeen: false`、`prologueResult` 1／2，HubDialogueUI 的按鈕用 `onClick.Invoke`，重開村長用反射呼叫 `HubManager.Open`。驅動晚於對話開始掛上時要先 `DialogueSession.PublishState()`。**進 Play 結束時 ProfileStore 會寫回 `save.json`**，測完一定要還原備份。

## 序章試玩回饋之後的規則（Oct 29，`docs/prologue-fix-list.md` 的 J）

- 登場前藏著的角色不能露出地面：守衛模型在 Rise 之前是關著的，小花在 Hidden／Peek 時模型關著（Peek 只動土包）。伏筆物件只放貼地的東西，不放長得像角色部位的模型。
- 小樹精有實心碰撞（照 OIIA 貓：根物件的 kinematic Rigidbody＋CapsuleCollider，Default 層），大小在 `TrainingSaplingData.bodyRadius`／`bodyHeight`，由 PrologueActorsBuilder 寫入；`TrainingSapling.bodyCollider` 只在場上時開著（處決命中後要等 Defeat 才關）。重建小樹精 prefab 後要跑 Build Guardian Phase 2，召喚池小樹精的覆寫才會接回（Validate Prologue 會擋）。
- 環形攻擊可以分段：`AttackArea.segments`／`segmentPhase`（世界角度，0 = 北、順時針）／`segmentHalfWidth`，判定、`GroundTelegraph.SetSegments` 的地面預兆與 AttackAreaFx 的根刺共用同一組角度，看到的空隙就是打不到的空隙。樹根環障每圈的刺數、刺半徑、錯開比例在 `RootRingData`。
- 手寫 Shader Graph JSON 加節點時，節點 id 一定要加進 graph 的 `m_Nodes`：沒登記的節點連同它的連線會被丟掉，Custom Function 的那個輸入只會收到槽的預設值（C1 的 `Field`、`Hole0..2` 就是這樣一直是 0）。改完用 `ShaderGraphImporter.GetShaderText`（反射）看產生的程式碼，確認呼叫 Custom Function 時傳的是 `_Property_…` 而不是 `float4 (0, 0, 0, 0)`。

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
| T24 | 村長任務與首通獎勵 | 第一次進休息區村長自動講開場；之後村長頭上「!」；接「轉暈那隻小笨貓！」→ 打贏 OIIA 貓 → 勝利畫面顯示首通獎勵（技能點 ×3、厚皮項圈）→ 回休息區村長「?」→ 回報拿到技能點 ×2、反擊護符；第二次打贏不再給首通獎勵；鐵匠鋪新裝備標「新」 |
| T25 | 序章場景與開機分流 | 見 `docs/prologue.md` 的 T25 測試表；Boot 場景依存檔分流到序章或休息區 |
| T26 | 菊花老師與小樹精 | 見 `docs/prologue.md` 的 T26 測試表 |
| T27 | 六步教學流程 | 見 `docs/prologue.md` 的 T27 測試表與 `docs/prologue-fix-playtest.md` 的 G＋H＋I（開場演出、對話鏡頭、示範、伏筆） |
| T28 | 大樹守衛一階段與萬根齊發 | 見 `docs/prologue.md` 的 T28 測試表與 `docs/prologue-fix-playtest.md` 的 A–C |
| T29 | 二階段與勝利獎勵接口 | 見 `docs/prologue.md` 的 T29 測試表 |
| T30 | 演出與休息區交接 | 見 `docs/prologue.md` 的 T30 測試表；村長開場講完直接接任務（I2） |


## Skill 使用規則
- 打擊感、受擊回饋、畫面震動：參考 game-feel
- Boss AI、狀態機：參考 game-ai
- 鏡頭：參考 camera-systems
- 光效、後處理：參考 urp-postprocessing
- skill 建議與 docs/boss-oiia.md 衝突時，以企劃書為準
