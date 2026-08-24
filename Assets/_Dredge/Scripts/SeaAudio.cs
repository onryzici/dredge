using UnityEngine;

namespace Dredge
{
    /// <summary>
    /// Deniz ambiyansı — statik bir wav döngüsü yerine gerçek zamanlı üretiliyor,
    /// dolayısıyla tekrar eden bir "loop" duyulmuyor. Üç katman:
    ///   1) derin uğultu   : kahverengi gürültü, ~180 Hz altı, yavaş şişip inen
    ///   2) su yıkanması   : pembe gürültü bandı, iki yavaş LFO ile dalga "nefesi"
    ///   3) gövde şapırtısı: tekne hızıyla artan kısa, parlak gürültü patlamaları
    /// Ses iş parçacığında çalışır; Unity API'si çağrılmaz.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SeaAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.55f;
        [Tooltip("Dalga nefesinin derinliği (0 = düz uğultu).")]
        [Range(0f, 1f)] public float swell = 0.7f;
        [Tooltip("Gövde şapırtısı için tekne (opsiyonel).")]
        public BoatController boat;

        AudioSource source;
        int sampleRate;
        System.Random rng = new System.Random(1234);

        // filtre durumları
        float brown, lp1, lp2, lp3, hp1;
        float pinkB0, pinkB1, pinkB2;
        double phaseA, phaseB, phaseC;
        float lapEnv, lapTimer;
        volatile float speed01;    // ana iş parçacığından yazılır

        void Awake()
        {
            source = GetComponent<AudioSource>();
            sampleRate = AudioSettings.outputSampleRate;
            // Akış klibi: içerik her istekte OnRead ile üretilir.
            var clip = AudioClip.Create("SeaAmbienceProcedural", sampleRate * 2, 1, sampleRate, true, OnRead);
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 200;
            source.playOnAwake = true;
            source.Play();
        }

        void Update()
        {
            if (boat == null) boat = FindAnyObjectByType<BoatController>();
            speed01 = boat != null ? boat.NormalizedSpeed : 0f;
        }

        float White() => (float)(rng.NextDouble() * 2.0 - 1.0);

        void OnRead(float[] data)
        {
            float dt = 1f / sampleRate;
            float v = volume;
            float s01 = speed01;

            for (int i = 0; i < data.Length; i++)
            {
                float w = White();

                // 1) Kahverengi gürültü (integratör) + düşük geçiren → derin uğultu
                brown = Mathf.Clamp(brown + w * 0.02f, -1f, 1f) * 0.998f;
                lp1 += (brown - lp1) * 0.020f;              // ~150 Hz

                // 2) Pembe gürültü (Paul Kellet yaklaşık) → su yıkanması
                pinkB0 = 0.99765f * pinkB0 + w * 0.0990460f;
                pinkB1 = 0.96300f * pinkB1 + w * 0.2965164f;
                pinkB2 = 0.57000f * pinkB2 + w * 1.0526913f;
                float pink = (pinkB0 + pinkB1 + pinkB2 + w * 0.1848f) * 0.12f;
                lp2 += (pink - lp2) * 0.12f;                // ~900 Hz
                hp1 += (lp2 - hp1) * 0.004f;                // DC/çok bas kes
                float wash = lp2 - hp1;

                // Dalga nefesi: iki uyumsuz LFO (0.07 / 0.11 Hz) + yavaş rastgele sürüklenme
                phaseA += 0.07 * dt; phaseB += 0.113 * dt; phaseC += 0.019 * dt;
                float breath = 0.5f + 0.5f * (float)(0.6 * System.Math.Sin(phaseA * 6.2831853)
                                                     + 0.4 * System.Math.Sin(phaseB * 6.2831853 + 1.3));
                float drift = 0.85f + 0.15f * (float)System.Math.Sin(phaseC * 6.2831853);
                float swellGain = Mathf.Lerp(1f, 0.35f + 0.9f * breath, swell) * drift;

                // 3) Gövde şapırtısı: hızla artan, kısa parlak patlamalar
                lapTimer -= dt;
                if (lapTimer <= 0f)
                {
                    lapTimer = Mathf.Lerp(0.9f, 0.18f, s01) * (0.6f + (float)rng.NextDouble() * 0.8f);
                    lapEnv = Mathf.Lerp(0.0f, 0.5f, s01) * (0.5f + (float)rng.NextDouble() * 0.5f);
                }
                lapEnv *= 0.9992f;
                lp3 += (w - lp3) * 0.35f;                   // ~3 kHz
                float lap = (w - lp3) * lapEnv;             // yüksek geçiren parlaklık

                float sample = lp1 * 1.6f * (0.7f + 0.3f * breath)
                             + wash * 1.1f * swellGain
                             + lap * 0.8f;

                data[i] = Mathf.Clamp(sample * v, -1f, 1f);
            }
        }
    }
}
