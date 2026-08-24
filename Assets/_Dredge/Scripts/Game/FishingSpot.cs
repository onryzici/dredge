using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Suda balık noktası: dışa doğru genişleyip sönen iki halka (su titreşimi hissi),
    /// arada sıçrayan balık serpintisi. Yaklaşınca (E) mini-oyun başlar.
    /// </summary>
    public class FishingSpot : MonoBehaviour
    {
        public Habitat habitat = Habitat.Coastal;
        public int stock = 3;
        public float interactRadius = 12f;
        public bool nightOnly;

        [Header("Görsel")]
        public Transform[] rings;          // 2 halka, farklı fazda
        public ParticleSystem splash;
        public float cycle = 2.6f;         // bir halkanın doğup sönme süresi (s)
        public float maxScale = 1.6f;

        Renderer[] ringRenderers;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        float splashTimer;

        public bool Depleted => stock <= 0;

        void Awake()
        {
            if (rings != null) { ringRenderers = new Renderer[rings.Length]; for (int i = 0; i < rings.Length; i++) ringRenderers[i] = rings[i] != null ? rings[i].GetComponent<Renderer>() : null; }
            mpb = new MaterialPropertyBlock();
            splashTimer = Random.Range(1f, 3f);
        }

        public bool IsAvailable
        {
            get
            {
                var clock = GameSession.Instance != null ? GameSession.Instance.clock : null;
                return !Depleted && (!nightOnly || (clock != null && clock.IsNight));
            }
        }

        void Update()
        {
            bool active = IsAvailable;
            // Uzaktan belli belirsiz, yaklaşınca net (60 m → 12 m)
            float dist = GameSession.Instance != null && GameSession.Instance.boat != null ? Vector3.Distance(GameSession.Instance.boat.transform.position, transform.position) : 30f;
            float near = Mathf.Clamp01(1f - (dist - 12f) / 60f);
            float vis = 0.25f + 0.75f * near;
            var water = DredgeLook.WaterSurface.Instance;
            var basePos = transform.position;
            if (water != null) basePos.y = water.GetHeight(basePos) + 0.06f;

            var tint = habitat == Habitat.Night ? new Color(1f, 0.45f, 0.4f) : habitat == Habitat.Deep ? new Color(0.8f, 0.95f, 1f) : new Color(0.95f, 1f, 0.97f);

            if (rings != null && (ringRenderers == null || ringRenderers.Length != rings.Length))
            {
                ringRenderers = new Renderer[rings.Length];
                for (int i = 0; i < rings.Length; i++) ringRenderers[i] = rings[i] != null ? rings[i].GetComponent<Renderer>() : null;
                if (mpb == null) mpb = new MaterialPropertyBlock();
            }
            if (rings != null)
            for (int i = 0; i < rings.Length; i++)
            {
                var r = rings[i]; if (r == null) continue;
                float t = Mathf.Repeat(Time.time / cycle + i / (float)rings.Length, 1f);   // 0→1
                float s = Mathf.Lerp(0.35f, maxScale, t);
                float a = Mathf.Sin(t * Mathf.PI);                                        // doğ-sön
                a *= a;
                r.position = basePos;
                r.localScale = active ? Vector3.one * s : Vector3.one * 0.001f;
                r.Rotate(Vector3.up, 6f * Time.deltaTime, Space.World);
                if (ringRenderers[i] != null)
                {
                    ringRenderers[i].GetPropertyBlock(mpb);
                    var c = tint; c.a = a * 0.7f * vis * (active ? 1f : 0f);
                    mpb.SetColor(BaseColor, c);
                    ringRenderers[i].SetPropertyBlock(mpb);
                }
            }

            if (splash != null && active)
            {
                splash.transform.position = basePos + Vector3.up * 0.1f;
                splashTimer -= Time.deltaTime;
                if (splashTimer <= 0f)
                {
                    splash.Emit(Random.Range(4, 9));
                    splashTimer = Random.Range(1.4f, 3.8f);
                }
            }
        }
    }
}
