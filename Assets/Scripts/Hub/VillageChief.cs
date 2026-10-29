using System;
using System.Collections.Generic;
using System.Text;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的村長（T24，企劃書「村長與任務」）：對他按 Player/Interact 打開對話（DialogueUI），依任務狀態挑要講的話：
    /// 可回報的任務（先回報：發獎勵並存檔，再講回報對白，最後補一句拿到的獎勵）＞ 可接的任務（講完問「接受／再想想」）＞ 進行中的任務 ＞ 閒聊。
    /// 任務照 GameDatabase.quests 的順序挑，狀態由 ProgressRecorder 依存檔算。頭上的標記（有可回報「?」、有可接「!」）用 OnMarkerChanged 送出。
    /// 開場劇情：存檔沒看過時，HubManager 在淡入後呼叫 BeginIntro，村長直線走到玩家面前（期間 ReadyToTalk 是 false，HubManager 不切鏡頭），
    /// 走到之後他的站位點剛好在玩家腳下，站點鏡頭（他的子物件）就是雙人鏡頭；講完開場對白（HubStoryData）存檔記住看過、開放第一個任務，再走回原位。
    /// 走回去的路上也可以找他講話（講話時停下來，講完繼續走）。對話一開始就送 OnDialogue，介面講完後呼叫 Accept、Decline 或 HubManager.CloseStation。
    /// 開場（含序章的擊倒版／勝利版）講完直接接第一個可接任務的說明（I2）：CompleteIntroDialogue 回傳那個任務，介面接著呼叫 Offer 講任務說明，講完問「接受／再想想」；
    /// 選再想想時 Decline 講提醒（HubStoryData.declineLines），關掉介面後頭上亮「!」。在原位、有任務時，玩家走近會轉身面向玩家（I3）。
    /// </summary>
    public class VillageChief : Interactable, IPlayerBinding
    {
        /// <summary>頭上的標記。</summary>
        public enum QuestMarker
        {
            None,
            /// <summary>有可接的任務「!」。</summary>
            NewQuest,
            /// <summary>有可回報的任務「?」。</summary>
            TurnIn,
        }

        /// <summary>一段對話（同一份物件重複使用，收到後不要留著）。</summary>
        public sealed class DialogueView
        {
            /// <summary>說話者名稱。</summary>
            public string speaker;
            /// <summary>依序講的每一句。</summary>
            public readonly List<string> lines = new List<string>();
            /// <summary>講完要問接不接的任務；null = 講完就結束。</summary>
            public QuestData offer;
        }

        [SerializeField] HubStoryData story;
        [Tooltip("任務清單（GameDatabase.quests）")]
        [SerializeField] GameDatabase database;
        [Tooltip("頭上「!」「?」的位置；留空時用自己的位置")]
        [SerializeField] Transform markerAnchor;

        /// <summary>開始一段對話（走到之後、接下任務後）。</summary>
        public event Action<DialogueView> OnDialogue;
        /// <summary>頭上的標記換了（Start 時也送一次）；Transform 是標記的位置（會跟著村長走）。</summary>
        public event Action<QuestMarker, Transform> OnMarkerChanged;

        /// <summary>存檔還沒看過開場劇情。</summary>
        public bool WantsIntro => !ProfileStore.StoryIntroSeen;
        /// <summary>淡入後等幾秒開始開場劇情。</summary>
        public float IntroDelay => story != null ? story.introDelay : 0f;
        /// <summary>開場時還在走過來的路上就是 false。</summary>
        public override bool ReadyToTalk => !comingOver;

        readonly DialogueView view = new DialogueView();
        readonly StringBuilder builder = new StringBuilder();
        Vector3 homePosition, walkTarget;
        Quaternion homeRotation, finalRotation;
        bool walking, turning, comingOver, open, dialogueStarted, introPlaying, markerSent;
        QuestMarker marker;
        PlayerRig player;

        /// <summary>只記引用（轉身面向玩家用）。</summary>
        public void BindPlayer(PlayerRig rig) => player = rig;

        void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        void Start() => RefreshMarker();

        /// <summary>開場劇情：走到玩家面前（自己的站位點落在 playerPosition），HubManager 接著打開這個站點。</summary>
        public void BeginIntro(Vector3 playerPosition)
        {
            Vector3 to = playerPosition - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) to = transform.forward;
            Vector3 dir = to.normalized;
            Vector3 stand = StandPoint.position - transform.position;
            stand.y = 0f;
            walkTarget = new Vector3(playerPosition.x, transform.position.y, playerPosition.z) - dir * stand.magnitude;
            finalRotation = Quaternion.LookRotation(dir);
            walking = comingOver = true;
            turning = false;
        }
        /// <summary>先擺到醒來第一眼的雙人位置，保留 homePosition，講完仍回原位。</summary>
        public void PlaceForWake(Vector3 playerPosition)
        { BeginIntro(playerPosition); transform.SetPositionAndRotation(walkTarget, finalRotation); walking = turning = comingOver = false; }

        /// <summary>
        /// 開場對白完整讀完時呼叫（取消或關面板不算）：記住看過、開放第一個任務，回傳現在可接的任務（介面接著問接不接）；
        /// 不是開場或沒有可接的任務時回傳 null。
        /// </summary>
        public QuestData CompleteIntroDialogue()
        {
            if (!open || !introPlaying) return null;
            introPlaying = false; ProfileStore.MarkStoryIntroSeen(); RefreshMarker();
            return Find(QuestState.Available);
        }

        /// <summary>講可接任務的說明（QuestData.offerLines），講完介面問接不接；開場講完時用。不是可接時不做。</summary>
        public void Offer(QuestData quest)
        {
            if (!open || quest == null || ProgressRecorder.GetState(quest) != QuestState.Available) return;
            BeginView();
            AddLines(quest.offerLines);
            view.offer = quest;
            OnDialogue?.Invoke(view);
        }

        /// <summary>任務說明後選「再想想」：村長講提醒（任務照樣可接，頭上的「!」在關掉介面後出現）。</summary>
        public void Decline()
        {
            if (!open) return;
            BeginView();
            if (story != null) AddLines(story.declineLines);
            OnDialogue?.Invoke(view);
        }

        public override void Opened(PlayerRig rig)
        {
            open = true;
            dialogueStarted = false;
        }

        // 走到了（開場時是村長走到玩家面前）才開始講
        public override void Tick(PlayerRig rig, bool arrived)
        {
            if (dialogueStarted || comingOver) return;
            dialogueStarted = true;
            StartDialogue();
        }

        public override void Closed(PlayerRig rig)
        {
            open = false;
            if (introPlaying)
            {
                introPlaying = false;
            }
            RefreshMarker();
            // 不在原位就走回去
            Vector3 away = homePosition - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f || Quaternion.Angle(transform.rotation, homeRotation) > 0.5f)
            {
                walkTarget = homePosition;
                finalRotation = homeRotation;
                walking = true;
                comingOver = turning = false;
            }
        }

        /// <summary>接下對話問的任務（DialogueUI 的「接受」）：接了之後村長補一句；不是可接時回傳 false。</summary>
        public bool Accept(QuestData quest)
        {
            if (!open || !ProgressRecorder.Accept(quest)) return false;
            RefreshMarker();
            BeginView();
            if (story != null) view.lines.Add(string.Format(story.questAcceptedFormat, quest.title));
            AddLines(quest.acceptedLines);
            OnDialogue?.Invoke(view);
            return true;
        }

        void Update()
        {
            if (story == null || (open && !comingOver)) return; // 開場走過來時站點已經打開了；其他時候講話時停下來
            if (walking) Walk();
            else if (turning) Turn();
            else if (!open) Greet();
        }

        // 在原位、有任務可接或可回報時，玩家走近就轉身看他；走遠了轉回原本的朝向
        void Greet()
        {
            Quaternion want = homeRotation;
            if (player != null && marker != QuestMarker.None)
            {
                Vector3 to = player.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude <= story.greetRadius * story.greetRadius && to.sqrMagnitude > 1e-4f) want = Quaternion.LookRotation(to);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, story.turnSpeed * Time.deltaTime);
        }

        // 直線走向 walkTarget、面向前進方向；到了之後開場是直接面向玩家（站位點要剛好在玩家腳下），回原位是慢慢轉回原本的朝向
        void Walk()
        {
            Vector3 pos = transform.position;
            Vector3 to = walkTarget - pos;
            to.y = 0f;
            float step = story.walkSpeed * Time.deltaTime;
            if (to.sqrMagnitude > step * step)
            {
                transform.position = pos + to.normalized * step;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), story.turnSpeed * Time.deltaTime);
                return;
            }
            transform.position = new Vector3(walkTarget.x, pos.y, walkTarget.z);
            walking = false;
            if (comingOver)
            {
                transform.rotation = finalRotation;
                comingOver = false;
            }
            else turning = true;
        }

        void Turn()
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, finalRotation, story.turnSpeed * Time.deltaTime);
            turning = Quaternion.Angle(transform.rotation, finalRotation) > 0.01f;
        }

        // ---------- 對話 ----------

        void StartDialogue()
        {
            BeginView();
            if (WantsIntro)
            {
                introPlaying = true;
                if (story != null) AddLines(ProfileStore.PrologueResult == PrologueResult.KnockedDown ? story.knockdownLines :
                    ProfileStore.PrologueResult == PrologueResult.Victory ? story.victoryLines : story.introLines);
            }
            else if (Find(QuestState.ReadyToTurnIn) is QuestData turnIn)
            {
                var granted = ProgressRecorder.TurnIn(turnIn);
                AddLines(turnIn.turnInLines);
                if (granted.Count > 0 && story != null) view.lines.Add(string.Format(story.rewardLineFormat, DescribeRewards(granted)));
                RefreshMarker();
            }
            else if (Find(QuestState.Available) is QuestData offer)
            {
                AddLines(offer.offerLines);
                view.offer = offer;
            }
            else if (Find(QuestState.Active) is QuestData active) AddLines(active.activeLines);
            else if (story != null) AddLines(story.idleLines);
            OnDialogue?.Invoke(view);
        }

        void BeginView()
        {
            view.speaker = story != null ? story.chiefName : string.Empty;
            view.lines.Clear();
            view.offer = null;
        }

        void AddLines(List<string> lines)
        {
            if (lines == null) return;
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line)) view.lines.Add(line);
            }
        }

        string DescribeRewards(IReadOnlyList<RewardData> rewards)
        {
            builder.Clear();
            foreach (var reward in rewards)
            {
                string text = reward.Describe(story.skillPointsFormat);
                if (string.IsNullOrEmpty(text)) continue;
                if (builder.Length > 0) builder.Append(story.rewardSeparator);
                builder.Append(text);
            }
            return builder.ToString();
        }

        // 照 GameDatabase.quests 的順序找第一個在這個狀態的任務
        QuestData Find(QuestState state)
        {
            if (database == null || database.quests == null) return null;
            foreach (var quest in database.quests)
            {
                if (quest != null && ProgressRecorder.GetState(quest) == state) return quest;
            }
            return null;
        }

        void RefreshMarker()
        {
            var next = Find(QuestState.ReadyToTurnIn) != null ? QuestMarker.TurnIn
                     : Find(QuestState.Available) != null ? QuestMarker.NewQuest
                     : QuestMarker.None;
            if (markerSent && next == marker) return;
            markerSent = true;
            marker = next;
            OnMarkerChanged?.Invoke(marker, markerAnchor != null ? markerAnchor : transform);
        }
    }
}
