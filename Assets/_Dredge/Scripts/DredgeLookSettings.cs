using DredgeLook;
using UnityEngine;

namespace Dredge
{
    /// <summary>
    /// Sahnenin bütün görsel ayarları tek yerde. Sahne kurucusu (Dredge ▸ 1) ve
    /// Dredge Look entegrasyonu (Dredge ▸ 2) değerleri BURADAN okur; yani sahneyi
    /// yeniden kursan da ayarların kaybolmaz.
    ///
    /// İş akışı:
    ///   • Oynarken/editörde "Dredge Atmosphere" Live Values ve materyalleri Inspector'dan değiştir.
    ///   • Beğendiysen  Dredge ▸ 3) Sahnedeki Ayarları Kaydet  → bu dosyaya yazılır.
    ///   • Ya da doğrudan bu asset'i düzenle ve  Dredge ▸ 2) Dredge Look Uygula.
    /// </summary>
    [CreateAssetMenu(menuName = "Dredge/Look Settings", fileName = "DredgeLookSettings")]
    public class DredgeLookSettings : ScriptableObject
    {
        [Header("Atmosfer (güneş, gökyüzü, ambient, sis, su renkleri, post)")]
        public AtmosphereValues atmosphere = AtmosphereValues.Default;

        [Header("Su — atmosferin sürmediği parametreler")]
        [Range(0f, 1f)] public float crestFoam = 0f;
        [Range(0.01f, 0.6f)] public float foamSoftness = 0.35f;
        [Range(0f, 2f)] public float normalStrength = 0.35f;
        [Range(4f, 512f)] public float specularPower = 260f;
        [Range(0.001f, 0.5f)] public float specularSoftness = 0.06f;
        [Range(0f, 1f)] public float alphaShallow = 0.35f;
        [Range(20f, 600f)] public float waveFadeDistance = 140f;
        [Tooltip("Fresnel dışında her yerde görünen yansıma tabanı.")]
        [Range(0f, 1f)] public float reflectionBase = 0.22f;
        [Range(0f, 1f)] public float planarStrength = 1f;
        [Range(0f, 0.2f)] public float planarDistortion = 0.08f;
        [Range(0.1f, 1f)] public float reflectionTextureScale = 0.5f;

        [Header("Kaya / ağaç / prop gölgeleme (StylizedLit)")]
        [Range(1, 5)] public int bands = 3;
        [Range(0.001f, 0.5f)] public float bandSoftness = 0.08f;
        [Range(0f, 1f)] public float lightWrap = 0.10f;
        [Range(0f, 1f)] public float shadowStrength = 0.85f;
        [Range(0f, 2f)] public float ambientStrength = 0.75f;
        [Range(0f, 2f)] public float rimStrength = 0.10f;
        public Color rockColor = new Color(0.56f, 0.58f, 0.62f);
        public Color pineColor = new Color(0.16f, 0.30f, 0.24f);
        public Color autumnColor = new Color(0.78f, 0.46f, 0.18f);
        public Color trunkColor = new Color(0.30f, 0.22f, 0.17f);
        public Color cloudColor = new Color(0.82f, 0.85f, 0.90f);   // 0.97 bloom eşiğini aşıp uzakta beyaz toplar yapıyordu
        [Range(0f, 1f)] public float cloudShadowStrength = 0.4f;

        [Header("Kamera")]
        [Range(20f, 70f)] public float fieldOfView = 45f;
        [Range(200f, 3000f)] public float farClip = 1200f;

        [Header("Bulutlar")]
        public bool clouds = true;
        [Range(0, 40)] public int cloudCount = 16;
        public Vector2 cloudHeight = new Vector2(45f, 110f);
        public Vector2 cloudDistance = new Vector2(140f, 420f);
        public Vector2 cloudScale = new Vector2(1.0f, 2.4f);
        public Vector3 cloudWind = new Vector3(0.9f, 0f, 0.35f);

        [Header("Ses")]
        [Range(0f, 1f)] public float seaVolume = 0.55f;
        [Range(0f, 1f)] public float engineVolume = 0.35f;

        [Header("Gün döngüsü (GameClock)")]
        [Tooltip("Bir oyun gününün gerçek süresi (saniye). 480 = 8 dk.")]
        public float dayLengthSeconds = 480f;
        [Tooltip("Oyun başlangıç saati.")]
        [Range(0f, 24f)] public float startHour = 9f;
        public AtmosphereValues duskAtmosphere = DuskAtmosphere();
        public AtmosphereValues nightAtmosphere = NightAtmosphere();

