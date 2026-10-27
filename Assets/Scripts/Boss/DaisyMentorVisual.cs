using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>菊花的佔位姿勢，只訂閱菊花事件；這裡都是純視覺參數。</summary>
    public class DaisyMentorVisual : MonoBehaviour
    {
        [SerializeField] DaisyMentor mentor;
        [SerializeField] Transform model;
        [SerializeField] Transform flower;
        [SerializeField] Transform petalHand;
        [SerializeField] LineRenderer vine;
        [SerializeField] float undergroundDepth = 0.7f;
        Vector3 rest, handRest, slapPoint;
        Quaternion flowerRest;

        void Awake()
        {
            rest = model.localPosition;
            handRest = petalHand.localPosition;
            flowerRest = flower.localRotation;
            model.gameObject.SetActive(false);
            vine.enabled = false;
        }
        void OnEnable()
        {
            mentor.OnActionChanged += HandleAction;
            mentor.OnActionProgress += HandleProgress;
            mentor.OnWhipHit += HandleHit;
            mentor.OnSlapTarget += HandleSlapTarget;
        }
        void OnDisable()
        {
            mentor.OnActionChanged -= HandleAction;
            mentor.OnActionProgress -= HandleProgress;
            mentor.OnWhipHit -= HandleHit;
            mentor.OnSlapTarget -= HandleSlapTarget;
        }
        void HandleAction(DaisyAction action)
        {
            model.gameObject.SetActive(action != DaisyAction.Hidden);
            vine.enabled = false;
            model.localPosition = rest;
            flower.localRotation = flowerRest;
            petalHand.localPosition = handRest;
        }
        void HandleProgress(DaisyAction action, float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            switch (action)
            {
                case DaisyAction.Emerge: case DaisyAction.PrankEmerge:
                    model.localPosition = rest - Vector3.up * (undergroundDepth * model.localScale.y * (1f - k)); break;
                case DaisyAction.Burrow: case DaisyAction.PrankBurrow:
                    model.localPosition = rest - Vector3.up * (undergroundDepth * model.localScale.y * k); break;
                case DaisyAction.Whip:
                    flower.localRotation = flowerRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 25f); break;
                case DaisyAction.Slap:
                    Vector3 reach = petalHand.parent.InverseTransformPoint(slapPoint + Vector3.up * (0.55f * model.lossyScale.y));
                    petalHand.localPosition = Vector3.Lerp(handRest, reach, Mathf.Sin(t * Mathf.PI)); break;
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
    }
}
