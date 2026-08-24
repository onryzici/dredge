using System;
using UnityEngine;

namespace DredgeLook
{
    /// <summary>
    /// Bir atmosfer durumunun (gün ışığı / gün batımı / gece / fırtına) tüm görsel
    /// parametreleri. Preset'ler bunu içerir, StylizedAtmosphere bunu sahneye uygular.
    /// İki AtmosphereValues arasında Lerp yapılabildiği için gün döngüsü kurmak kolaydır.
    /// </summary>
    [Serializable]
    public struct AtmosphereValues
    {
        // ─────────────────────────────── GÜNEŞ / ANA IŞIK ───────────────────────────────
        [Header("Güneş")]
        [Tooltip("Ufuktan yükseklik. 12-25° dramatik ve DREDGE'e yakın. 45+ düz görünür.")]
        [Range(-20f, 90f)] public float sunElevation;
        [Tooltip("Pusula yönü (Y ekseni rotasyonu).")]
        [Range(-180f, 180f)] public float sunAzimuth;
        [ColorUsage(false)] public Color sunColor;
        [Range(0f, 4f)] public float sunIntensity;
        [Tooltip("Gölge koyuluğu. 1 = tam siyah gölge. DREDGE için 0.65-0.85.")]
        [Range(0f, 1f)] public float shadowStrength;
        [Tooltip("Gölgelerin rengi. ASLA siyah seçme — ışığın tamamlayıcısını seç (sıcak güneş → mavi-mor gölge).")]
        [ColorUsage(false)] public Color shadowTint;

        // ─────────────────────────────── GÖKYÜZÜ ───────────────────────────────
        [Header("Gökyüzü")]
        [ColorUsage(false)] public Color skyZenith;
        [ColorUsage(false)] public Color skyHorizon;
        [ColorUsage(false)] public Color skyGround;
        [Tooltip("Ufuk bandının darlığı. Yüksek = keskin ufuk çizgisi.")]
        [Range(0.2f, 8f)] public float horizonPower;
        [Tooltip("Ufuk çizgisindeki parlak bandın gücü.")]
        [Range(0f, 2f)] public float horizonGlow;
        [ColorUsage(false)] public Color sunDiscColor;
        [Range(0f, 0.05f)] public float sunDiscSize;
        [Range(0.0005f, 0.02f)] public float sunDiscSoftness;
        [ColorUsage(false)] public Color sunGlowColor;
        [Tooltip("Güneş çevresindeki halenin yayılımı. Yüksek = dar hale.")]
        [Range(2f, 512f)] public float sunGlowFalloff;
        [Range(0f, 3f)] public float skyExposure;
        [Tooltip("Gece preset'lerinde 0.5-1.0 yap, gündüz 0 bırak.")]
        [Range(0f, 2f)] public float starStrength;

        // ─────────────────────────────── AMBIENT ───────────────────────────────
        [Header("Ambient (Gradient)")]
        [ColorUsage(false)] public Color ambientSky;
        [ColorUsage(false)] public Color ambientEquator;
        [ColorUsage(false)] public Color ambientGround;
        [Range(0f, 2f)] public float ambientIntensity;

        // ─────────────────────────────── SİS ───────────────────────────────
        [Header("Sis — rengi HER ZAMAN skyHorizon'a yakın olmalı")]
        [ColorUsage(false)] public Color fogColor;
        [Tooltip("Exponential Squared yoğunluğu. Açık hava 0.010, puslu 0.020, fırtına 0.038.")]
        [Range(0f, 0.08f)] public float fogDensity;

        // ─────────────────────────────── SU ───────────────────────────────
        [Header("Su")]
        [ColorUsage(false)] public Color waterShallow;
        [ColorUsage(false)] public Color waterDeep;
        [ColorUsage(false)] public Color waterFoam;
        [ColorUsage(false)] public Color waterSpecular;
        [Tooltip("Sığdan derine geçiş mesafesi (metre).")]
        [Range(0.5f, 30f)] public float waterDepthFade;
        [Tooltip("Kıyıdaki köpük bandının genişliği (metre).")]
        [Range(0f, 8f)] public float waterFoamDistance;
        [Range(0f, 3f)] public float waveAmplitude;
        [Range(0f, 3f)] public float waveSpeed;
        [Tooltip("Sudaki güneş yolunun parlaklığı. DREDGE'de tek parlak yüzey budur.")]
        [Range(0f, 8f)] public float waterSpecularIntensity;
        [Tooltip("Yansımanın gücü (fresnel ile ufka doğru artar).")]
        [Range(0f, 1f)] public float waterReflection;

        // ─────────────────────────────── POST PROCESSING ───────────────────────────────
        [Header("Post Processing")]
        [Range(-2f, 2f)] public float postExposure;
        [Range(-40f, 60f)] public float contrast;
        [Range(-60f, 60f)] public float saturation;
        [ColorUsage(false)] public Color splitShadows;
        [ColorUsage(false)] public Color splitHighlights;
        [Range(-100f, 100f)] public float splitBalance;
        [Range(0f, 2f)] public float bloomIntensity;
        [Range(0.5f, 3f)] public float bloomThreshold;
        [Range(0f, 1f)] public float vignetteIntensity;
        [Range(0f, 1f)] public float filmGrain;

