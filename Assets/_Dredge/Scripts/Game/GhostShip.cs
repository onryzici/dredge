using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Gece, panik %40'ı geçince sisin içinde beliren yeşil fenerli gemi. Tekneden
    /// 90–140 m uzakta yavaşça süzülür, yaklaşılırsa uzaklaşır, şafakta kaybolur.
    /// </summary>
    public class GhostShip : MonoBehaviour
    {
        public float appearPanic = 0.4f;
        public float speed = 2.2f;
        public Light lantern;
        public Renderer[] renderers;

        public bool Visible { get; private set; }
        float fade;
        Vector3 driftDir;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            SetVisible(false, true);
        }

        void Update()
        {
            var s = GameSession.Instance;
            if (s == null || s.boat == null || s.clock == null) return;
            bool want = s.clock.IsNight && s.panic != null && s.panic.Panic01 > appearPanic;

            if (want && !Visible)
            {
                // Teknenin görüş yönünde, uzakta doğ
                var b = s.boat.transform;
                float ang = Random.Range(-40f, 40f);
                var dir = Quaternion.Euler(0f, ang, 0f) * b.forward;
                transform.position = b.position + dir * Random.Range(100f, 140f);
                driftDir = Vector3.Cross(Vector3.up, dir).normalized * (Random.value < 0.5f ? 1f : -1f);
                transform.rotation = Quaternion.LookRotation(driftDir, Vector3.up);
                SetVisible(true, false);
            }
            else if (!want && Visible) SetVisible(false, false);

            fade = Mathf.MoveTowards(fade, Visible ? 1f : 0f, Time.deltaTime * 0.35f);
            if (fade <= 0f && !Visible) { foreach (var r in renderers) r.enabled = false; if (lantern) lantern.enabled = false; return; }

            foreach (var r in renderers) r.enabled = true;
            if (lantern) { lantern.enabled = true; lantern.intensity = 12f * fade * (0.8f + 0.2f * Mathf.Sin(Time.time * 3f)); }

            // Süzül; tekne yaklaşırsa kaç
            var to = transform.position - s.boat.transform.position; to.y = 0f;
            float d = to.magnitude;
            var move = driftDir * speed;
            if (d < 80f) move += to.normalized * speed * 1.5f;
            transform.position += move * Time.deltaTime;
            transform.position = new Vector3(transform.position.x, Mathf.Sin(Time.time * 0.6f) * 0.3f, transform.position.z);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move.normalized, Vector3.up), Time.deltaTime);

            // Yeşilimsi, yarı saydam his: renk soluk (materyal opak; ışık düşük)
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, Color.Lerp(new Color(0.1f, 0.14f, 0.16f), new Color(0.35f, 0.9f, 0.6f), fade * 0.8f));
                r.SetPropertyBlock(mpb);
            }
        }

        void SetVisible(bool v, bool instant)
        {
            Visible = v;
            if (instant) { fade = v ? 1f : 0f; foreach (var r in renderers) r.enabled = v; if (lantern) lantern.enabled = v; }
        }
    }
}
