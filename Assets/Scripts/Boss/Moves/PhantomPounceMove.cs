using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 幻影分身（第二階段新招，取代扭屁股飛撲）：預兆時魔法陣閃爍，本體戴上發光項圈，左右各出現半透明的幻影（BossPhantom）；
    /// 出招時本體與幻影依序飛撲：每隔 pounceInterval 秒輪到下一隻伏低扭屁股 pounceWindup 秒再起跳，飛撲本身和扭屁股飛撲一樣（PounceRunner）。
    /// 順序：幻影隨機，本體預設最後一個（realPouncesLast）。每隻 damage，可擋、可盾反（白光）；盾反幻影也算這招被盾反，Boss 照樣加失衡。
    /// 幻影一擊即碎，碎掉的那一隻不再飛撲；幻影的判定不會打到 Boss 和其他幻影，本體的也不會打到幻影。
    /// 所有飛撲都落地後進入破綻 recoveryTime 秒，幻影淡出。數值讀 PhantomPounceData。
    /// </summary>
    [DisallowMultipleComponent]
    public class PhantomPounceMove : BossMove
    {
        [Tooltip("本體的攻擊判定，attacker 指向 Boss 根物件")]
        [SerializeField] Hitbox hitbox;
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("落點圈（執行時每一隻各生成一份）")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [Tooltip("落地的衝擊環")]
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] [ColorUsage(true, true)] Color impactColor = new Color(2.6f, 2.6f, 2.6f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color phantomImpactColor = new Color(2.4f, 0.5f, 0.4f, 1f);
        [Tooltip("幻影（執行時生成 phantomCount 隻）")]
        [SerializeField] BossPhantom phantomPrefab;
        [Tooltip("本體戴的發光項圈（平常隱藏）")]
        [SerializeField] GameObject collar;
        [Tooltip("召喚時閃爍的魔法陣（場景物件，可留空）")]
        [SerializeField] MagicCircle magicCircle;
        [Tooltip("落地後伏低的程度（0..1）")]
        [Range(0f, 1f)] [SerializeField] float landingCrouch = 0.6f;
        [Tooltip("破綻時伏低的程度（0..1）")]
        [Range(0f, 1f)] [SerializeField] float recoveryCrouch = 0.3f;
        [Tooltip("幻影轉向玩家的速度（度/秒）")]
        [Min(0f)] [SerializeField] float phantomTurnSpeed = 360f;

        // 一隻會飛撲的貓：本體（phantom 為 null）或一隻幻影
        class Pouncer
        {
            public PounceRunner runner;
            public BossPhantom phantom;
            public GroundTelegraph circle;
            public float windupAt;
            public bool started, leapt, cancelled;
        }

        PhantomPounceData pounce;
        readonly List<Pouncer> pouncers = new List<Pouncer>();
        readonly List<Pouncer> order = new List<Pouncer>();
        float timer;

        void Awake()
        {
            pounce = Data as PhantomPounceData;
            if (pounce == null)
            {
                Debug.LogError($"[Boss] {name} 的資料不是 PhantomPounceData。", this);
                return;
            }
            pouncers.Add(new Pouncer { circle = NewCircle("Real") });
            for (int i = 0; i < pounce.phantomCount; i++)
            {
                var phantom = Instantiate(phantomPrefab);
                phantom.name = $"{name}_Phantom{i + 1}";
                pouncers.Add(new Pouncer { phantom = phantom, circle = NewCircle($"Phantom{i + 1}") });
            }
            if (collar != null) collar.SetActive(false);
        }

        GroundTelegraph NewCircle(string label)
        {
            var circle = Instantiate(telegraphPrefab);
            circle.name = $"{name}_{label}_Telegraph";
            return circle;
        }

        void Start()
        {
            // Boss 在 BossController.Awake 才綁定
            foreach (var p in pouncers)
            {
                if (p.phantom == null)
                {
                    p.runner = new PounceRunner(Boss, Boss.transform, poser, hitbox, p.circle, impactPrefab, impactColor, landingCrouch,
                                                BossImpactKind.PounceLanding);
                    hitbox.Filter = NotOwnSide;
                }
                else
                {
                    p.runner = new PounceRunner(Boss, p.phantom.transform, p.phantom.Poser, p.phantom.Hitbox, p.circle,
                                                impactPrefab, phantomImpactColor, landingCrouch, BossImpactKind.PhantomLanding);
                    p.phantom.Hitbox.Filter = NotOwnSide;
                }
            }
        }

        void OnDestroy()
        {
            foreach (var p in pouncers)
            {
                if (p.circle != null) Destroy(p.circle.gameObject);
                if (p.phantom != null) Destroy(p.phantom.gameObject);
            }
        }

        void OnEnable()
        {
            hitbox.OnHit += ReportHit;
            foreach (var p in pouncers)
            {
                if (p.phantom == null) continue;
                p.phantom.Hitbox.OnHit += ReportHit;
                p.phantom.OnShattered += HandleShattered;
            }
        }

        void OnDisable()
        {
            hitbox.OnHit -= ReportHit;
            foreach (var p in pouncers)
            {
                if (p.phantom == null) continue;
                p.phantom.Hitbox.OnHit -= ReportHit;
                p.phantom.OnShattered -= HandleShattered;
            }
        }

        // 飛撲的判定不打 Boss 本體和幻影（幻影的攻擊方是幻影自己，不排除的話會打到 Boss）
        bool NotOwnSide(Collider col) => !col.transform.IsChildOf(Boss.transform) && col.GetComponentInParent<BossPhantom>() == null;

        public override void BeginTelegraph()
        {
            timer = 0f;
            if (collar != null) collar.SetActive(true);
            if (magicCircle != null) magicCircle.Flicker(TelegraphTime);

            // 幻影平均排在本體左右兩側：右 1、左 1、右 2、左 2…
            Vector3 me = OnGround(Boss.transform.position);
            Vector3 right = Flat(Boss.transform.right).normalized;
            int index = 0;
            foreach (var p in pouncers)
            {
                p.started = p.leapt = p.cancelled = false;
                if (p.phantom == null) continue;
                float side = index % 2 == 0 ? 1f : -1f;
                float distance = pounce.spawnDistance * (index / 2 + 1);
                p.phantom.Appear(Boss.ClampToArena(me + right * (side * distance)), Boss.transform.rotation);
                index++;
            }
        }

        public override void TickTelegraph(float dt)
        {
            base.TickTelegraph(dt);
            FacePhantoms(dt, null);
        }

        public override void BeginActive()
        {
            timer = 0f;
            order.Clear();
            Pouncer real = null;
            foreach (var p in pouncers)
            {
                if (p.phantom == null) real = p;
                else order.Insert(Random.Range(0, order.Count + 1), p);
            }
            if (pounce.realPouncesLast) order.Add(real);
            else order.Insert(Random.Range(0, order.Count + 1), real);
            for (int i = 0; i < order.Count; i++) order[i].windupAt = i * pounce.pounceInterval;
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            bool allDone = true;
            foreach (var p in order)
            {
                if (p.cancelled) continue;
                if (!p.leapt)
                {
                    allDone = false;
                    if (p.phantom == null) Boss.FaceTarget(dt);
                    else FacePhantoms(dt, p);
                    if (!p.started)
                    {
                        if (timer < p.windupAt) continue;
                        p.started = true;
                        p.runner.BeginWindup(pounce, pounce.pounceWindup, Data.canBlock);
                        if (p.phantom == null) Telegraph(Data.canBlock, pounce.pounceWindup);
                        else p.phantom.Telegraph(pounce.pounceWindup);
                        continue;
                    }
                    p.runner.TickWindup(dt);
                    if (timer >= p.windupAt + pounce.pounceWindup)
                    {
                        p.leapt = true;
                        p.runner.Leap(CreateDamage());
                    }
                    continue;
                }
                if (!p.runner.Tick(dt)) allDone = false;
            }
            return allDone;
        }

        public override void BeginRecovery()
        {
            hitbox.Deactivate();
            poser.SetCrouch(recoveryCrouch);
            foreach (var p in pouncers)
            {
                if (p.phantom != null) p.phantom.Vanish();
            }
        }

        public override void End()
        {
            foreach (var p in pouncers)
            {
                p.runner.Stop();
                if (p.phantom != null) p.phantom.Vanish();
            }
            if (collar != null) collar.SetActive(false);
        }

        // 還沒起跳的幻影轉向玩家；only 不為 null 時只轉那一隻
        void FacePhantoms(float dt, Pouncer only)
        {
            if (Boss.Target == null) return;
            foreach (var p in pouncers)
            {
                if (p.phantom == null || p.leapt || !p.phantom.IsAlive) continue;
                if (only != null && p != only) continue;
                p.phantom.FaceToward(Boss.Target.position, phantomTurnSpeed, dt);
            }
        }

        void HandleShattered(BossPhantom phantom)
        {
            foreach (var p in pouncers)
            {
                if (p.phantom != phantom) continue;
                p.cancelled = true;
                p.runner.Stop();
            }
        }
    }
}
