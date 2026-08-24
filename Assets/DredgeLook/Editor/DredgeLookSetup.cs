using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DredgeLook.EditorTools
{
    /// <summary>
    /// Tools > Dredge Look menüsü. Tek tıkla materyalleri, preset'leri, volume'u ve
    /// su düzlemini kurar; URP ayarlarını kontrol eder.
    /// </summary>
    public static class DredgeLookSetup
    {
        const string GeneratedFolder = "Assets/DredgeLook/Generated";

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Dredge Look/1 - Sahneyi Kur", false, 0)]
        public static void SetupScene()
        {
            EnsureFolder();

            var skyShader = Shader.Find("Dredge/StylizedSky");
            var waterShader = Shader.Find("Dredge/StylizedWater");
            if (skyShader == null || waterShader == null)
            {
                EditorUtility.DisplayDialog("Dredge Look",
                    "Shader'lar bulunamadı. DredgeLook/Shaders klasörünün projede olduğundan " +
                    "ve derleme hatası olmadığından emin ol.", "Tamam");
                return;
            }

            // 1) Materyaller
            var skyMat = CreateOrLoadMaterial(skyShader, GeneratedFolder + "/M_DredgeSky.mat");
            var waterMat = CreateOrLoadMaterial(waterShader, GeneratedFolder + "/M_DredgeWater.mat");

            // 2) Preset'ler
            var presets = CreatePresets();

            // 3) Volume profile
            var profile = CreateOrLoadProfile(GeneratedFolder + "/DredgeVolumeProfile.asset");

            // 4) Atmosfer objesi
            var atmoGO = GameObject.Find("Dredge Atmosphere");
            if (atmoGO == null)
            {
                atmoGO = new GameObject("Dredge Atmosphere");
                Undo.RegisterCreatedObjectUndo(atmoGO, "Create Dredge Atmosphere");
            }

            var volume = GetOrAdd<Volume>(atmoGO);
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;

            var atmo = GetOrAdd<StylizedAtmosphere>(atmoGO);
            atmo.skyMaterial = skyMat;
            atmo.volumeProfile = profile;
            atmo.presetA = presets[1];   // Gündüz
            atmo.presetB = presets[2];   // Gün batımı
            atmo.blend = 0f;
            atmo.usePresets = true;

            // 5) Ana ışık
            Light sun = null;
            foreach (var l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional && (sun == null || l.intensity > sun.intensity)) sun = l;

            if (sun == null)
            {
                var lightGO = new GameObject("Sun");
                sun = lightGO.AddComponent<Light>();
                sun.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(lightGO, "Create Sun");
            }
            atmo.sun = sun;

            // 6) Su
            var waterGO = GameObject.Find("Dredge Water");
            if (waterGO == null)
            {
                waterGO = new GameObject("Dredge Water");
                Undo.RegisterCreatedObjectUndo(waterGO, "Create Dredge Water");
            }
            var mf = GetOrAdd<MeshFilter>(waterGO);
            var mr = GetOrAdd<MeshRenderer>(waterGO);
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var plane = GetOrAdd<InfiniteWaterPlane>(waterGO);
            plane.Rebuild();

            var surface = GetOrAdd<WaterSurface>(waterGO);
            surface.waterMaterial = waterMat;
            surface.seaLevel = waterGO.transform.position.y;

            atmo.waterMaterials.Clear();
            atmo.waterMaterials.Add(waterMat);

            // 7) Skybox + uygula
            RenderSettings.skybox = skyMat;
            atmo.Apply();

            // 8) URP ayarlarını kontrol et
            FixUrpSettings(false);

            EditorUtility.SetDirty(atmo);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = atmoGO;

            Debug.Log("<b>[Dredge Look]</b> Sahne kuruldu. 'Dredge Atmosphere' objesini seçip " +
                      "Inspector'dan canlı ayar yapabilirsin. Presetleri denemek için Preset A/B ve Blend kullan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Dredge Look/2 - Preset'leri Olustur", false, 1)]
        public static void CreatePresetsMenu()
        {
            EnsureFolder();
            CreatePresets();
            AssetDatabase.SaveAssets();
            Debug.Log("<b>[Dredge Look]</b> Preset'ler oluşturuldu: " + GeneratedFolder);
        }

        [MenuItem("Tools/Dredge Look/3 - URP Ayarlarini Duzelt", false, 2)]
        public static void FixUrpSettingsMenu() => FixUrpSettings(true);

        // ─────────────────────────────────────────────────────────────────────
        static void FixUrpSettings(bool verbose)
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null)
            {
                Debug.LogWarning("[Dredge Look] Aktif URP Asset bulunamadı. " +
                                 "Project Settings > Graphics'te URP Asset atanmış olmalı.");
                return;
            }

            var so = new SerializedObject(rp);
            SetBool(so, "m_RequireDepthTexture", true);    // su köpüğü/derinliği için ŞART
            SetBool(so, "m_RequireOpaqueTexture", true);
            SetBool(so, "m_SupportsHDR", true);
            SetFloat(so, "m_ShadowDistance", 110f);        // uzun mesafe bantlı gölgeyi bozar
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rp);

            if (verbose)
            {
                Debug.Log("<b>[Dredge Look]</b> URP ayarları güncellendi: Depth Texture açık, " +
                          "Opaque Texture açık, HDR açık, Shadow Distance 110m.\n" +
                          "SSAO Renderer Feature'ı elle eklemen gerekiyor: URP Renderer > Add Renderer Feature > " +
                          "Screen Space Ambient Occlusion (Intensity 0.5, Radius 0.35).");
            }
        }

        static void SetBool(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
        }

        static void SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();   // Unity sahte-null, ?? ile yakalanmaz
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/DredgeLook"))
                AssetDatabase.CreateFolder("Assets", "DredgeLook");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/DredgeLook", "Generated");
        }

        static Material CreateOrLoadMaterial(Shader shader, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = shader;
                return existing;
            }
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static VolumeProfile CreateOrLoadProfile(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        static AtmospherePreset CreateOrLoadPreset(string name, AtmosphereValues values)
        {
            string path = GeneratedFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<AtmospherePreset>(path);
            if (existing != null) return existing;

            var preset = ScriptableObject.CreateInstance<AtmospherePreset>();
            preset.values = values;
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5 hazır atmosfer. Değerler DREDGE_SANAT_YONU.md'deki paletle birebir.
        // ─────────────────────────────────────────────────────────────────────
        static AtmospherePreset[] CreatePresets()
        {
            return new[]
            {
                CreateOrLoadPreset("Atmosphere_01_Safak", Dawn()),
                CreateOrLoadPreset("Atmosphere_02_Gunduz", AtmosphereValues.Default),
                CreateOrLoadPreset("Atmosphere_03_GunBatimi", Dusk()),
                CreateOrLoadPreset("Atmosphere_04_Gece", Night()),
                CreateOrLoadPreset("Atmosphere_05_Firtina", Storm()),
            };
        }

        static Color H(string hex) => AtmosphereValues.Hex(hex);

        static AtmosphereValues Dawn()
        {
            var v = AtmosphereValues.Default;
            v.sunElevation = 6f; v.sunAzimuth = 95f;
            v.sunColor = H("FFC9A8"); v.sunIntensity = 0.95f;
            v.shadowStrength = 0.62f; v.shadowTint = H("4A5A82");

            v.skyZenith = H("2E4C79"); v.skyHorizon = H("E8C4B8"); v.skyGround = H("B4A9AC");
            v.horizonPower = 2.4f; v.horizonGlow = 0.55f;
            v.sunDiscColor = H("FFE0C0"); v.sunGlowColor = H("FFC49A"); v.sunGlowFalloff = 22f;
            v.starStrength = 0.15f;

            v.ambientSky = H("6E7FA0"); v.ambientEquator = H("97949C"); v.ambientGround = H("46474F");
            v.fogColor = H("DCC0BA"); v.fogDensity = 0.015f;

            v.waterShallow = H("55757F"); v.waterDeep = H("101C2A");
            v.waterFoam = H("F0E6E2"); v.waterSpecular = H("FFD8B8");
            v.waterSpecularIntensity = 3.0f; v.waterReflection = 0.62f;
            v.waveAmplitude = 0.8f; v.waveSpeed = 0.85f;

            v.postExposure = 0.05f; v.contrast = 10f; v.saturation = 0f;
            v.splitShadows = H("3A4C78"); v.splitHighlights = H("FFD9B0");
            v.bloomIntensity = 0.30f; v.bloomThreshold = 1.05f;
            v.vignetteIntensity = 0.26f;
            return v;
        }

        static AtmosphereValues Dusk()
        {
            var v = AtmosphereValues.Default;
            v.sunElevation = 8f; v.sunAzimuth = -110f;
            v.sunColor = H("FF9A4A"); v.sunIntensity = 1.25f;
            v.shadowStrength = 0.7f; v.shadowTint = H("3A3F6B");

            v.skyZenith = H("26365C"); v.skyHorizon = H("E9A165"); v.skyGround = H("C48B6E");
            v.horizonPower = 2.6f; v.horizonGlow = 0.75f;
            v.sunDiscColor = H("FFD9A0"); v.sunGlowColor = H("FF9C52"); v.sunGlowFalloff = 14f;
            v.starStrength = 0.2f;

            v.ambientSky = H("5A6C92"); v.ambientEquator = H("8A7A80"); v.ambientGround = H("3B3440");
            v.fogColor = H("D9A277"); v.fogDensity = 0.016f;

            v.waterShallow = H("6B7C7A"); v.waterDeep = H("17202E");
            v.waterFoam = H("F3DCC6"); v.waterSpecular = H("FFC489");
            v.waterSpecularIntensity = 3.6f; v.waterReflection = 0.68f;

            v.postExposure = 0.0f; v.contrast = 14f; v.saturation = 6f;
            v.splitShadows = H("2B3B6E"); v.splitHighlights = H("FFC98A");
            v.bloomIntensity = 0.38f; v.bloomThreshold = 1.0f;
            v.vignetteIntensity = 0.3f;
            return v;
        }

        static AtmosphereValues Night()
        {
            var v = AtmosphereValues.Default;
            v.sunElevation = 42f; v.sunAzimuth = 140f;
            v.sunColor = H("A8BEDC"); v.sunIntensity = 0.30f;
            v.shadowStrength = 0.45f; v.shadowTint = H("18243A");

            v.skyZenith = H("0A1424"); v.skyHorizon = H("24344C"); v.skyGround = H("1A2434");
            v.horizonPower = 1.6f; v.horizonGlow = 0.25f;
            v.sunDiscColor = H("E8F0FF"); v.sunDiscSize = 0.010f;
            v.sunGlowColor = H("6E86AE"); v.sunGlowFalloff = 60f;
            v.starStrength = 0.9f;

            v.ambientSky = H("1E2C44"); v.ambientEquator = H("1A2230"); v.ambientGround = H("0E1420");
            v.fogColor = H("16212F"); v.fogDensity = 0.024f;

            v.waterShallow = H("23404C"); v.waterDeep = H("060C14");
            v.waterFoam = H("93A8B8"); v.waterSpecular = H("BFD4F0");
            v.waterSpecularIntensity = 1.6f; v.waterReflection = 0.45f;
            v.waveAmplitude = 1.1f;

            v.postExposure = 0.25f; v.contrast = 8f; v.saturation = -12f;
            v.splitShadows = H("16223A"); v.splitHighlights = H("9FB6D8");
            v.bloomIntensity = 0.45f; v.bloomThreshold = 0.9f;
            v.vignetteIntensity = 0.34f; v.filmGrain = 0.18f;
            return v;
        }

        static AtmosphereValues Storm()
        {
            var v = AtmosphereValues.Default;
            v.sunElevation = 34f; v.sunAzimuth = -60f;
            v.sunColor = H("D8DDDC"); v.sunIntensity = 0.65f;
            v.shadowStrength = 0.4f; v.shadowTint = H("46525A");

            v.skyZenith = H("6B7780"); v.skyHorizon = H("A8B2B6"); v.skyGround = H("98A2A6");
            v.horizonPower = 1.3f; v.horizonGlow = 0.15f;
            v.sunDiscColor = H("C8CFD2"); v.sunDiscSize = 0.0f;
            v.sunGlowColor = H("9AA4A8"); v.sunGlowFalloff = 8f;

            v.ambientSky = H("6E7A82"); v.ambientEquator = H("6A7276"); v.ambientGround = H("3C4246");
            v.fogColor = H("A2ACB2"); v.fogDensity = 0.038f;

            v.waterShallow = H("47585C"); v.waterDeep = H("10181E");
            v.waterFoam = H("D6DEE0"); v.waterSpecular = H("C8D0D2");
            v.waterSpecularIntensity = 0.9f; v.waterReflection = 0.35f;
            v.waveAmplitude = 2.1f; v.waveSpeed = 1.5f; v.waterFoamDistance = 2.6f;

            v.postExposure = 0.0f; v.contrast = 6f; v.saturation = -22f;
            v.splitShadows = H("40525E"); v.splitHighlights = H("CBD3D6");
            v.bloomIntensity = 0.12f; v.bloomThreshold = 1.3f;
            v.vignetteIntensity = 0.36f; v.filmGrain = 0.2f;
            return v;
        }
    }
}
