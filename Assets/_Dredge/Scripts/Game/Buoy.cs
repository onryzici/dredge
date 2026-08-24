using UnityEngine;

namespace Dredge.Game
{
    /// <summary>Dalgayla sallanan şamandıra; tepesindeki ışık gece yanıp söner.</summary>
    public class Buoy : MonoBehaviour
    {
        public Light lamp;
        public float blinkPeriod = 2.2f;
        public Color color = new Color(1f, 0.35f, 0.25f);
        Vector3 basePos;

        void Start() { basePos = transform.position; if (lamp) lamp.color = color; }

        void Update()
        {
            var water = DredgeLook.WaterSurface.Instance;
            float h = water != null ? water.GetHeight(basePos) : 0f;
            transform.position = new Vector3(basePos.x, h, basePos.z);
            transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.1f) * 6f, 0f, Mathf.Cos(Time.time * 0.9f) * 6f);
            if (lamp)
            {
                var s = GameSession.Instance;
                float night = s != null && s.clock != null ? s.clock.Night01 : 0f;
                bool on = Mathf.Repeat(Time.time, blinkPeriod) < 0.35f;
                lamp.intensity = on ? 1.5f * Mathf.Clamp01(night * 1.5f + 0.15f) : 0f;
            }
        }
    }
}