        // ─────────────────────────────── VARSAYILAN (GÜNDÜZ) ───────────────────────────────
        public static AtmosphereValues Default => new AtmosphereValues
        {
            sunElevation = 22f,
            sunAzimuth = -35f,
            sunColor = Hex("FFF3DC"),
            sunIntensity = 1.55f,
            shadowStrength = 0.75f,
            shadowTint = Hex("3E5B7A"),

            skyZenith = Hex("4B8CC4"),
            skyHorizon = Hex("C7DBE6"),
            skyGround = Hex("B6C5CD"),
            horizonPower = 1.8f,
            horizonGlow = 0.35f,
            sunDiscColor = Hex("FFF8E8"),
            sunDiscSize = 0.006f,
            sunDiscSoftness = 0.0025f,
            sunGlowColor = Hex("FFE9C4"),
            sunGlowFalloff = 48f,
            skyExposure = 1f,
            starStrength = 0f,

            ambientSky = Hex("7FA6C4"),
            ambientEquator = Hex("9BA9AE"),
            ambientGround = Hex("4B535A"),
            ambientIntensity = 1f,

            fogColor = Hex("C2D4DE"),
            fogDensity = 0.011f,

            waterShallow = Hex("4E8C93"),
            waterDeep = Hex("12242F"),
            waterFoam = Hex("EAF3F5"),
            waterSpecular = Hex("FFF6E2"),
            waterDepthFade = 6.5f,
            waterFoamDistance = 1.4f,
            waveAmplitude = 1f,
            waveSpeed = 1f,
            waterSpecularIntensity = 2.4f,
            waterReflection = 0.55f,

            postExposure = 0.05f,
            contrast = 12f,
            saturation = 4f,
            splitShadows = Hex("2E4A6E"),
            splitHighlights = Hex("FFE6C2"),
            splitBalance = 0f,
            bloomIntensity = 0.22f,
            bloomThreshold = 1.15f,
            vignetteIntensity = 0.24f,
            filmGrain = 0.12f
        };

        /// <summary>"RRGGBB" → Color. Editör dışında da çalışır.</summary>
        public static Color Hex(string hex)
        {
            float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            return new Color(r, g, b, 1f);
        }

        public static AtmosphereValues Lerp(AtmosphereValues a, AtmosphereValues b, float t)
        {
            t = Mathf.Clamp01(t);
            AtmosphereValues o;

            o.sunElevation = Mathf.Lerp(a.sunElevation, b.sunElevation, t);
            o.sunAzimuth = Mathf.LerpAngle(a.sunAzimuth, b.sunAzimuth, t);
            o.sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
            o.sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            o.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t);
            o.shadowTint = Color.Lerp(a.shadowTint, b.shadowTint, t);

            o.skyZenith = Color.Lerp(a.skyZenith, b.skyZenith, t);
            o.skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t);
            o.skyGround = Color.Lerp(a.skyGround, b.skyGround, t);
            o.horizonPower = Mathf.Lerp(a.horizonPower, b.horizonPower, t);
            o.horizonGlow = Mathf.Lerp(a.horizonGlow, b.horizonGlow, t);
            o.sunDiscColor = Color.Lerp(a.sunDiscColor, b.sunDiscColor, t);
            o.sunDiscSize = Mathf.Lerp(a.sunDiscSize, b.sunDiscSize, t);
            o.sunDiscSoftness = Mathf.Lerp(a.sunDiscSoftness, b.sunDiscSoftness, t);
            o.sunGlowColor = Color.Lerp(a.sunGlowColor, b.sunGlowColor, t);
            o.sunGlowFalloff = Mathf.Lerp(a.sunGlowFalloff, b.sunGlowFalloff, t);
            o.skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t);
            o.starStrength = Mathf.Lerp(a.starStrength, b.starStrength, t);

            o.ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
            o.ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            o.ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
            o.ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t);

            o.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            o.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);

            o.waterShallow = Color.Lerp(a.waterShallow, b.waterShallow, t);
            o.waterDeep = Color.Lerp(a.waterDeep, b.waterDeep, t);
            o.waterFoam = Color.Lerp(a.waterFoam, b.waterFoam, t);
            o.waterSpecular = Color.Lerp(a.waterSpecular, b.waterSpecular, t);
            o.waterDepthFade = Mathf.Lerp(a.waterDepthFade, b.waterDepthFade, t);
            o.waterFoamDistance = Mathf.Lerp(a.waterFoamDistance, b.waterFoamDistance, t);
            o.waveAmplitude = Mathf.Lerp(a.waveAmplitude, b.waveAmplitude, t);
            o.waveSpeed = Mathf.Lerp(a.waveSpeed, b.waveSpeed, t);
            o.waterSpecularIntensity = Mathf.Lerp(a.waterSpecularIntensity, b.waterSpecularIntensity, t);
            o.waterReflection = Mathf.Lerp(a.waterReflection, b.waterReflection, t);

            o.postExposure = Mathf.Lerp(a.postExposure, b.postExposure, t);
            o.contrast = Mathf.Lerp(a.contrast, b.contrast, t);
            o.saturation = Mathf.Lerp(a.saturation, b.saturation, t);
            o.splitShadows = Color.Lerp(a.splitShadows, b.splitShadows, t);
            o.splitHighlights = Color.Lerp(a.splitHighlights, b.splitHighlights, t);
            o.splitBalance = Mathf.Lerp(a.splitBalance, b.splitBalance, t);
            o.bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t);
            o.bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t);
            o.vignetteIntensity = Mathf.Lerp(a.vignetteIntensity, b.vignetteIntensity, t);
            o.filmGrain = Mathf.Lerp(a.filmGrain, b.filmGrain, t);

            return o;
        }
    }
}
