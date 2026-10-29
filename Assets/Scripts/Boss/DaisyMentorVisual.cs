using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>菊花的佔位姿勢，只訂閱菊花事件；動作的時間與幅度讀 DaisyMentorData，其他是純視覺參數。</summary>
    public class DaisyMentorVisual : MonoBehaviour
    {
        [SerializeField] DaisyMentor mentor;
        [SerializeField] Transform model;
        [SerializeField] Transform flower;
        [SerializeField] Transform petalHand;
        [SerializeField] LineRenderer vine;
        [Tooltip("笑臉（咧開時放大）")]
        [SerializeField] Transform smile;
        [Tooltip("丟給玩家的骨頭（佔位模型）")]
        [SerializeField] Transform bone;
        [SerializeField] float undergroundDepth = 0.7f;
        [Tooltip("骨頭落點離目標根物件的高度 (m)")]
        [SerializeField] float boneTargetHeight = 0.8f;
        Vector3 rest, handRest, slapPoint, tossFrom, tossTarget, pointAt, smileRest;
        Quaternion flowerRest, modelRest;
        DaisyGesture gesture;
        float gestureClock;
        bool grinHeld;

        DaisyMentorData Data => mentor.Data;

        void Awake()
        {
            rest = model.localPosition;
            modelRest = model.localRotation;
            handRest = petalHand.localPosition;
            flowerRest = flower.localRotation;
            if (smile != null) smileRest = smile.localScale;
            model.gameObject.SetActive(false);
            vine.enabled = false;
            if (bone != null) bone.gameObject.SetActive(false);
        }
        void OnEnable()
        {
            mentor.OnActionChanged += HandleAction;
            mentor.OnActionProgress += HandleProgress;
            mentor.OnWhipHit += HandleHit;
            mentor.OnSlapTarget += HandleSlapTarget;
            mentor.OnTossTarget += HandleTossTarget;
            mentor.OnGesture += HandleGesture;
        }
        void OnDisable()
        {
            mentor.OnActionChanged -= HandleAction;
            mentor.OnActionProgress -= HandleProgress;
            mentor.OnWhipHit -= HandleHit;
            mentor.OnSlapTarget -= HandleSlapTarget;
            mentor.OnTossTarget -= HandleTossTarget;
            mentor.OnGesture -= HandleGesture;
        }
        void HandleAction(DaisyAction action)
        {
            model.gameObject.SetActive(action != DaisyAction.Hidden);
            vine.enabled = false;
            model.localPosition = rest;
            model.localRotation = modelRest;
            flower.localRotation = flowerRest;
            petalHand.localPosition = handRest;
            if (bone != null)
            {
                bone.gameObject.SetActive(action == DaisyAction.Toss);
                if (action == DaisyAction.Toss) { tossFrom = petalHand.position; tossTarget = tossFrom; bone.position = tossFrom; }
            }
            // 動作（冒出、鑽回、抽打…）會蓋掉說話的姿勢；鑽回或藏起來時連笑臉一起收
            gesture = DaisyGesture.None;
            if (action == DaisyAction.Hidden || action == DaisyAction.Burrow || action == DaisyAction.PrankBurrow) SetGrin(false);
        }
        void HandleProgress(DaisyAction action, float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            float depth = undergroundDepth * model.localScale.y;
            switch (action)
            {
                case DaisyAction.Emerge: case DaisyAction.PrankEmerge:
                    model.localPosition = rest - Vector3.up * (depth * (1f - k)); break;
                case DaisyAction.Burrow: case DaisyAction.PrankBurrow:
                    model.localPosition = rest - Vector3.up * (depth * k); break;
                case DaisyAction.Peek:
                    model.localPosition = rest - Vector3.up * (depth * (1f - Data.peekHeight * Mathf.Sin(t * Mathf.PI))); break;
                case DaisyAction.Whip:
                    flower.localRotation = flowerRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 25f); break;
                case DaisyAction.Slap:
                    Vector3 reach = petalHand.parent.InverseTransformPoint(slapPoint + Vector3.up * (0.55f * model.lossyScale.y));
                    petalHand.localPosition = Vector3.Lerp(handRest, reach, Mathf.Sin(t * Mathf.PI)); break;
                case DaisyAction.Toss:
                    petalHand.localPosition = handRest + Vector3.up * (0.12f * Mathf.Sin(Mathf.Min(1f, t * 3f) * Mathf.PI));
                    if (bone != null)
                    {
                        Vector3 to = tossTarget + Vector3.up * boneTargetHeight;
                        bone.position = Vector3.Lerp(tossFrom, to, t) + Vector3.up * (Data.tossHeight * 4f * t * (1f - t));
                        bone.rotation = Quaternion.Euler(0f, 0f, t * 720f) * Quaternion.LookRotation(to - tossFrom + Vector3.forward * 1e-4f);
                    }
                    break;
            }
        }
        void HandleHit(Core.HitReport hit)
        {
            if (hit.hurtbox == null) return;
            vine.enabled = true;
            vine.positionCount = 3;
            Vector3 start = mentor.transform.position + Vector3.up * 0.15f;
            Vector3 end = hit.hurtbox.Center;
            vine.SetPosition(0, start);
            vine.SetPosition(1, (start + end) * 0.5f + Vector3.up * 0.8f);
            vine.SetPosition(2, end);
        }
        void HandleSlapTarget(Vector3 point) => slapPoint = point;
        void HandleTossTarget(Vector3 point) => tossTarget = point;

        void HandleGesture(DaisyGesture value, Vector3 point)
        {
            gesture = value;
            gestureClock = 0f;
            pointAt = point;
            SetGrin(value == DaisyGesture.BigGrin);
            if (value == DaisyGesture.None) ResetPose();
        }

        void SetGrin(bool value)
        {
            grinHeld = value;
            if (smile != null) smile.localScale = value ? smileRest * Data.grinScale : smileRest;
        }

        void ResetPose()
        {
            model.localRotation = modelRest;
            flower.localRotation = flowerRest;
            petalHand.localPosition = handRest;
            if (mentor.Action == DaisyAction.Idle) model.localPosition = rest;
        }

        // 說話的姿勢只在待機時做；動作開始時 HandleAction 會收掉
        void LateUpdate()
        {
            if (gesture == DaisyGesture.None || mentor.Action != DaisyAction.Idle) return;
            gestureClock += Time.deltaTime;
            float length = gesture == DaisyGesture.Spin ? Data.spinTime : Data.gestureTime;
            float t = Mathf.Clamp01(gestureClock / length);
            float wave = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
            ResetPose();
            switch (gesture)
            {
                case DaisyGesture.Wave:
                    petalHand.localPosition = handRest + new Vector3(0f, 0.12f, 0f) * wave;
                    flower.localRotation = flowerRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 12f * wave);
                    break;
                case DaisyGesture.TiltHead:
                    flower.localRotation = flowerRest * Quaternion.Euler(0f, 0f, Data.tiltAngle * wave);
                    break;
                case DaisyGesture.PatChest:
                    petalHand.localPosition = Vector3.Lerp(handRest, new Vector3(0.02f, -0.02f, 0.06f), Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)));
                    break;
                case DaisyGesture.Spin:
                    model.localRotation = modelRest * Quaternion.Euler(0f, Mathf.SmoothStep(0f, 360f, t), 0f);
                    break;
                case DaisyGesture.Point:
                    Vector3 dir = pointAt - flower.position;
                    if (dir.sqrMagnitude > 1e-4f)
                    {
                        Vector3 local = petalHand.parent.InverseTransformDirection(dir.normalized);
                        petalHand.localPosition = handRest + local * (Data.pointReach * Mathf.Min(1f, t * 4f));
                    }
                    break;
                case DaisyGesture.Clap:
                    petalHand.localPosition = handRest + new Vector3(-0.1f, 0.05f, 0.04f) * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f));
                    model.localPosition = rest + Vector3.up * (0.03f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f)));
                    break;
                case DaisyGesture.Snicker:
                    flower.localRotation = flowerRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 14f) * 6f * wave);
                    break;
                case DaisyGesture.Surprised:
                    model.localPosition = rest + Vector3.up * (Data.surpriseJump * Mathf.Sin(Mathf.Min(1f, t * 2.5f) * Mathf.PI));
                    flower.localRotation = flowerRest * Quaternion.Euler(-12f * wave, 0f, 0f);
                    break;
                case DaisyGesture.BigGrin:
                    flower.localRotation = flowerRest * Quaternion.Euler(-8f * Mathf.Min(1f, t * 3f), 0f, 0f);
                    break;
                case DaisyGesture.Nod:
                    flower.localRotation = flowerRest * Quaternion.Euler(Data.nodAngle * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)), 0f, 0f);
                    break;
            }
            // 笑臉咧開維持到下一個動作；其他做完就回原姿勢
            if (t >= 1f && !grinHeld) { gesture = DaisyGesture.None; ResetPose(); }
        }
    }
}
