using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>爪子的姿勢。</summary>
    public enum PawPose
    {
        /// <summary>放在身前。</summary>
        Rest,
        /// <summary>舉過頭（預兆）。</summary>
        Raised,
        /// <summary>往前拍下（出招）。</summary>
        Strike,
    }

    /// <summary>
    /// OIIA 貓佔位模型的程序化姿勢（還沒有骨架與動畫）：舉爪、拍下、伏低扭屁股、抬頭、繞 Y 軸旋轉、撞暈後搖晃、受擊仰一下、倒地。
    /// 招式呼叫 SetXxx 指定目標姿勢，這裡在 LateUpdate 平滑過去；旋轉只轉 Model，不影響 Boss 根物件的面向。
    /// 關節由 Editor/BossBuilder 指派；這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class OiiaCatPoser : MonoBehaviour
    {
        [Header("關節（由 BossBuilder 指派）")]
        [Tooltip("整個模型的根；旋轉、伏低、抬頭都轉它（支點在腳底）")]
        public Transform model;
        public Transform pawL;
        public Transform pawR;

        [Header("爪子位置（右爪，Model 區域座標，+Z 前方；左爪左右對稱）")]
        public Vector3 pawRest = new Vector3(0.45f, 0.8f, 0.72f);
        public Vector3 pawRaised = new Vector3(0.6f, 2.75f, 0.35f);
        public Vector3 pawStrike = new Vector3(0.25f, 0.45f, 1.7f);
        [Tooltip("爪子移向一般姿勢的速度（越大越快）")]
        [Min(0f)] public float pawSpeed = 10f;
        [Tooltip("拍下時的速度")]
        [Min(0f)] public float pawStrikeSpeed = 45f;

        [Header("伏低、抬頭、扭屁股")]
        [Tooltip("完全伏低時的高度比例")]
        [Range(0.3f, 1f)] public float crouchScale = 0.7f;
        [Tooltip("完全伏低時往前傾（度）")]
        public float crouchPitch = 14f;
        [Tooltip("完全抬頭時往後仰（度）")]
        public float lookUpPitch = -20f;
        [Tooltip("扭屁股左右擺動（度）")]
        public float wiggleAngle = 14f;
        [Tooltip("扭屁股每秒幾下")]
        [Min(0f)] public float wiggleRate = 6f;
        [Tooltip("姿勢切換的速度（越大越快）")]
        [Min(0f)] public float poseSpeed = 12f;

        [Header("旋轉")]
        [Tooltip("停止旋轉後轉回正面的速度（度/秒）")]
        [Min(1f)] public float spinSettleSpeed = 900f;
        [Tooltip("轉速變化（度/秒²）")]
        [Min(1f)] public float spinAcceleration = 2400f;

        [Header("撞暈搖晃")]
        public float wobbleAngle = 12f;
        [Min(0f)] public float wobbleRate = 1.6f;

        [Header("受擊（Flinch）")]
        [Tooltip("被打時往後仰的角度（度），strength = 1 時")]
        public float flinchAngle = 9f;
        [Tooltip("被打時壓扁的比例（高度減少），strength = 1 時")]
        [Range(0f, 0.5f)] public float flinchSquash = 0.08f;
        [Tooltip("彈回原狀的時間（秒）")]
        [Min(0.01f)] public float flinchTime = 0.22f;

        [Header("倒地（血量歸零）")]
        [Tooltip("往旁邊倒下的角度（度）")]
        [Range(0f, 90f)] public float fallAngle = 80f;
        [Tooltip("倒下花的時間（秒），越倒越快")]
        [Min(0.01f)] public float fallTime = 0.7f;

        PawPose leftPose, rightPose;
        float crouchTarget, crouch, lookUpTarget, lookUp, wiggleWeight, wobbleWeight;
        bool wiggle, wobble;
        float spinTarget, spinSpeed, spinAngle, time;
        Vector3 flinchAxis;
        float flinchStrength, flinchTimer;
        Vector3 fallAxis = Vector3.forward;
        bool fallen;
        float fallWeight;

        /// <summary>目前轉速（度/秒）。</summary>
        public float SpinSpeed => spinSpeed;

        public void SetPaw(bool right, PawPose pose)
        {
            if (right) rightPose = pose;
            else leftPose = pose;
        }

        /// <summary>伏低程度 0..1。</summary>
        public void SetCrouch(float t) => crouchTarget = Mathf.Clamp01(t);

        /// <summary>抬頭程度 0..1。</summary>
        public void SetLookUp(float t) => lookUpTarget = Mathf.Clamp01(t);

        public void SetWiggle(bool on) => wiggle = on;

        public void SetWobble(bool on) => wobble = on;

        /// <summary>目標轉速（度/秒），以 spinAcceleration 加減速；0 = 停下並轉回正面。</summary>
        public void SetSpin(float degreesPerSecond) => spinTarget = degreesPerSecond;

        /// <summary>立刻設定轉速（不經過加速）。</summary>
        public void SetSpinImmediate(float degreesPerSecond) => spinTarget = spinSpeed = degreesPerSecond;

        /// <summary>被打一下：往 away（世界座標的水平方向，遠離攻擊者）仰一下、壓扁一下，flinchTime 秒內彈回。疊在其他姿勢上。</summary>
        public void Flinch(Vector3 away, float strength = 1f)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            // 繞「上 × 遠離方向」轉，頭往遠離攻擊者的方向倒；換成 Boss 根物件的區域座標（Model 是它的子物件）
            flinchAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away.normalized));
            flinchStrength = strength;
            flinchTimer = flinchTime;
        }

        /// <summary>倒地：往 away（世界座標的水平方向，通常是遠離最後一擊的攻擊者）倒下，fallTime 秒倒平；疊在其他姿勢上，ResetPose 不會扶起來。</summary>
        public void Fall(Vector3 away)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            fallAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away.normalized));
            fallen = true;
        }

        /// <summary>回到坐姿：爪子放下、不伏低、不抬頭、停止旋轉與搖晃。</summary>
        public void ResetPose()
        {
            leftPose = rightPose = PawPose.Rest;
            crouchTarget = lookUpTarget = 0f;
            wiggle = wobble = false;
            spinTarget = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || model == null) return;
            time += dt;
            float k = 1f - Mathf.Exp(-poseSpeed * dt);

            crouch = Mathf.Lerp(crouch, crouchTarget, k);
            lookUp = Mathf.Lerp(lookUp, lookUpTarget, k);
            wiggleWeight = Mathf.Lerp(wiggleWeight, wiggle ? 1f : 0f, k);
            wobbleWeight = Mathf.Lerp(wobbleWeight, wobble ? 1f : 0f, k);

            // 旋轉：停下時繼續往同一個方向轉到下一個整圈，才不會倒轉回去
            spinSpeed = Mathf.MoveTowards(spinSpeed, spinTarget, spinAcceleration * dt);
            if (Mathf.Abs(spinTarget) < 1e-3f && Mathf.Abs(spinSpeed) < spinSettleSpeed)
            {
                float rest = Mathf.Repeat(spinAngle, 360f);
                if (rest > 1e-2f)
                {
                    float step = Mathf.Max(Mathf.Abs(spinSpeed), spinSettleSpeed) * dt;
                    spinAngle = rest + step >= 360f ? 0f : rest + step;
                }
                spinSpeed = 0f;
            }
            else
            {
                spinAngle = Mathf.Repeat(spinAngle + spinSpeed * dt, 360f);
            }

            float pitch = crouch * crouchPitch + lookUp * lookUpPitch;
            float roll = Mathf.Sin(time * wiggleRate * Mathf.PI * 2f) * wiggleAngle * wiggleWeight
                         + Mathf.Sin(time * wobbleRate * Mathf.PI * 2f) * wobbleAngle * wobbleWeight;

            // 受擊：一開始最大，之後快速彈回（帶一點回彈）
            flinchTimer = Mathf.Max(0f, flinchTimer - dt);
            float u = 1f - flinchTimer / flinchTime;
            float f = flinchTimer > 0f ? Mathf.Cos(u * Mathf.PI * 1.5f) * (1f - u) * flinchStrength : 0f;
            Quaternion flinch = Quaternion.AngleAxis(flinchAngle * f, flinchAxis);

            fallWeight = Mathf.MoveTowards(fallWeight, fallen ? 1f : 0f, dt / fallTime);
            Quaternion fall = Quaternion.AngleAxis(fallAngle * fallWeight * fallWeight, fallAxis);

            model.localRotation = fall * flinch * Quaternion.Euler(pitch, spinAngle, roll);
            float squash = flinchSquash * Mathf.Max(0f, f);
            model.localScale = new Vector3(1f + squash * 0.5f, Mathf.Lerp(1f, crouchScale, crouch) * (1f - squash), 1f + squash * 0.5f);

            MovePaw(pawR, rightPose, 1f, dt);
            MovePaw(pawL, leftPose, -1f, dt);
        }

        void MovePaw(Transform paw, PawPose pose, float side, float dt)
        {
            if (paw == null) return;
            Vector3 target = pose == PawPose.Raised ? pawRaised : pose == PawPose.Strike ? pawStrike : pawRest;
            target.x *= side;
            float speed = pose == PawPose.Strike ? pawStrikeSpeed : pawSpeed;
            paw.localPosition = Vector3.Lerp(paw.localPosition, target, 1f - Mathf.Exp(-speed * dt));
        }
    }
}
