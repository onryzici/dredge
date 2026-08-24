using UnityEngine;

namespace Dredge
{
    /// <summary>
    /// Tek silindirli balıkçı motoru "pat-pat"ı — gerçek zamanlı üretim.
    /// Devir, gaz ile 8 Hz (rölanti) → 24 Hz arasında yumuşakça gezer; her vuruş
    /// kısa bir bas "tak" (55–90 Hz sinüs) + boğuk gürültü patlamasıdır.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineAudio : MonoBehaviour
    {
        public BoatController boat;
        [Range(0f, 1f)] public float volume = 0.35f;
        [Range(0f, 1f)] public float idleVolume = 0.45f;
        public float idleRate = 8f;
        public float fullRate = 24f;
        [Tooltip("Devrin gaza tepki hızı (1/sn).")]
        public float rpmResponse = 1.6f;

        AudioSource source;
        int sampleRate;
        System.Random rng = new System.Random(77);

        volatile float throttle01;
        float rate, phase, thumpPhase, burst, lp, lp2;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            sampleRate = AudioSettings.outputSampleRate;
            var clip = AudioClip.Create("EngineProcedural", sampleRate * 2, 1, sampleRate, true, OnRead);
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.minDistance = 6f;
            source.maxDistance = 60f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.playOnAwake = true;
            source.Play();
            rate = idleRate;
        }

        void Update()
        {
            if (boat == null) boat = GetComponentInParent<BoatController>();
            throttle01 = boat != null ? boat.NormalizedSpeed : 0f;
        }

        void OnRead(float[] data)
        {
            float dt = 1f / sampleRate;
            float target = Mathf.Lerp(idleRate, fullRate, throttle01);
            float vol = volume * Mathf.Lerp(idleVolume, 1f, throttle01);

            for (int i = 0; i < data.Length; i++)
            {
                rate += (target - rate) * rpmResponse * dt;

                phase += rate * dt;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    burst = 0.9f + (float)rng.NextDouble() * 0.3f;   // her vuruş biraz farklı
                    thumpPhase = 0f;
                }

                // Bas "tak": hızla sönen sinüs, devirle biraz tizleşir
                float thumpFreq = Mathf.Lerp(55f, 90f, throttle01);
                thumpPhase += thumpFreq * dt;
                float env = burst;
                float thump = Mathf.Sin(thumpPhase * 6.2831853f) * env;

                // Boğuk egzoz gürültüsü
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.06f;      // ~450 Hz
                lp2 += (lp - lp2) * 0.06f;
                float exhaust = lp2 * env * 3.0f;

                burst *= 0.9994f;            // vuruş zarfı (~30 ms)

                float sample = thump * 0.55f + exhaust * 0.6f;
                data[i] = Mathf.Clamp(sample * vol, -1f, 1f);
            }
        }
    }
}
