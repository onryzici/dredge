using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DredgeLook
{
    /// <summary>
    /// DREDGE tarzı atmosferin tek kontrol paneli.
    /// Güneş, gökyüzü, ambient, sis, su ve post-processing'i tek yerden sürer.
    /// [ExecuteAlways] sayesinde Play'e basmadan editörde canlı değişir.
    ///
    /// AYAR SIRASI (bozma): gökyüzü → sis → güneş açısı → ambient → su → post.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Dredge Look/Stylized Atmosphere")]
    public class StylizedAtmosphere : MonoBehaviour
    {
        // ─────────────────────────── KAYNAKLAR ───────────────────────────
        [Header("Sahne Referansları")]
        [Tooltip("Ana directional light. Boşsa sahnedeki ilk directional light bulunur.")]
        public Light sun;
        [Tooltip("Skybox materyali (Dredge/StylizedSky shader'ı). Lighting > Environment'a da atanır.")]
        public Material skyMaterial;
        [Tooltip("Global Volume'un profili. Post-processing buradan sürülür.")]
        public VolumeProfile volumeProfile;
        [Tooltip("Dredge/StylizedWater kullanan materyaller.")]
        public List<Material> waterMaterials = new List<Material>();

        // ─────────────────────────── PRESET ───────────────────────────
        [Header("Preset")]
        [Tooltip("Açıkken aşağıdaki iki preset arasında Blend ile geçiş yapılır. Kapalıyken 'Live Values' elle ayarlanır.")]
        public bool usePresets = true;
        public AtmospherePreset presetA;
        public AtmospherePreset presetB;
        [Range(0f, 1f)]
        [Tooltip("0 = Preset A, 1 = Preset B. Gün döngüsü için bunu zamanla sür.")]
        public float blend = 0f;

        [Header("Live Values (usePresets kapalıyken kullanılır)")]
        public AtmosphereValues values = AtmosphereValues.Default;

        // ─────────────────────────── NE UYGULANSIN ───────────────────────────
        [Header("Neler Uygulansın")]
        public bool applySun = true;
        public bool applySky = true;
        public bool applyAmbient = true;
        public bool applyFog = true;
        public bool applyWater = true;
        public bool applyPostProcessing = true;

        [Header("Gelişmiş")]
        [Tooltip("Renkler yıkanmış/soluk görünüyorsa bunu aç. Linear color space'te gamma dönüşümünü zorlar.")]
        public bool linearColorFix = false;
        [Tooltip("Her frame uygula. Kapalıyken sadece değer değiştiğinde uygulanır (daha performanslı).")]
        public bool continuousUpdate = true;

        // ─────────────────────────── SHADER ID'LERİ ───────────────────────────
        static readonly int ID_SunDirection = Shader.PropertyToID("_DL_SunDirection");
        static readonly int ID_SunColor = Shader.PropertyToID("_DL_SunColor");
        static readonly int ID_ShadowTint = Shader.PropertyToID("_DL_ShadowTint");
        static readonly int ID_FogColor = Shader.PropertyToID("_DL_FogColor");
        static readonly int ID_G_SkyZenith = Shader.PropertyToID("_DL_SkyZenith");
        static readonly int ID_G_SkyHorizon = Shader.PropertyToID("_DL_SkyHorizon");
        static readonly int ID_G_SkyGround = Shader.PropertyToID("_DL_SkyGround");
        static readonly int ID_G_SunGlowColor = Shader.PropertyToID("_DL_SunGlowColor");
        static readonly int ID_G_SkyParams = Shader.PropertyToID("_DL_SkyParams");

        static readonly int ID_ZenithColor = Shader.PropertyToID("_ZenithColor");
        static readonly int ID_HorizonColor = Shader.PropertyToID("_HorizonColor");
        static readonly int ID_GroundColor = Shader.PropertyToID("_GroundColor");
        static readonly int ID_HorizonPower = Shader.PropertyToID("_HorizonPower");
        static readonly int ID_HorizonGlow = Shader.PropertyToID("_HorizonGlow");
        static readonly int ID_SunDiscColor = Shader.PropertyToID("_SunDiscColor");
        static readonly int ID_SunDiscSize = Shader.PropertyToID("_SunDiscSize");
        static readonly int ID_SunDiscSoftness = Shader.PropertyToID("_SunDiscSoftness");
        static readonly int ID_SunGlowColor = Shader.PropertyToID("_SunGlowColor");
        static readonly int ID_SunGlowFalloff = Shader.PropertyToID("_SunGlowFalloff");
        static readonly int ID_SkyExposure = Shader.PropertyToID("_Exposure");
        static readonly int ID_StarStrength = Shader.PropertyToID("_StarStrength");

        static readonly int ID_ShallowColor = Shader.PropertyToID("_ShallowColor");
        static readonly int ID_DeepColor = Shader.PropertyToID("_DeepColor");
        static readonly int ID_FoamColor = Shader.PropertyToID("_FoamColor");
        static readonly int ID_SpecularColor = Shader.PropertyToID("_SpecularColor");
        static readonly int ID_DepthFade = Shader.PropertyToID("_DepthFade");
        static readonly int ID_FoamDistance = Shader.PropertyToID("_FoamDistance");
        static readonly int ID_WaveAmplitude = Shader.PropertyToID("_WaveAmplitude");
        static readonly int ID_WaveSpeed = Shader.PropertyToID("_WaveSpeed");
        static readonly int ID_SpecularIntensity = Shader.PropertyToID("_SpecularIntensity");
        static readonly int ID_ReflectionStrength = Shader.PropertyToID("_ReflectionStrength");

        /// <summary>Şu an geçerli olan değerler (preset blend'i dahil).</summary>
        public AtmosphereValues Current
        {
            get
            {
                if (!usePresets) return values;
                if (presetA == null && presetB == null) return values;
                if (presetB == null) return presetA.values;
                if (presetA == null) return presetB.values;
                return AtmosphereValues.Lerp(presetA.values, presetB.values, blend);
            }
        }

        void OnEnable()
        {
            if (sun == null) sun = FindMainDirectionalLight();
            Apply();
        }

        void OnValidate()
        {
            // Inspector'da bir değer değiştiğinde uygula.
            // OnValidate serialization/import sırasında da çağrılabildiği için
            // (orada ScriptableObject.CreateInstance yasaktır) editörde erteliyoruz.
            if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                Apply();
                if (!Application.isPlaying && volumeProfile != null)
                    UnityEditor.EditorUtility.SetDirty(volumeProfile);
            };
#else
            Apply();
#endif
        }

        void Update()
        {
            if (continuousUpdate) Apply();
        }

        // ─────────────────────────── ANA UYGULAMA ───────────────────────────
        public void Apply()
        {
            var v = Current;

            if (applySun) ApplySun(v);
            if (applySky) ApplySky(v);
            if (applyAmbient) ApplyAmbient(v);
            if (applyFog) ApplyFog(v);
            if (applyWater) ApplyWater(v);
            if (applyPostProcessing) ApplyPost(v);

            // Shader global'leri (StylizedLit, StylizedSky ve StylizedWater kullanır)
            Vector3 dir = sun != null ? -sun.transform.forward : DirectionFromAngles(v.sunElevation, v.sunAzimuth);
            Shader.SetGlobalVector(ID_SunDirection, new Vector4(dir.x, dir.y, dir.z, 0f));
            Shader.SetGlobalColor(ID_SunColor, C(v.sunColor) * v.sunIntensity);
            Shader.SetGlobalColor(ID_ShadowTint, C(v.shadowTint));
            Shader.SetGlobalColor(ID_FogColor, C(v.fogColor));

            // Su, skybox ile AYNI gradyanı yansıtsın diye gökyüzü değerleri de global:
            Shader.SetGlobalColor(ID_G_SkyZenith, C(v.skyZenith));
            Shader.SetGlobalColor(ID_G_SkyHorizon, C(v.skyHorizon));
            Shader.SetGlobalColor(ID_G_SkyGround, C(v.skyGround));
            Shader.SetGlobalColor(ID_G_SunGlowColor, C(v.sunGlowColor));
            Shader.SetGlobalVector(ID_G_SkyParams,
                new Vector4(v.horizonPower, v.horizonGlow, v.sunGlowFalloff, v.skyExposure));
        }

        void ApplySun(AtmosphereValues v)
        {
            if (sun == null) sun = FindMainDirectionalLight();
            if (sun == null) return;

            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(v.sunElevation, v.sunAzimuth, 0f);
            sun.color = C(v.sunColor);
            sun.intensity = v.sunIntensity;
            sun.shadows = v.shadowStrength > 0.001f ? LightShadows.Soft : LightShadows.None;
            sun.shadowStrength = v.shadowStrength;
            // Bantlı gölgelemede shadow bias çok görünür olur; makul bir varsayılan:
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
        }

        void ApplySky(AtmosphereValues v)
        {
            if (skyMaterial == null) return;

            skyMaterial.SetColor(ID_ZenithColor, C(v.skyZenith));
            skyMaterial.SetColor(ID_HorizonColor, C(v.skyHorizon));
            skyMaterial.SetColor(ID_GroundColor, C(v.skyGround));
            skyMaterial.SetFloat(ID_HorizonPower, v.horizonPower);
            skyMaterial.SetFloat(ID_HorizonGlow, v.horizonGlow);
            skyMaterial.SetColor(ID_SunDiscColor, C(v.sunDiscColor));
            skyMaterial.SetFloat(ID_SunDiscSize, v.sunDiscSize);
            skyMaterial.SetFloat(ID_SunDiscSoftness, v.sunDiscSoftness);
            skyMaterial.SetColor(ID_SunGlowColor, C(v.sunGlowColor));
            skyMaterial.SetFloat(ID_SunGlowFalloff, v.sunGlowFalloff);
            skyMaterial.SetFloat(ID_SkyExposure, v.skyExposure);
            if (skyMaterial.HasProperty(ID_StarStrength))
                skyMaterial.SetFloat(ID_StarStrength, v.starStrength);

            if (RenderSettings.skybox != skyMaterial)
                RenderSettings.skybox = skyMaterial;
        }

        void ApplyAmbient(AtmosphereValues v)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C(v.ambientSky) * v.ambientIntensity;
            RenderSettings.ambientEquatorColor = C(v.ambientEquator) * v.ambientIntensity;
            RenderSettings.ambientGroundColor = C(v.ambientGround) * v.ambientIntensity;
            RenderSettings.reflectionIntensity = 0.7f;
        }

        void ApplyFog(AtmosphereValues v)
        {
            RenderSettings.fog = v.fogDensity > 0.00001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = C(v.fogColor);
            RenderSettings.fogDensity = v.fogDensity;
        }

        void ApplyWater(AtmosphereValues v)
        {
            for (int i = 0; i < waterMaterials.Count; i++)
            {
                var m = waterMaterials[i];
                if (m == null) continue;

                if (m.HasProperty(ID_ShallowColor)) m.SetColor(ID_ShallowColor, C(v.waterShallow));
                if (m.HasProperty(ID_DeepColor)) m.SetColor(ID_DeepColor, C(v.waterDeep));
                if (m.HasProperty(ID_FoamColor)) m.SetColor(ID_FoamColor, C(v.waterFoam));
                if (m.HasProperty(ID_SpecularColor)) m.SetColor(ID_SpecularColor, C(v.waterSpecular));
                if (m.HasProperty(ID_DepthFade)) m.SetFloat(ID_DepthFade, v.waterDepthFade);
                if (m.HasProperty(ID_FoamDistance)) m.SetFloat(ID_FoamDistance, v.waterFoamDistance);
                if (m.HasProperty(ID_WaveAmplitude)) m.SetFloat(ID_WaveAmplitude, v.waveAmplitude);
                if (m.HasProperty(ID_WaveSpeed)) m.SetFloat(ID_WaveSpeed, v.waveSpeed);
                if (m.HasProperty(ID_SpecularIntensity)) m.SetFloat(ID_SpecularIntensity, v.waterSpecularIntensity);
                if (m.HasProperty(ID_ReflectionStrength)) m.SetFloat(ID_ReflectionStrength, v.waterReflection);
            }
        }

        void ApplyPost(AtmosphereValues v)
        {
            if (volumeProfile == null) return;

            var tone = GetOrAdd<Tonemapping>();
            tone.active = true;
            tone.mode.overrideState = true;
            // ACES DEĞİL. Neutral, DREDGE'in temiz paletini korur.
            tone.mode.value = TonemappingMode.Neutral;

            var ca = GetOrAdd<ColorAdjustments>();
            ca.active = true;
            ca.postExposure.overrideState = true; ca.postExposure.value = v.postExposure;
            ca.contrast.overrideState = true; ca.contrast.value = v.contrast;
            ca.saturation.overrideState = true; ca.saturation.value = v.saturation;
            // Preset'te olmayan alanlar nötr kalsın; yoksa elle girilen bir hue/filter
            // bütün sahneyi boyar ve kaynağı bulunamaz.
            ca.hueShift.overrideState = true; ca.hueShift.value = 0f;
            ca.colorFilter.overrideState = true; ca.colorFilter.value = Color.white;

            var st = GetOrAdd<SplitToning>();
            st.active = true;
            st.shadows.overrideState = true; st.shadows.value = C(v.splitShadows);
            st.highlights.overrideState = true; st.highlights.value = C(v.splitHighlights);
            st.balance.overrideState = true; st.balance.value = v.splitBalance;

            var bloom = GetOrAdd<Bloom>();
            bloom.active = v.bloomIntensity > 0.001f;
            bloom.intensity.overrideState = true; bloom.intensity.value = v.bloomIntensity;
            bloom.threshold.overrideState = true; bloom.threshold.value = v.bloomThreshold;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.62f;

            var vig = GetOrAdd<Vignette>();
            vig.active = v.vignetteIntensity > 0.001f;
            vig.intensity.overrideState = true; vig.intensity.value = v.vignetteIntensity;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.55f;
            vig.color.overrideState = true; vig.color.value = C(v.splitShadows) * 0.35f;

            var grain = GetOrAdd<FilmGrain>();
            grain.active = v.filmGrain > 0.001f;
            grain.intensity.overrideState = true; grain.intensity.value = v.filmGrain;
            grain.type.overrideState = true; grain.type.value = FilmGrainLookup.Medium1;

        }

        // ─────────────────────────── YARDIMCILAR ───────────────────────────
        T GetOrAdd<T>() where T : VolumeComponent
        {
            if (volumeProfile.TryGet(out T comp)) return comp;

            comp = volumeProfile.Add<T>(true);
#if UNITY_EDITOR
            // VolumeProfile.Add() bileşeni asset'in alt objesi YAPMAZ; yapmazsak
            // ilk domain reload'da profile boşalır ve tüm post-processing kaybolur.
            if (UnityEditor.EditorUtility.IsPersistent(volumeProfile))
            {
                comp.hideFlags = HideFlags.HideInHierarchy;
                UnityEditor.AssetDatabase.AddObjectToAsset(comp, volumeProfile);
                UnityEditor.EditorUtility.SetDirty(volumeProfile);
            }
#endif
            return comp;
        }

        Color C(Color c) => linearColorFix ? c.linear : c;

        static Vector3 DirectionFromAngles(float elevation, float azimuth)
        {
            return -(Quaternion.Euler(elevation, azimuth, 0f) * Vector3.forward);
        }

        static Light FindMainDirectionalLight()
        {
#if UNITY_2023_1_OR_NEWER
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = Object.FindObjectsOfType<Light>();
#endif
            Light best = null;
            foreach (var l in lights)
            {
                if (l.type != LightType.Directional) continue;
                if (best == null || l.intensity > best.intensity) best = l;
            }
            return best;
        }

        /// <summary>Sahnedeki tüm StylizedWater materyallerini bulup listeye ekler.</summary>
        public void CollectWaterMaterials()
        {
            waterMaterials.Clear();
#if UNITY_2023_1_OR_NEWER
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
            var renderers = Object.FindObjectsOfType<Renderer>();
#endif
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null) continue;
                    if (!m.shader.name.Contains("StylizedWater")) continue;
                    if (!waterMaterials.Contains(m)) waterMaterials.Add(m);
                }
            }
        }

        /// <summary>Preset A'nın değerlerini Live Values'a kopyalar.</summary>
        public void CopyFromPresetA()
        {
            if (presetA != null) values = presetA.values;
        }

        /// <summary>Live Values'ı Preset A'ya yazar.</summary>
        public void SaveToPresetA()
        {
            if (presetA == null) return;
            presetA.values = values;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(presetA);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }
    }
}
