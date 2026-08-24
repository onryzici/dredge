using DredgeLook;
using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Gün döngüsü. Saat 0–24 arasında akar; balık tutmak zamanı ileri sarar.
    /// Atmosfer, DredgeLookSettings'teki gündüz / alacakaranlık / gece değerleri
    /// arasında saate göre karıştırılır (DredgeLookLive'dan sonra, atmosferden önce).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class GameClock : MonoBehaviour
    {
        public DredgeLookSettings settings;
        public StylizedAtmosphere atmosphere;

        [Tooltip("Oyun saati (0–24).")]
        public float hour = 9f;
        public int day = 1;
        [Tooltip("Zaman aksın mı (menü/duraklatma için).")]
        public bool running = true;

        public float DayLength => settings != null ? settings.dayLengthSeconds : 480f;

        /// <summary>0 = tam gündüz, 1 = tam gece.</summary>
        public float Night01 { get; private set; }
        public bool IsNight => Night01 > 0.55f;

        void Start()
        {
            if (settings != null) hour = settings.startHour;
        }

        float pendingHours;   // yumuşak ileri sarma kuyruğu (balık tutma vb.)

        void Update()
        {
            if (running && Application.isPlaying)
                Advance(24f / DayLength * Time.deltaTime);

            // Ani gün/gece geçişi olmasın: bekleyen saatleri ~2 sn'ye yayarak akıt
            if (pendingHours > 0f)
            {
                float step = Mathf.Min(pendingHours, Mathf.Max(0.02f, pendingHours) * Time.deltaTime * 2.2f + 0.002f);
                pendingHours -= step;
                Advance(step);
            }

            ApplyAtmosphere();
        }

        /// <summary>Saati yumuşakça ilerletir (birkaç saniyeye yayar).</summary>
        public void AdvanceSmooth(float hours) { pendingHours += hours; }

        /// <summary>Saati ilerletir (oyun saati cinsinden).</summary>
        public void Advance(float hours)
        {
            hour += hours;
            while (hour >= 24f) { hour -= 24f; day++; }
        }

        public string TimeString
        {
            get
            {
                int h = Mathf.FloorToInt(hour);
                int m = Mathf.FloorToInt((hour - h) * 60f);
                return $"{h:00}:{m:00}";
            }
        }

        void ApplyAtmosphere()
        {
            if (settings == null || atmosphere == null) return;

            // 06–08 şafak, 08–17 gündüz, 17–19 alacakaranlık, 19–21 geceye geçiş, 21–05 gece, 05–06 şafağa geçiş
            AtmosphereValues day = settings.atmosphere, dusk = settings.duskAtmosphere, night = settings.nightAtmosphere;
            AtmosphereValues v;
            float h = hour;
            if (h >= 8f && h < 17f) { v = day; Night01 = 0f; }
            else if (h >= 17f && h < 19f) { float t = (h - 17f) / 2f; v = AtmosphereValues.Lerp(day, dusk, Smooth(t)); Night01 = 0.2f * t; }
            else if (h >= 19f && h < 21f) { float t = (h - 19f) / 2f; v = AtmosphereValues.Lerp(dusk, night, Smooth(t)); Night01 = 0.2f + 0.8f * t; }
            else if (h >= 21f || h < 5f) { v = night; Night01 = 1f; }
            else if (h >= 5f && h < 6.5f) { float t = (h - 5f) / 1.5f; v = AtmosphereValues.Lerp(night, dusk, Smooth(t)); Night01 = 1f - 0.8f * t; }
            else { float t = (h - 6.5f) / 1.5f; v = AtmosphereValues.Lerp(dusk, day, Smooth(t)); Night01 = 0.2f * (1f - t); }

            // Panik sisi/doygunluğu (varsa)
            var panic = GameSession.Instance != null ? GameSession.Instance.panic : null;
            if (panic != null && panic.Panic01 > 0.01f)
            {
                float p = panic.Panic01;
                v.fogDensity += 0.018f * p;
                v.saturation -= 35f * p;
                v.vignetteIntensity = Mathf.Lerp(v.vignetteIntensity, 0.55f, p);
                v.contrast += 10f * p;
            }

            atmosphere.usePresets = false;
            atmosphere.values = v;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
