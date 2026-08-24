using UnityEngine;

namespace DredgeLook
{
    /// <summary>
    /// Shader'daki Gerstner dalga matematiğinin CPU karşılığı.
    /// Dalga parametrelerini materyalden okur → görüntü ile fizik ASLA ayrışmaz.
    /// Tekne sallanması (BuoyantObject) ve dalga yüksekliği sorguları bunu kullanır.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Dredge Look/Water Surface")]
    public class WaterSurface : MonoBehaviour
    {
        public static WaterSurface Instance { get; private set; }

        [Tooltip("Dredge/StylizedWater materyali. Dalga değerleri buradan okunur.")]
        public Material waterMaterial;

        [Tooltip("Su düzleminin dünya yüksekliği (dalgasız seviye).")]
        public float seaLevel = 0f;

        [Tooltip("Yükseklik çözümündeki iterasyon sayısı. 3 yeterli.")]
        [Range(1, 6)] public int solverIterations = 3;

        Vector4[] _waves = new Vector4[4];
        float _amplitude = 1f;
        float _speed = 1f;

        void OnEnable()
        {
            Instance = this;
            ReadMaterial();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            ReadMaterial();
        }

        void ReadMaterial()
        {
            if (waterMaterial == null) return;
            _waves[0] = waterMaterial.HasProperty("_WaveA") ? waterMaterial.GetVector("_WaveA") : new Vector4(1, 0.15f, 0.22f, 22f);
            _waves[1] = waterMaterial.HasProperty("_WaveB") ? waterMaterial.GetVector("_WaveB") : new Vector4(0.7f, 0.7f, 0.16f, 13f);
            _waves[2] = waterMaterial.HasProperty("_WaveC") ? waterMaterial.GetVector("_WaveC") : new Vector4(-0.5f, 0.9f, 0.10f, 7f);
            _waves[3] = waterMaterial.HasProperty("_WaveD") ? waterMaterial.GetVector("_WaveD") : new Vector4(0.2f, -1f, 0.06f, 3.5f);
            _amplitude = waterMaterial.HasProperty("_WaveAmplitude") ? waterMaterial.GetFloat("_WaveAmplitude") : 1f;
            _speed = waterMaterial.HasProperty("_WaveSpeed") ? waterMaterial.GetFloat("_WaveSpeed") : 1f;
        }

        static float ShaderTime =>
#if UNITY_EDITOR
            Application.isPlaying ? Time.timeSinceLevelLoad : (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            Time.timeSinceLevelLoad;
#endif

        /// <summary>Bir Gerstner dalgasının yer değiştirmesi (shader ile birebir aynı formül).</summary>
        Vector3 GerstnerOffset(Vector4 wave, Vector3 p, float t)
        {
            float steepness = wave.z * _amplitude;
            float wavelength = Mathf.Max(wave.w, 0.01f);
            float k = 2f * Mathf.PI / wavelength;
            float c = Mathf.Sqrt(9.8f / k);
            Vector2 d = new Vector2(wave.x, wave.y);
            if (d.sqrMagnitude < 1e-8f) d = Vector2.right;
            d.Normalize();

            float f = k * (d.x * p.x + d.y * p.z - c * t);
            float a = steepness / k;

            return new Vector3(d.x * (a * Mathf.Cos(f)), a * Mathf.Sin(f), d.y * (a * Mathf.Cos(f)));
        }

        Vector3 TotalOffset(Vector3 p, float t)
        {
            Vector3 o = Vector3.zero;
            for (int i = 0; i < 4; i++) o += GerstnerOffset(_waves[i], p, t);
            return o;
        }

        /// <summary>Verilen dünya XZ konumunda su yüzeyinin yüksekliği.</summary>
        public float GetHeight(Vector3 worldPos)
        {
            float t = ShaderTime * _speed;

            // Gerstner yatayda da kaydırdığı için hedef XZ'ye ulaşan başlangıç
            // noktasını bulmak üzere birkaç iterasyon yapıyoruz.
            Vector3 guess = new Vector3(worldPos.x, seaLevel, worldPos.z);
            for (int i = 0; i < solverIterations; i++)
            {
                Vector3 o = TotalOffset(guess, t);
                guess.x = worldPos.x - o.x;
                guess.z = worldPos.z - o.z;
            }
            return seaLevel + TotalOffset(guess, t).y;
        }

        /// <summary>Su yüzeyinin normali (sonlu fark ile).</summary>
        public Vector3 GetNormal(Vector3 worldPos, float step = 0.6f)
        {
            float hL = GetHeight(worldPos + Vector3.left * step);
            float hR = GetHeight(worldPos + Vector3.right * step);
            float hB = GetHeight(worldPos + Vector3.back * step);
            float hF = GetHeight(worldPos + Vector3.forward * step);

            Vector3 dx = new Vector3(2f * step, hR - hL, 0f);
            Vector3 dz = new Vector3(0f, hF - hB, 2f * step);
            return Vector3.Normalize(Vector3.Cross(dz, dx));
        }
    }
}
