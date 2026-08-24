using UnityEngine;

namespace Dredge.Game
{
    /// <summary>Deniz feneri: gece dönen spot ışığı + tepede parlayan küre.</summary>
    public class Lighthouse : MonoBehaviour
    {
        public Light beam;
        public Renderer lamp;
        public float rpm = 4f;
        public float dayIntensity = 0f, nightIntensity = 40f;

        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Update()
        {
            var s = GameSession.Instance;
            float night = s != null && s.clock != null ? s.clock.Night01 : 0f;
            if (beam != null)
            {
                beam.transform.Rotate(Vector3.up, rpm * 6f * Time.deltaTime, Space.World);
                beam.intensity = Mathf.Lerp(dayIntensity, nightIntensity, Mathf.Clamp01(night * 1.5f));
                beam.enabled = beam.intensity > 0.5f;
            }
            if (lamp != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                lamp.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, Color.Lerp(new Color(0.9f, 0.85f, 0.7f), new Color(3f, 2.4f, 1.4f), night));
                lamp.SetPropertyBlock(mpb);
            }
        }
    }
}