        public static AtmosphereValues DuskAtmosphere()
        {
            var v = DefaultAtmosphere();
            v.sunElevation = 7f; v.sunAzimuth = -110f;
            v.sunColor = AtmosphereValues.Hex("FF9A4A"); v.sunIntensity = 1.2f;
            v.shadowStrength = 0.7f; v.shadowTint = AtmosphereValues.Hex("3A3F6B");
            v.skyZenith = AtmosphereValues.Hex("26365C"); v.skyHorizon = AtmosphereValues.Hex("E9A165"); v.skyGround = AtmosphereValues.Hex("C48B6E");
            v.horizonPower = 2.8f; v.horizonGlow = 0.6f;
            v.sunDiscColor = AtmosphereValues.Hex("FFD9A0"); v.sunGlowColor = AtmosphereValues.Hex("FF9C52"); v.sunGlowFalloff = 14f;
            v.starStrength = 0.15f;
            v.ambientSky = AtmosphereValues.Hex("5A6C92"); v.ambientEquator = AtmosphereValues.Hex("8A7A80"); v.ambientGround = AtmosphereValues.Hex("2B2630");
            v.fogColor = AtmosphereValues.Hex("D9A277"); v.fogDensity = 0.006f;
            v.waterShallow = AtmosphereValues.Hex("4E6E7A"); v.waterDeep = AtmosphereValues.Hex("14202E");
            v.waterFoam = AtmosphereValues.Hex("F3DCC6"); v.waterSpecular = AtmosphereValues.Hex("FFC489");
            v.waterSpecularIntensity = 2.4f;
            v.contrast = 14f; v.saturation = 6f;
            v.splitShadows = AtmosphereValues.Hex("2B3B6E"); v.splitHighlights = AtmosphereValues.Hex("FFC98A");
            v.bloomIntensity = 0.3f; v.bloomThreshold = 1.05f; v.vignetteIntensity = 0.28f;
            return v;
        }

        public static AtmosphereValues NightAtmosphere()
        {
            var v = DefaultAtmosphere();
            v.sunElevation = 42f; v.sunAzimuth = 140f;
            v.sunColor = AtmosphereValues.Hex("A8BEDC"); v.sunIntensity = 0.75f;
            v.shadowStrength = 0.5f; v.shadowTint = AtmosphereValues.Hex("18243A");
            v.skyZenith = AtmosphereValues.Hex("0A1424"); v.skyHorizon = AtmosphereValues.Hex("22324A"); v.skyGround = AtmosphereValues.Hex("1A2434");
            v.horizonPower = 1.8f; v.horizonGlow = 0.22f;
            v.sunDiscColor = AtmosphereValues.Hex("E8F0FF"); v.sunDiscSize = 0.010f;
            v.sunGlowColor = AtmosphereValues.Hex("6E86AE"); v.sunGlowFalloff = 60f;
            v.starStrength = 0.08f;
            v.ambientSky = AtmosphereValues.Hex("2C3E5E"); v.ambientEquator = AtmosphereValues.Hex("232E40"); v.ambientGround = AtmosphereValues.Hex("10161F");
            v.ambientIntensity = 1.1f;
            v.fogColor = AtmosphereValues.Hex("16212F"); v.fogDensity = 0.006f;
            v.waterShallow = AtmosphereValues.Hex("1E3A48"); v.waterDeep = AtmosphereValues.Hex("050A12");
            v.waterFoam = AtmosphereValues.Hex("93A8B8"); v.waterSpecular = AtmosphereValues.Hex("BFD4F0");
            v.waterSpecularIntensity = 1.4f; v.waterReflection = 0.35f;
            v.postExposure = 0.6f; v.contrast = 10f; v.saturation = -10f;
            v.splitShadows = AtmosphereValues.Hex("16223A"); v.splitHighlights = AtmosphereValues.Hex("9FB6D8");
            v.bloomIntensity = 0.45f; v.bloomThreshold = 0.9f; v.vignetteIntensity = 0.34f; v.filmGrain = 0.16f;
            return v;
        }

        /// <summary>Referans (IMG_1929) için kalibre edilmiş varsayılan atmosfer.</summary>
        public static AtmosphereValues DefaultAtmosphere()
        {
            var v = AtmosphereValues.Default;
            v.sunElevation = 30f;  v.sunAzimuth = -50f;
            v.sunColor = AtmosphereValues.Hex("FFF1DA"); v.sunIntensity = 1.45f;
            v.shadowStrength = 0.9f; v.shadowTint = AtmosphereValues.Hex("3E5B7A");

            v.skyZenith = AtmosphereValues.Hex("2E7DC6");
            v.skyHorizon = AtmosphereValues.Hex("8DBBE2");
            v.skyGround = AtmosphereValues.Hex("8DBBE2");
            v.horizonPower = 4.5f; v.horizonGlow = 0.12f; v.starStrength = 0f;

            v.ambientSky = AtmosphereValues.Hex("6F9CC4");
            v.ambientEquator = AtmosphereValues.Hex("7E8C99");
            v.ambientGround = AtmosphereValues.Hex("343B42");
            v.ambientIntensity = 0.65f;

            v.fogColor = AtmosphereValues.Hex("8DBBE2"); v.fogDensity = 0.003f;

            v.waterShallow = AtmosphereValues.Hex("2F7A96");
            v.waterDeep = AtmosphereValues.Hex("12365A");
            v.waterFoam = AtmosphereValues.Hex("E6EEF2");
            v.waterDepthFade = 4f; v.waterFoamDistance = 0.5f;
            v.waveAmplitude = 0.22f; v.waveSpeed = 0.55f;
            v.waterReflection = 0.45f; v.waterSpecularIntensity = 1.2f;

            v.postExposure = 0f; v.contrast = 16f; v.saturation = 12f;
            v.splitShadows = AtmosphereValues.Hex("2E4A6E");
            v.splitHighlights = AtmosphereValues.Hex("FFE6C2");
            v.bloomIntensity = 0.18f; v.bloomThreshold = 1.2f;
            v.vignetteIntensity = 0.22f; v.filmGrain = 0.06f;
            return v;
        }

        void Reset()
        {
            atmosphere = DefaultAtmosphere();
            duskAtmosphere = DuskAtmosphere();
            nightAtmosphere = NightAtmosphere();
        }
    }
}
