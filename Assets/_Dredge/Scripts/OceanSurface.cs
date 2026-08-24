using UnityEngine;

namespace Dredge
{
    /// <summary>Tek bir Gerstner dalgası. Shader ile birebir aynı parametreler.</summary>
    [System.Serializable]
    public struct GerstnerWave
    {
        public Vector2 direction;
        [Range(0f, 1f)] public float steepness;
        public float wavelength;

        public Vector4 Packed => new Vector4(direction.x, direction.y, steepness, wavelength);
    }

    /// <summary>
    /// Denizin tek doğruluk kaynağı. Dalga parametrelerini hem shader'a global olarak
    /// yollar hem de aynı matematiği CPU'da çözer; böylece tekne, gördüğü dalganın
    /// tam olarak üstünde yüzer — göz kararı bir sinüsle değil.
    /// </summary>
    [ExecuteAlways]
    public class OceanSurface : MonoBehaviour
    {
        public static OceanSurface Instance { get; private set; }

        [SerializeField] float timeScale = 1f;

        [Header("Takip")]
        [Tooltip("Genelde tekne. Izgara, hücre boyutuna yuvarlanarak hedefin altında kalır; " +
                 "dalgalar dünya konumundan hesaplandığı için kayma görünmez.")]
        public Transform follow;
        [SerializeField] float cellSize = 1.75f;
        [SerializeField]
        GerstnerWave[] waves =
        {
            // Genlik = diklik * dalgaboyu / 2π.  Toplam ≈ 1.25 m — sakin, ağır bir deniz.
            // Dikliklerin toplamı 1'i geçerse dalga kendi üstüne katlanır.
            //
            // Dalga boyları bilerek birbirinin katı DEĞİL ve yönler geniş bir yelpazeye
            // yayılmış; dört uyumlu dalga gözle görülür şekilde tekrar eden bir desen
            // üretiyordu.
            new GerstnerWave { direction = new Vector2( 1.00f,  0.10f), steepness = 0.130f, wavelength = 31f  },
            new GerstnerWave { direction = new Vector2( 0.78f, -0.62f), steepness = 0.100f, wavelength = 19f  },
            new GerstnerWave { direction = new Vector2( 0.55f,  0.83f), steepness = 0.085f, wavelength = 12.5f},
            new GerstnerWave { direction = new Vector2(-0.35f,  0.94f), steepness = 0.065f, wavelength = 7.9f },
            new GerstnerWave { direction = new Vector2(-0.86f, -0.51f), steepness = 0.050f, wavelength = 5.1f },
            new GerstnerWave { direction = new Vector2( 0.20f, -0.98f), steepness = 0.035f, wavelength = 3.3f },
        };

        const int Slots = 8;                       // shader tarafındaki WAVE_SLOTS ile aynı
        static readonly int WavesId = Shader.PropertyToID("_Waves");
        static readonly int WaveTime = Shader.PropertyToID("_WaveTime");
        static readonly Vector4[] packed = new Vector4[Slots];

        public float Time01 => (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * timeScale;

        void OnEnable()
        {
            Instance = this;
            PushToShader();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Update() => PushToShader();

        void LateUpdate()
        {
            if (follow == null || cellSize <= 0f) return;
            Vector3 p = follow.position;
            transform.position = new Vector3(Mathf.Round(p.x / cellSize) * cellSize,
                                             transform.position.y,
                                             Mathf.Round(p.z / cellSize) * cellSize);
        }

        void PushToShader()
        {
            for (int i = 0; i < Slots; i++)
                packed[i] = waves != null && i < waves.Length ? waves[i].Packed : new Vector4(0f, 0f, 0f, 1f);

            Shader.SetGlobalVectorArray(WavesId, packed);
            Shader.SetGlobalFloat(WaveTime, Time01);
        }

        // ------------------------------------------------------------ dalga matematiği

        /// <summary>Düzlemdeki bir noktanın dalgayla ötelenmiş hâli (shader ile aynı formül).</summary>
        public Vector3 Displace(Vector3 flatPos)
        {
            float t = Time01;
            Vector3 result = flatPos;
            if (waves == null) return result;

            foreach (var w in waves)
            {
                if (w.wavelength <= 0.01f) continue;
                float k = 2f * Mathf.PI / w.wavelength;
                float c = Mathf.Sqrt(9.8f / k);
                Vector2 d = w.direction.sqrMagnitude < 1e-6f ? Vector2.right : w.direction.normalized;
                float f = k * (d.x * flatPos.x + d.y * flatPos.z - c * t);
                float a = w.steepness / k;

                result.x += d.x * (a * Mathf.Cos(f));
                result.y += a * Mathf.Sin(f);
                result.z += d.y * (a * Mathf.Cos(f));
            }

            // Shader'daki gürültü detayının birebir aynısı — tekne dalganın
            // görünen yüzeyinden 40 cm sapmasın diye burada da uygulanıyor.
            var dn = new Vector2(flatPos.x * 0.11f + t * 0.035f, flatPos.z * 0.11f - t * 0.021f);
            result.y += (ValueNoise(dn) - 0.5f) * 0.55f
                      + (ValueNoise(dn * 2.7f + new Vector2(13f, 13f)) - 0.5f) * 0.22f;
            return result;
        }

        // ---- shader'daki Hash21 / ValueNoise ile aynı ----
        static float Frac(float v) => v - Mathf.Floor(v);

        static float Hash21(Vector2 p)
        {
            p = new Vector2(Frac(p.x * 127.1f), Frac(p.y * 311.7f));
            float add = Vector2.Dot(p, new Vector2(p.x + 42.13f, p.y + 42.13f));
            p += new Vector2(add, add);
            return Frac(p.x * p.y);
        }

        static float ValueNoise(Vector2 p)
        {
            var i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            var f = p - i;
            f = new Vector2(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));
            float a = Hash21(i);
            float b = Hash21(i + Vector2.right);
            float c = Hash21(i + Vector2.up);
            float d = Hash21(i + Vector2.one);
            return Mathf.Lerp(Mathf.Lerp(a, b, f.x), Mathf.Lerp(c, d, f.x), f.y);
        }

        /// <summary>
        /// Verilen dünya XZ'sindeki su yüksekliği. Gerstner yatayda da ötelediği için
        /// tersi analitik değil; birkaç sabit nokta yinelemesiyle çözüyoruz.
        /// </summary>
        public float SampleHeight(Vector3 worldPos, int iterations = 4)
        {
            Vector3 guess = new Vector3(worldPos.x, 0f, worldPos.z);
            for (int i = 0; i < iterations; i++)
            {
                Vector3 displaced = Displace(guess);
                guess.x += worldPos.x - displaced.x;
                guess.z += worldPos.z - displaced.z;
            }
            return Displace(guess).y + transform.position.y;
        }
    }
}
