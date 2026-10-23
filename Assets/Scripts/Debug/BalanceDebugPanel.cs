#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BossFight.DebugTools
{
    /// <summary>
    /// 平衡面板（F12，T10）：即時調整 Boss 血量、各招傷害／冷卻／預兆／破綻／權重、各階段節奏、失衡值、玩家耐力，並量這場的通關時間。
    /// <para>
    /// 不寫入 SO 資產：Awake 時把 Boss、玩家、鏡頭底下所有元件引用的 BossStats／BossPhaseData／BossMoveData／PlayerStats
    /// 換成記憶體裡的複本（Instantiate；BossStats.phases、階段的 entryMove 與開場序列也改指向複本，招式比對才不會錯），面板只改複本。
    /// 遊戲程式每次都從 SO 讀數值，所以改了立刻生效；冷卻在下一次招式結束時才用新值計算。
    /// 「存回 .asset」（只在 Editor，要按兩次確認）才把面板上的欄位寫回原本的資產並存檔。
    /// </para>
    /// <para>
    /// 調整值與歷次計時在重試（重新載入場景）後保留：複本與紀錄放在這個檔案的 static 欄位，進 Play 模式時清空、離開時銷毀複本。
    /// 這是 CLAUDE.md「唯一的 static 例外」之外的 Debug 例外，正式 build 不會有。
    /// </para>
    /// <para>
    /// 面板開著時（預設）暫停遊戲（timeScale 0）、鎖住玩家與鏡頭的輸入、放開游標；關掉時還原（開場、轉階段演出中或勝敗畫面時維持鎖定，交給原本的流程解除）。
    /// 計時用遊戲時間（暫停不算）：第一階段＝開場結束到轉階段開始、轉階段、第二階段＝轉階段結束到勝負（另列大迴旋與狂暴），
    /// 總時間＝開場結束到分出勝負（和勝利畫面的通關時間相同）。每場結束時在 Console 寫一行結算。
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class BalanceDebugPanel : MonoBehaviour
    {
        [SerializeField] BattleManager battle;
        [SerializeField] BossController boss;
        [SerializeField] BossHealth bossHealth;
        [SerializeField] BossStagger stagger;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth playerHealth;
        [Tooltip("分辨輕攻擊、重攻擊（頭暈前的失衡來源統計）")]
        [SerializeField] PlayerAttack playerAttack;
        [Tooltip("面板開著時鎖住視角、放開游標")]
        [SerializeField] CameraController cameraController;
        [Tooltip("一開始就打開面板（F12 切換）")]
        [SerializeField] bool panelVisible;
        [Tooltip("面板開著時暫停遊戲（面板裡也可以切換）")]
        [SerializeField] bool pauseWhileOpen = true;
        [Tooltip("面板關著時在右下角顯示計時")]
        [SerializeField] bool showTimer = true;
        [Tooltip("面板寬度（像素）")]
        [Min(400f)] [SerializeField] float panelWidth = 640f;

        // ---------- 跨重試保留（只在 Debug） ----------

        static readonly Dictionary<ScriptableObject, ScriptableObject> cloneOf = new Dictionary<ScriptableObject, ScriptableObject>();
        static readonly Dictionary<ScriptableObject, ScriptableObject> originalOf = new Dictionary<ScriptableObject, ScriptableObject>();
        static readonly List<RunRecord> runs = new List<RunRecord>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DestroyClones();
            runs.Clear();
            Application.quitting -= DestroyClones;
            Application.quitting += DestroyClones;
        }

        static void DestroyClones()
        {
            foreach (var clone in originalOf.Keys)
            {
                if (clone != null) Destroy(clone);
            }
            cloneOf.Clear();
            originalOf.Clear();
        }

        static bool IsTunable(Type t) =>
            typeof(BossStats).IsAssignableFrom(t) || typeof(BossPhaseData).IsAssignableFrom(t) ||
            typeof(BossMoveData).IsAssignableFrom(t) || typeof(PlayerStats).IsAssignableFrom(t);

        // ---------- 一個可調的欄位 ----------

        sealed class Knob
        {
            public string label;
            public ScriptableObject original, clone;
            public FieldInfo field;
            public float min, max, step;
            /// <summary>只顯示、不能直接調（例如跟著血量上限縮放的門檻）；存回 .asset 時一樣會寫。</summary>
            public bool readOnly;
            public Action<float, float> onChanged; // (舊值, 新值)

            public float Value => (float)field.GetValue(clone);
            public float Original => (float)field.GetValue(original);
            public bool Changed => !Mathf.Approximately(Value, Original);

            public void Set(float value)
            {
                float old = Value;
                if (Mathf.Approximately(old, value)) return;
                field.SetValue(clone, value);
                onChanged?.Invoke(old, value);
            }
        }

        sealed class Section
        {
            public string title;
            public bool foldable, expanded;
            public readonly List<Knob> knobs = new List<Knob>();
        }

        // ---------- 一場的紀錄 ----------

        sealed class RunRecord
        {
            public int attempt;
            public string result = "進行中";
            public bool finished;
            public float phase1, transition, phase2, ultimate, berserk;
            public float damagePhase1, damagePhase2, damageTaken;
            public int parries, stuns, executions, hitsTaken, bonesUsed, bonesEarned;
            public int tunedCount;

            public float Phase2Total => phase2 + ultimate + berserk;
            public float Total => phase1 + transition + Phase2Total;
        }

        static readonly string[] Tabs = { "計時", "Boss", "階段", "招式", "玩家" };

        readonly List<Section>[] sections = { null, new List<Section>(), new List<Section>(), new List<Section>(), new List<Section>() };
        readonly List<Knob> allKnobs = new List<Knob>();
        Knob maxHealthKnob;
        readonly List<Knob> thresholdKnobs = new List<Knob>();

        int tab;
        Vector2 scroll;
        float pendingHealth = -1f;
        bool confirmSave, opened, paused;
        bool savedPlayerLock, savedCameraLock, savedCursorReleased;
        string toast;
        float toastUntil;
        GUIStyle label, row, small, header, box;
        const float RowHeight = 24f;
        static readonly Color PanelBackground = new Color(0.07f, 0.08f, 0.1f, 1f);
        readonly StringBuilder sb = new StringBuilder();

        RunRecord run;
        // 上一次收到的剩餘骨頭；-1 = 還沒收到（開場的初始值不算用掉或得到）
        int lastBonesLeft = -1;
        BattleState segmentState = BattleState.None;
        float segmentStart;
        bool lastAttackHeavy;
        int stunParries, stunLights, stunHeavies;
        float stunCountStart;

        // ---------- 複本 ----------

        void Awake()
        {
            int swapped = 0;
            foreach (var root in Roots())
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && component != this) swapped += Remap(component);
                }
            }
            BuildKnobs();
            Debug.Log($"[Balance] 已把 {swapped} 個 SO 引用換成執行期複本（{originalOf.Count} 份），F12 調整數值不會改到 .asset；" +
                      $"目前有 {CountChanged()} 項調整（重試後保留）。");
        }

        IEnumerable<GameObject> Roots()
        {
            if (boss != null) yield return boss.gameObject;
            if (player != null) yield return player.gameObject;
            if (cameraController != null) yield return cameraController.gameObject;
        }

        static ScriptableObject CloneOf(ScriptableObject original)
        {
            if (originalOf.ContainsKey(original)) return original; // 已經是複本
            if (cloneOf.TryGetValue(original, out var clone) && clone != null) return clone;

            clone = Instantiate(original);
            clone.name = original.name;
            clone.hideFlags = HideFlags.HideAndDontSave; // 不存檔、換場景時不被卸載
            cloneOf[original] = clone;
            originalOf[clone] = original;
            Remap(clone); // 階段、開場序列、進場招式也指向複本
            return clone;
        }

        /// <summary>把 target 上引用可調 SO 的欄位（含陣列、List、父類別的私有欄位）換成複本；回傳換了幾個。</summary>
        static int Remap(object target)
        {
            int count = 0;
            for (var type = target.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject); type = type.BaseType)
            {
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var ft = f.FieldType;
                    if (typeof(ScriptableObject).IsAssignableFrom(ft))
                    {
                        if (f.GetValue(target) is ScriptableObject so && so != null && IsTunable(so.GetType()))
                        {
                            var clone = CloneOf(so);
                            if (clone != so)
                            {
                                f.SetValue(target, clone);
                                count++;
                            }
                        }
                    }
                    else if (ft.IsArray && typeof(ScriptableObject).IsAssignableFrom(ft.GetElementType()))
                    {
                        if (!(f.GetValue(target) is Array array)) continue;
                        var copy = (Array)array.Clone();
                        int changed = 0;
                        for (int i = 0; i < copy.Length; i++)
                        {
                            if (!(copy.GetValue(i) is ScriptableObject so) || so == null || !IsTunable(so.GetType())) continue;
                            var clone = CloneOf(so);
                            if (clone == so) continue;
                            copy.SetValue(clone, i);
                            changed++;
                        }
                        if (changed > 0) f.SetValue(target, copy);
                        count += changed;
                    }
                    else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) &&
                             typeof(ScriptableObject).IsAssignableFrom(ft.GetGenericArguments()[0]))
                    {
                        if (!(f.GetValue(target) is System.Collections.IList list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (!(list[i] is ScriptableObject so) || so == null || !IsTunable(so.GetType())) continue;
                            var clone = CloneOf(so);
                            if (clone == so) continue;
                            list[i] = clone;
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        static T FindClone<T>() where T : ScriptableObject
        {
            foreach (var clone in originalOf.Keys)
            {
                if (clone is T t) return t;
            }
            return null;
        }

        // ---------- 欄位清單 ----------

        Knob AddKnob(Section section, string text, ScriptableObject clone, string fieldName, float min, float max, float step)
        {
            if (clone == null || !originalOf.TryGetValue(clone, out var original)) return null;
            var field = clone.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(float))
            {
                Debug.LogWarning($"[Balance] {clone.GetType().Name} 沒有 float 欄位 {fieldName}");
                return null;
            }
            var knob = new Knob
            {
                label = text, original = original, clone = clone, field = field, min = min, step = step,
                max = Mathf.Max(max, (float)field.GetValue(original) * 2f),
            };
            section.knobs.Add(knob);
            allKnobs.Add(knob);
            return knob;
        }

        void BuildKnobs()
        {
            var bossStats = FindClone<BossStats>();
            if (bossStats != null)
            {
                var s = new Section { title = "OIIA 貓" };
                sections[1].Add(s);
                maxHealthKnob = AddKnob(s, "血量上限", bossStats, nameof(BossStats.maxHealth), 200f, 5000f, 50f);
                if (maxHealthKnob != null) maxHealthKnob.onChanged = HandleMaxHealthChanged;
                AddKnob(s, "失衡衰減延遲 (s)", bossStats, nameof(BossStats.imbalanceDecayDelay), 0f, 10f, 0.1f);
                AddKnob(s, "失衡衰減速度 (/s)", bossStats, nameof(BossStats.imbalanceDecayRate), 0f, 40f, 0.5f);
                AddKnob(s, "頭暈受傷倍率", bossStats, nameof(BossStats.stunDamageMultiplier), 1f, 3f, 0.05f);

                var t = new Section { title = "階段門檻（跟著血量上限等比例縮放）" };
                sections[1].Add(t);
                foreach (var phase in bossStats.phases)
                {
                    if (phase == null) continue;
                    var k = AddKnob(t, phase.displayName, phase, nameof(BossPhaseData.healthThreshold), 0f, 5000f, 1f);
                    if (k == null) continue;
                    k.readOnly = true;
                    thresholdKnobs.Add(k);

                    var p = new Section { title = phase.displayName };
                    sections[2].Add(p);
                    AddKnob(p, "移動速度 (m/s)", phase, nameof(BossPhaseData.moveSpeed), 0f, 10f, 0.1f);
                    AddKnob(p, "出招間隔 最短 (s)", phase, nameof(BossPhaseData.moveIntervalMin), 0f, 3f, 0.05f);
                    AddKnob(p, "出招間隔 最長 (s)", phase, nameof(BossPhaseData.moveIntervalMax), 0f, 3f, 0.05f);
                    AddKnob(p, "冷卻倍率", phase, nameof(BossPhaseData.cooldownMultiplier), 0.25f, 2f, 0.05f);
                    AddKnob(p, "失衡上限", phase, nameof(BossPhaseData.imbalanceMax), 25f, 300f, 5f);
                    AddKnob(p, "頭暈時間 (s)", phase, nameof(BossPhaseData.stunDuration), 0f, 8f, 0.1f);
                    AddKnob(p, "走位逾時 (s)", phase, nameof(BossPhaseData.repositionTimeout), 0f, 10f, 0.5f);
                }
            }

            if (boss != null)
            {
                foreach (var move in boss.Moves)
                {
                    var d = move.Data;
                    if (d == null) continue;
                    var m = new Section { title = $"[{d.phase}] {d.displayName}", foldable = true };
                    sections[3].Add(m);
                    AddKnob(m, d is TornadoData ? "每跳傷害" : "傷害", d, nameof(BossMoveData.damage), 0f, 100f, 1f);
                    if (d is PawSwipeData paw && paw.enhancedFinisher)
                        AddKnob(m, "最後一下傷害", d, nameof(PawSwipeData.finisherDamage), 0f, 100f, 1f);
                    AddKnob(m, "冷卻 (s)", d, nameof(BossMoveData.cooldown), 0f, 30f, 0.5f);
                    AddKnob(m, "預兆 (s)", d, nameof(BossMoveData.telegraphTime), 0f, 3f, 0.05f);
                    AddKnob(m, d is ThunderCircleData ? "破綻（含施法）(s)" : "破綻 (s)", d, nameof(BossMoveData.recoveryTime), 0f, 6f, 0.1f);
                    AddKnob(m, "權重", d, nameof(BossMoveData.weight), 0f, 5f, 0.1f);
                }
            }

            var playerStats = FindClone<PlayerStats>();
            if (playerStats != null)
            {
                var s = new Section { title = "刀盾狗" };
                sections[4].Add(s);
                AddKnob(s, "耐力上限", playerStats, nameof(PlayerStats.maxStamina), 50f, 200f, 5f);
                AddKnob(s, "耐力回復 (/s)", playerStats, nameof(PlayerStats.staminaRegen), 0f, 100f, 1f);
                AddKnob(s, "舉盾加速回復 (/s)", playerStats, nameof(PlayerStats.staminaRegenBlocking), 0f, 100f, 1f);
                AddKnob(s, "舉盾加速延遲 (s)", playerStats, nameof(PlayerStats.blockRegenDelay), 0f, 3f, 0.1f);
                AddKnob(s, "翻滾耗耐力", playerStats, nameof(PlayerStats.rollStaminaCost), 0f, 60f, 1f);
                AddKnob(s, "重擊耗耐力", playerStats, nameof(PlayerStats.heavyStaminaCost), 0f, 60f, 1f);
                AddKnob(s, "盾反失衡值", playerStats, nameof(PlayerStats.parryImbalance), 0f, 60f, 1f);
                AddKnob(s, "命中累積骨頭（根）", playerStats, nameof(PlayerStats.boneGainPerHit), 0f, 0.5f, 0.01f);
                AddKnob(s, "處決累積骨頭（根）", playerStats, nameof(PlayerStats.boneGainPerExecution), 0f, 1f, 0.05f);
                AddKnob(s, "受擊累積骨頭（根）", playerStats, nameof(PlayerStats.boneGainWhenHurt), 0f, 0.2f, 0.01f);
            }
        }

        // 血量上限改了：門檻照資產的比例縮放（UI 的 50%／20% 刻度才對得上），目前血量維持同樣比例
        void HandleMaxHealthChanged(float oldMax, float newMax)
        {
            float scale = maxHealthKnob.Original > 0f ? newMax / maxHealthKnob.Original : 1f;
            foreach (var k in thresholdKnobs) k.field.SetValue(k.clone, Mathf.Round(k.Original * scale));
            if (bossHealth != null && oldMax > 0f) bossHealth.DebugSetHealth(Mathf.Round(bossHealth.Current / oldMax * newMax));
        }

        int CountChanged()
        {
            int n = 0;
            foreach (var k in allKnobs)
            {
                if (k.Changed) n++;
            }
            return n;
        }

        void ResetAll()
        {
            foreach (var k in allKnobs)
            {
                if (!k.readOnly) k.Set(k.Original); // 血量上限會帶著門檻一起還原
            }
            foreach (var k in thresholdKnobs) k.field.SetValue(k.clone, k.Original);
            Notify("已全部還原成 .asset 的值");
        }

        string ChangesText()
        {
            sb.Clear();
            foreach (var k in allKnobs)
            {
                if (k.Changed) sb.AppendLine($"{k.original.name}.{k.field.Name}（{k.label}）: {Format(k.Original, k.step)} → {Format(k.Value, k.step)}");
            }
            return sb.ToString();
        }

#if UNITY_EDITOR
        void SaveToAssets()
        {
            var changes = ChangesText();
            var dirty = new HashSet<ScriptableObject>();
            foreach (var k in allKnobs)
            {
                if (!k.Changed) continue;
                var so = new SerializedObject(k.original);
                var prop = so.FindProperty(k.field.Name);
                if (prop == null) continue;
                prop.floatValue = k.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty.Add(k.original);
            }
            foreach (var asset in dirty)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            Debug.Log($"[Balance] 已存回 {dirty.Count} 個 .asset：\n{changes}");
            Notify($"已存回 {dirty.Count} 個 .asset（Console 有清單）");
        }
#endif

        // ---------- 開關、暫停 ----------

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f12Key.wasPressedThisFrame) SetOpen(!panelVisible);
        }

        // HitStop 結束時會把 timeScale 還原；放在 LateUpdate 才壓得住，下一幀的 deltaTime 才是 0
        void LateUpdate()
        {
            if (paused) Time.timeScale = 0f;
        }

        void SetOpen(bool open)
        {
            panelVisible = open;
            confirmSave = false;
            if (open && !opened)
            {
                opened = true;
                if (player != null)
                {
                    savedPlayerLock = player.InputLocked;
                    player.SetInputLocked(true);
                }
                if (cameraController != null)
                {
                    savedCameraLock = cameraController.InputLocked;
                    savedCursorReleased = cameraController.CursorReleased;
                    cameraController.SetInputLocked(true);
                    cameraController.SetCursorReleased(true);
                }
                SetPaused(pauseWhileOpen);
            }
            else if (!open && opened)
            {
                opened = false;
                SetPaused(false);
                // 開場、轉階段演出或勝敗畫面期間維持鎖定，交給原本的流程解除
                bool fighting = battle == null || IsFighting(battle.State);
                if (player != null && !savedPlayerLock && fighting) player.SetInputLocked(false);
                if (cameraController != null)
                {
                    if (!savedCameraLock && fighting) cameraController.SetInputLocked(false);
                    if (!savedCursorReleased && (battle == null || !battle.IsOver)) cameraController.SetCursorReleased(false);
                }
            }
        }

        void SetPaused(bool on)
        {
            if (paused == on) return;
            paused = on;
            Time.timeScale = on ? 0f : 1f; // 勝利的慢動作由 BattleManager 下一幀自己再設
        }

        static bool IsFighting(BattleState s) =>
            s == BattleState.Phase1 || s == BattleState.Phase2 || s == BattleState.Ultimate || s == BattleState.Berserk;

        void OnDisable()
        {
            if (opened) SetOpen(false);
            Unsubscribe();
            if (run != null && !run.finished)
            {
                CloseSegment();
                run.result = "中斷（重試）";
                run.finished = true;
            }
        }

        void Notify(string text)
        {
            toast = text;
            toastUntil = Time.realtimeSinceStartup + 2.5f;
        }

        // ---------- 計時與統計 ----------

        void OnEnable()
        {
            if (battle != null) battle.OnBattleStateChanged += HandleBattleState;
            if (bossHealth != null) bossHealth.OnDamaged += HandleBossDamaged;
            if (stagger != null)
            {
                stagger.OnStunned += HandleStunned;
                stagger.OnStunEnded += HandleStunEnded;
            }
            if (playerHealth != null)
            {
                playerHealth.OnParry += HandleParry;
                playerHealth.OnDamageTaken += HandlePlayerDamaged;
                playerHealth.OnBonesChanged += HandleBonesChanged;
            }
            if (playerAttack != null) playerAttack.OnAttackStarted += HandleAttackStarted;
        }

        void Unsubscribe()
        {
            if (battle != null) battle.OnBattleStateChanged -= HandleBattleState;
            if (bossHealth != null) bossHealth.OnDamaged -= HandleBossDamaged;
            if (stagger != null)
            {
                stagger.OnStunned -= HandleStunned;
                stagger.OnStunEnded -= HandleStunEnded;
            }
            if (playerHealth != null)
            {
                playerHealth.OnParry -= HandleParry;
                playerHealth.OnDamageTaken -= HandlePlayerDamaged;
                playerHealth.OnBonesChanged -= HandleBonesChanged;
            }
            if (playerAttack != null) playerAttack.OnAttackStarted -= HandleAttackStarted;
        }

        void HandleBattleState(BattleState next)
        {
            CloseSegment();
            segmentState = next;
            segmentStart = Time.time;

            if (next == BattleState.Phase1 && run == null)
            {
                run = new RunRecord { attempt = BattleSession.Attempt };
                runs.Add(run);
                ResetStunCount();
            }
            if ((next == BattleState.Victory || next == BattleState.Defeat) && run != null && !run.finished)
            {
                run.result = next == BattleState.Victory ? "勝利" : "失敗";
                run.finished = true;
                run.tunedCount = CountChanged();
                Debug.Log($"[Balance] {RunLine(run)}");
            }
        }

        // 把目前這段的時間記進這場的紀錄
        void CloseSegment()
        {
            if (run == null || run.finished) return;
            float dt = Time.time - segmentStart;
            segmentStart = Time.time;
            switch (segmentState)
            {
                case BattleState.Phase1: run.phase1 += dt; break;
                case BattleState.Transition: run.transition += dt; break;
                case BattleState.Phase2: run.phase2 += dt; break;
                case BattleState.Ultimate: run.ultimate += dt; break;
                case BattleState.Berserk: run.berserk += dt; break;
            }
        }

        void HandleBossDamaged(BossDamageReport r)
        {
            if (run == null || run.finished || r.result != DamageResult.Hit) return;
            if (segmentState == BattleState.Phase1) run.damagePhase1 += r.healthLost;
            else run.damagePhase2 += r.healthLost;
            if (r.info.isExecution) run.executions++;
            else if (lastAttackHeavy) stunHeavies++;
            else stunLights++;
        }

        void HandleAttackStarted(int comboIndex, bool heavy) => lastAttackHeavy = heavy;

        void HandleParry(DamageInfo info, float imbalance)
        {
            if (run == null || run.finished) return;
            run.parries++;
            stunParries++;
        }

        void HandlePlayerDamaged(PlayerDamageReport r)
        {
            if (run == null || run.finished) return;
            run.damageTaken += r.healthLost;
            if (r.result == DamageResult.Hit) run.hitsTaken++;
        }

        void HandleBonesChanged(int left, int max)
        {
            // 骨頭會累積增加，用前後差算：少了是啃掉，多了是湊滿
            if (lastBonesLeft >= 0 && run != null && !run.finished)
            {
                if (left < lastBonesLeft) run.bonesUsed += lastBonesLeft - left;
                else run.bonesEarned += left - lastBonesLeft;
            }
            lastBonesLeft = left;
        }

        void HandleStunned(float duration)
        {
            if (run == null || run.finished) return;
            run.stuns++;
            Debug.Log($"[Balance] 頭暈 #{run.stuns}：這次靠盾反 {stunParries} 次、輕攻擊 {stunLights} 下、重攻擊 {stunHeavies} 下，" +
                      $"距上次頭暈結束 {Time.time - stunCountStart:0.0} s");
        }

        void HandleStunEnded(StunEndReason reason) => ResetStunCount();

        void ResetStunCount()
        {
            stunParries = stunLights = stunHeavies = 0;
            stunCountStart = Time.time;
        }

        float LiveSegment(BattleState s) =>
            run != null && !run.finished && segmentState == s ? Time.time - segmentStart : 0f;

        static string Clock(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            return $"{m}:{seconds - m * 60f:00.0}";
        }

        static string Dps(float damage, float time) => time > 0f ? $"{damage / time:0.0}" : "—";

        static string RunLine(RunRecord r) =>
            $"第 {r.attempt} 次 {r.result}｜總 {Clock(r.Total)}｜第一階段 {Clock(r.phase1)}（DPS {Dps(r.damagePhase1, r.phase1)}）" +
            $"｜轉階段 {Clock(r.transition)}｜第二階段 {Clock(r.Phase2Total)}（DPS {Dps(r.damagePhase2, r.Phase2Total)}；" +
            $"大迴旋 {Clock(r.ultimate)}、狂暴 {Clock(r.berserk)}）｜盾反 {r.parries}、頭暈 {r.stuns}、處決 {r.executions}" +
            $"｜被打中 {r.hitsTaken} 次、受傷 {r.damageTaken:0}、骨頭 用 {r.bonesUsed}／得 {r.bonesEarned}｜調整 {r.tunedCount} 項";

        // ---------- 畫面 ----------

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                row = new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, fixedHeight = RowHeight, margin = new RectOffset(4, 4, 0, 0) };
                small = new GUIStyle(label) { fontSize = 12, wordWrap = true };
                header = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
                box = new GUIStyle(GUI.skin.box) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4) };
                box.normal.textColor = Color.white;
            }
            GUI.depth = -100; // 蓋在其他 Debug 面板上面

            if (!panelVisible)
            {
                if (showTimer && run != null) DrawTimerStrip();
                return;
            }

            var rect = new Rect(10f, 10f, Mathf.Min(panelWidth, Screen.width - 20f), Screen.height - 20f);
            var oldColor = GUI.color;
            GUI.color = PanelBackground; // 不透明底色，蓋住後面的 Debug 面板與 HUD
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));

            GUILayout.Label($"<b>平衡面板（F12）</b>　{(paused ? "<color=#ffd84d>暫停中</color>" : "遊戲進行中")}　調整 {CountChanged()} 項", header);
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = GUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawTimerTab();
            else
            {
                if (tab == 1) DrawHealthRow();
                foreach (var s in sections[tab]) DrawSection(s);
            }
            GUILayout.EndScrollView();
            DrawFooter();
            GUILayout.EndArea();
        }

        void DrawTimerStrip()
        {
            string current = segmentState == BattleState.Phase1 ? $"第一階段 {Clock(run.phase1 + LiveSegment(BattleState.Phase1))}"
                : $"第二階段 {Clock(run.Phase2Total + LiveSegment(BattleState.Phase2) + LiveSegment(BattleState.Ultimate) + LiveSegment(BattleState.Berserk))}";
            string text = $"{current}　總 {Clock(LiveTotal())}" + (run.finished ? $"　{run.result}" : "");
            var size = box.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(Screen.width - size.x - 12f, Screen.height - size.y - 12f, size.x, size.y), text, box);
        }

        float LiveTotal() =>
            run.Total + LiveSegment(BattleState.Phase1) + LiveSegment(BattleState.Transition) + LiveSegment(BattleState.Phase2) +
            LiveSegment(BattleState.Ultimate) + LiveSegment(BattleState.Berserk);

        void DrawTimerTab()
        {
            if (run == null)
            {
                GUILayout.Label("開場結束後開始計時。", label);
            }
            else
            {
                float p1 = run.phase1 + LiveSegment(BattleState.Phase1);
                float tr = run.transition + LiveSegment(BattleState.Transition);
                float p2 = run.phase2 + LiveSegment(BattleState.Phase2);
                float ul = run.ultimate + LiveSegment(BattleState.Ultimate);
                float be = run.berserk + LiveSegment(BattleState.Berserk);
                GUILayout.Label($"<b>第 {run.attempt} 次挑戰</b>　{run.result}　（目標 4:00–5:00）", header);
                GUILayout.Label($"第一階段　{Clock(p1)}　造成 {run.damagePhase1:0}　DPS {Dps(run.damagePhase1, p1)}", label);
                GUILayout.Label($"轉階段　　{Clock(tr)}", label);
                GUILayout.Label($"第二階段　{Clock(p2 + ul + be)}　造成 {run.damagePhase2:0}　DPS {Dps(run.damagePhase2, p2 + ul + be)}", label);
                GUILayout.Label($"　　強化 {Clock(p2)}　大迴旋 {Clock(ul)}　狂暴 {Clock(be)}", label);
                GUILayout.Label($"<b>總時間　　{Clock(p1 + tr + p2 + ul + be)}</b>", label);
                GUILayout.Label($"盾反 {run.parries}　頭暈 {run.stuns}　處決 {run.executions}　被打中 {run.hitsTaken} 次　受傷 {run.damageTaken:0}　骨頭 用 {run.bonesUsed}／得 {run.bonesEarned}", label);
                string max = stagger != null ? $" / {stagger.Max:0}" : "";
                GUILayout.Label($"這次頭暈前：盾反 {stunParries}、輕攻擊 {stunLights}、重攻擊 {stunHeavies}　目前失衡 {(stagger != null ? stagger.Current : 0f):0.0}{max}", label);
            }

            GUILayout.Space(8f);
            GUILayout.Label("<b>這次 Play 的紀錄</b>（重試後保留，結束時 Console 也有一行）", label);
            if (runs.Count == 0) GUILayout.Label("　—", label);
            for (int i = runs.Count - 1; i >= 0; i--) GUILayout.Label(RunLine(runs[i]), small);
        }

        void DrawHealthRow()
        {
            if (bossHealth == null) return;
            GUILayout.Label($"<b>目前血量</b>　{bossHealth.Current:0} / {bossHealth.Max:0}", label);
            if (pendingHealth < 0f || pendingHealth > bossHealth.Max) pendingHealth = bossHealth.Current;
            GUILayout.BeginHorizontal();
            pendingHealth = Mathf.Round(GUILayout.HorizontalSlider(pendingHealth, 0f, bossHealth.Max, GUILayout.Width(260f)) / 10f) * 10f;
            GUILayout.Label($"{pendingHealth:0}", label, GUILayout.Width(60f));
            if (GUILayout.Button("套用", GUILayout.Width(60f)))
            {
                bossHealth.DebugSetHealth(pendingHealth);
                Notify($"Boss 血量設為 {pendingHealth:0}（越過門檻會轉階段）");
            }
            if (GUILayout.Button("目前值", GUILayout.Width(70f))) pendingHealth = bossHealth.Current;
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        void DrawSection(Section s)
        {
            if (s.foldable)
            {
                int changed = 0;
                foreach (var k in s.knobs)
                {
                    if (k.Changed) changed++;
                }
                string mark = changed > 0 ? $"　<color=#ffd84d>改了 {changed} 項</color>" : "";
                string summary = s.knobs.Count > 0 ? $"　<color=#aaaaaa>{Summary(s)}</color>" : "";
                if (GUILayout.Button($"{(s.expanded ? "▼" : "▶")} {s.title}{summary}{mark}", label)) s.expanded = !s.expanded;
                if (!s.expanded) return;
            }
            else
            {
                GUILayout.Space(4f);
                GUILayout.Label($"<b>{s.title}</b>", header);
            }
            foreach (var k in s.knobs) DrawKnob(k);
        }

        string Summary(Section s)
        {
            sb.Clear();
            foreach (var k in s.knobs)
            {
                if (k.field.Name == nameof(BossMoveData.damage) || k.field.Name == nameof(BossMoveData.cooldown))
                    sb.Append($"{k.label} {Format(k.Value, k.step)}  ");
            }
            return sb.ToString();
        }

        void DrawKnob(Knob k)
        {
            float v = k.Value;
            bool changed = k.Changed;
            string name = changed ? $"<color=#ffd84d>{k.label}</color>" : k.label;
            string original = changed ? $"<color=#ffd84d>原 {Format(k.Original, k.step)}</color>" : "";

            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label($"　{name}", row, GUILayout.Width(190f));
            if (k.readOnly)
            {
                GUILayout.Label($"{Format(v, k.step)}　{original}", row);
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.Width(190f));
            GUILayout.Space(RowHeight * 0.5f - 5f); // 滑桿畫在區塊頂端，往下推到和文字同一條線
            float next = GUILayout.HorizontalSlider(v, k.min, k.max, GUILayout.Width(190f));
            GUILayout.EndVertical();
            if (GUILayout.Button("−", GUILayout.Width(24f), GUILayout.Height(RowHeight - 2f))) next = v - k.step;
            if (GUILayout.Button("+", GUILayout.Width(24f), GUILayout.Height(RowHeight - 2f))) next = v + k.step;
            GUILayout.Label(Format(v, k.step), row, GUILayout.Width(56f));
            GUILayout.Label(original, row);
            GUILayout.EndHorizontal();

            next = Mathf.Clamp(Mathf.Round(next / k.step) * k.step, k.min, k.max);
            if (!Mathf.Approximately(next, v)) k.Set(next);
        }

        static string Format(float value, float step) =>
            step >= 1f ? value.ToString("0") : step >= 0.1f ? value.ToString("0.0") : value.ToString("0.00");

        void DrawFooter()
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            bool pause = GUILayout.Toggle(pauseWhileOpen, "開著面板時暫停");
            if (pause != pauseWhileOpen)
            {
                pauseWhileOpen = pause;
                SetPaused(pause);
            }
            showTimer = GUILayout.Toggle(showTimer, "關著時顯示計時");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全部還原")) ResetAll();
            if (GUILayout.Button("複製調整清單與紀錄"))
            {
                var text = new StringBuilder();
                text.AppendLine("調整：");
                text.Append(CountChanged() > 0 ? ChangesText() : "（沒有）\n");
                text.AppendLine("紀錄：");
                foreach (var r in runs) text.AppendLine(RunLine(r));
                GUIUtility.systemCopyBuffer = text.ToString();
                Notify("已複製到剪貼簿");
            }
#if UNITY_EDITOR
            int count = CountChanged();
            GUI.enabled = count > 0;
            if (GUILayout.Button(confirmSave ? $"<color=#ff8060>再按一次：寫入 {count} 項到 .asset</color>" : "存回 .asset", new GUIStyle(GUI.skin.button) { richText = true }))
            {
                if (confirmSave)
                {
                    SaveToAssets();
                    confirmSave = false;
                }
                else confirmSave = true;
            }
            GUI.enabled = true;
#endif
            GUILayout.EndHorizontal();

            if (toast != null && Time.realtimeSinceStartup < toastUntil) GUILayout.Label($"<color=#7dff7d>{toast}</color>", label);
        }
    }
}
#endif
