using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 頭暈的佔位演出：頭上一圈星星繞著轉、上下浮動，身體用 OiiaCatPoser 的搖晃（撞暈搖晃）左右晃。
    /// 只訂閱 BossStagger 的 OnStunned／OnStunEnded；星星由 Editor/BossBuilder 建在 Model 底下（跟著身體一起晃）。
    /// 這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossStunVisual : MonoBehaviour
    {
        [SerializeField] BossStagger stagger;
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("星星的父物件，頭暈時顯示並繞 Y 軸轉")]
        [SerializeField] Transform starsRoot;
        [Tooltip("繞圈速度（度/秒）")]
        [SerializeField] float spinSpeed = 220f;
        [Tooltip("每顆星星自轉速度（度/秒）")]
        [SerializeField] float starSpin = 360f;
        [Tooltip("上下浮動的高度 (m)")]
        [Min(0f)] [SerializeField] float bobHeight = 0.08f;
        [Tooltip("上下浮動每秒幾下")]
        [Min(0f)] [SerializeField] float bobRate = 2f;
        [Tooltip("出現時從 0 放大到原本大小的時間（秒）")]
        [Min(0.01f)] [SerializeField] float popTime = 0.15f;

        Transform[] stars;
        Vector3[] starRest;
        Vector3 rootScale;
        float timer;

        void Awake()
        {
            if (starsRoot == null) return;
            rootScale = starsRoot.localScale;
            stars = new Transform[starsRoot.childCount];
            starRest = new Vector3[stars.Length];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = starsRoot.GetChild(i);
                starRest[i] = stars[i].localPosition;
            }
            starsRoot.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (stagger == null) return;
            stagger.OnStunned += HandleStunned;
            stagger.OnStunEnded += HandleStunEnded;
        }

        void OnDisable()
        {
            if (stagger == null) return;
            stagger.OnStunned -= HandleStunned;
            stagger.OnStunEnded -= HandleStunEnded;
        }

        void HandleStunned(float duration)
        {
            timer = 0f;
            if (starsRoot != null) starsRoot.gameObject.SetActive(true);
            if (poser != null) poser.SetWobble(true);
        }

        void HandleStunEnded(StunEndReason reason)
        {
            if (starsRoot != null) starsRoot.gameObject.SetActive(false);
            if (poser != null) poser.SetWobble(false);
        }

        void LateUpdate()
        {
            if (starsRoot == null || !starsRoot.gameObject.activeSelf) return;
            float dt = Time.deltaTime;
            timer += dt;

            starsRoot.localRotation = Quaternion.Euler(0f, timer * spinSpeed, 0f);
            starsRoot.localScale = rootScale * Mathf.SmoothStep(0f, 1f, timer / popTime);
            for (int i = 0; i < stars.Length; i++)
            {
                float phase = (timer * bobRate + (float)i / stars.Length) * Mathf.PI * 2f;
                stars[i].localPosition = starRest[i] + Vector3.up * (Mathf.Sin(phase) * bobHeight);
                stars[i].Rotate(0f, starSpin * dt, 0f, Space.Self);
            }
        }
    }
}
