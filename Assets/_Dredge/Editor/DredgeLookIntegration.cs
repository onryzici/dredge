using System.Collections.Generic;
using DredgeLook;
using DredgeLook.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dredge.EditorTools
{
    /// <summary>
    /// Dredge Look paketini (Assets/DredgeLook) deniz sahnesine bağlar. Bütün sayısal
    /// değerler <see cref="DredgeLookSettings"/> asset'inden gelir
    /// (Assets/_Dredge/DredgeLookSettings.asset) — Inspector'dan düzenlenir, sahne
    /// yeniden kurulunca kaybolmaz.
    ///
    /// Menü:
    ///   Dredge / 2) Dredge Look Uygula           — ayar dosyasını açık sahneye uygular
    ///   Dredge / 3) Sahnedeki Ayarları Kaydet    — sahnede (Live Values / materyal) yaptığın
    ///                                              değişiklikleri ayar dosyasına geri yazar
    /// </summary>
    public static class DredgeLookIntegration
    {
        public const string SettingsPath = "Assets/_Dredge/DredgeLookSettings.asset";
        const string LookMatDir = "Assets/DredgeLook/Generated/Materials";

        [MenuItem("Dredge/2) Dredge Look Uygula", false, 1)]
        public static void ApplyMenu()
        {
            Apply();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        [MenuItem("Dredge/3) Sahnedeki Ayarlari Kaydet (Live Values -> ayar dosyasi)", false, 2)]
        public static void SaveSceneToSettings()
        {
            var s = LoadOrCreateSettings();
            var atmo = Object.FindFirstObjectByType<StylizedAtmosphere>();
            if (atmo != null) s.atmosphere = atmo.Current;

            var plane = Object.FindFirstObjectByType<InfiniteWaterPlane>();
            var wm = plane != null ? plane.GetComponent<MeshRenderer>()?.sharedMaterial : null;
            if (wm != null)
            {
                s.crestFoam = wm.GetFloat("_CrestFoam");
                s.foamSoftness = wm.GetFloat("_FoamSoftness");
                s.normalStrength = wm.GetFloat("_NormalStrength");
                s.specularPower = wm.GetFloat("_SpecularPower");
                s.specularSoftness = wm.GetFloat("_SpecularSoftness");
                s.alphaShallow = wm.GetFloat("_AlphaShallow");
                s.waveFadeDistance = wm.GetFloat("_WaveFadeDistance");
                s.reflectionBase = wm.GetFloat("_ReflectionBase");
                s.planarStrength = wm.GetFloat("_PlanarStrength");
                s.planarDistortion = wm.GetFloat("_PlanarDistortion");
            }
            var refl = plane != null ? plane.GetComponent<PlanarReflection>() : null;
            if (refl != null) s.reflectionTextureScale = refl.textureScale;

            var rock = AssetDatabase.LoadAssetAtPath<Material>($"{LookMatDir}/M_Rock_Stylized.mat");
            if (rock != null)
            {
                s.bands = Mathf.RoundToInt(rock.GetFloat("_Bands"));
                s.bandSoftness = rock.GetFloat("_BandSoftness");
                s.lightWrap = rock.GetFloat("_LightWrap");
                s.shadowStrength = rock.GetFloat("_ShadowStrength");
                s.ambientStrength = rock.GetFloat("_AmbientStrength");
                s.rimStrength = rock.GetFloat("_RimStrength");
                s.rockColor = rock.GetColor("_BaseColor");
            }
            var pine = AssetDatabase.LoadAssetAtPath<Material>($"{LookMatDir}/M_Foliage_Stylized.mat");
            if (pine != null) s.pineColor = pine.GetColor("_BaseColor");
            var autumn = AssetDatabase.LoadAssetAtPath<Material>($"{LookMatDir}/M_FoliageAutumn_Stylized.mat");
            if (autumn != null) s.autumnColor = autumn.GetColor("_BaseColor");
            var trunk = AssetDatabase.LoadAssetAtPath<Material>($"{LookMatDir}/M_Trunk_Stylized.mat");
            if (trunk != null) s.trunkColor = trunk.GetColor("_BaseColor");
            var cloud = AssetDatabase.LoadAssetAtPath<Material>($"{LookMatDir}/M_Cloud_Stylized.mat");
            if (cloud != null) { s.cloudColor = cloud.GetColor("_BaseColor"); s.cloudShadowStrength = cloud.GetFloat("_ShadowStrength"); }

            var cam = Camera.main;
            if (cam != null) { s.fieldOfView = cam.fieldOfView; s.farClip = cam.farClipPlane; }

            var sea = Object.FindFirstObjectByType<SeaAudio>();
            if (sea != null) s.seaVolume = sea.volume;
            var eng = Object.FindFirstObjectByType<EngineAudio>();
            if (eng != null) s.engineVolume = eng.volume;

            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(s);
            Debug.Log("[Dredge] Sahnedeki ayarlar kaydedildi → " + SettingsPath);
        }

        public static DredgeLookSettings LoadOrCreateSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<DredgeLookSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<DredgeLookSettings>();
            s.atmosphere = DredgeLookSettings.DefaultAtmosphere();
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        public static void Apply()
        {
            if (Shader.Find("Dredge/StylizedLit") == null)
            {
                Debug.LogWarning("[Dredge] DredgeLook shader'ları bulunamadı; entegrasyon atlandı.");
                return;
            }

            var s = LoadOrCreateSettings();
            RetireOldSea();

            // Atmosfer + su + preset + volume'u paket kendi kuruyor.
            DredgeLookSetup.SetupScene();

            var atmo = Object.FindFirstObjectByType<StylizedAtmosphere>();
            var water = Object.FindFirstObjectByType<InfiniteWaterPlane>();
            if (atmo == null || water == null)
            {
                Debug.LogWarning("[Dredge] Dredge Look kurulumu tamamlanamadı.");
                return;
            }

            ConfigureAtmosphere(atmo, s);
            ConfigureWater(water, s);
            ConvertMaterials(s);
            ConfigureCamera(water, s);
            ConfigureAudio(s);
            EmbedVolumeComponents(atmo.volumeProfile);
            WireLive(atmo, water, s);

            EditorUtility.SetDirty(atmo);
            AssetDatabase.SaveAssets();
            Debug.Log("[Dredge] Dredge Look uygulandı. Ayarları CANLI düzenlemek için Project'te " + SettingsPath +
                      " dosyasını seç — her değişiklik anında sahneye yansır.");
        }

        /// <summary>Asset'i her kare sahneye basan bileşen: asset'i düzenle, anında gör.</summary>
        static void WireLive(StylizedAtmosphere atmo, InfiniteWaterPlane water, DredgeLookSettings s)
        {
            var live = atmo.GetComponent<DredgeLookLive>();
            if (live == null) live = atmo.gameObject.AddComponent<DredgeLookLive>();
            live.settings = s;
            live.atmosphere = atmo;
            live.waterMaterial = water.GetComponent<MeshRenderer>()?.sharedMaterial;
            live.reflection = water.GetComponent<PlanarReflection>();
            live.mainCamera = Camera.main;
            live.seaAudio = Object.FindFirstObjectByType<SeaAudio>();
            live.engineAudio = Object.FindFirstObjectByType<EngineAudio>();

            live.stylizedMaterials.Clear();
            var lit = Shader.Find("Dredge/StylizedLit");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { LookMatDir, "Assets/_Dredge/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.shader == lit) live.stylizedMaterials.Add(m);
            }
            live.ApplyNow();
            EditorUtility.SetDirty(live);
        }

        // ------------------------------------------------------------ eski deniz

        static void RetireOldSea()
        {
            foreach (var name in new[] { "Ocean", "Ufuk Duzlemi", "Global Volume" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }
        }

        // ------------------------------------------------------------- atmosfer

        static void ConfigureAtmosphere(StylizedAtmosphere atmo, DredgeLookSettings s)
        {
            var dusk = AssetDatabase.LoadAssetAtPath<AtmospherePreset>("Assets/DredgeLook/Generated/Atmosphere_03_GunBatimi.asset");
            var day  = AssetDatabase.LoadAssetAtPath<AtmospherePreset>("Assets/DredgeLook/Generated/Atmosphere_02_Gunduz.asset");
            if (day != null) atmo.presetA = day;
            if (dusk != null) atmo.presetB = dusk;
            atmo.blend = 0f;

            // usePresets KAPALI → Inspector'daki Live Values doğrudan uygulanır.
            atmo.usePresets = false;
            atmo.continuousUpdate = true;
            atmo.values = s.atmosphere;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            atmo.Apply();
        }

        // ------------------------------------------------------------------ su

        static void ConfigureWater(InfiniteWaterPlane plane, DredgeLookSettings s)
        {
            plane.size = 1200f;
            plane.resolution = 200;
            plane.centerDense = true;
            plane.followCamera = true;
            plane.snapToGrid = true;
            plane.targetCamera = Camera.main;
            plane.transform.position = Vector3.zero;
            plane.Rebuild();

            var surface = plane.GetComponent<WaterSurface>();
            if (surface != null) surface.seaLevel = 0f;

            var mr = plane.GetComponent<MeshRenderer>();
            var wm = mr != null ? mr.sharedMaterial : null;
            if (wm != null)
            {
                wm.SetFloat("_CrestFoam", s.crestFoam);
                wm.SetFloat("_FoamSoftness", s.foamSoftness);
                wm.SetFloat("_NormalStrength", s.normalStrength);
                wm.SetFloat("_SpecularPower", s.specularPower);
                wm.SetFloat("_SpecularSoftness", s.specularSoftness);
                wm.SetFloat("_AlphaShallow", s.alphaShallow);
                wm.SetFloat("_WaveFadeDistance", s.waveFadeDistance);
                wm.SetFloat("_ReflectionBase", s.reflectionBase);
                wm.SetFloat("_PlanarStrength", s.planarStrength);
                wm.SetFloat("_PlanarDistortion", s.planarDistortion);
                EditorUtility.SetDirty(wm);
            }

            plane.gameObject.layer = 4;   // Water: kendini yansıtmasın
            var refl = plane.GetComponent<PlanarReflection>();
            if (refl == null) refl = plane.gameObject.AddComponent<PlanarReflection>();
            refl.planeY = 0f;
            refl.textureScale = s.reflectionTextureScale;
            refl.reflectLayers = ~0;
            EditorUtility.SetDirty(plane);
        }

        // ------------------------------------------------------------ materyaller

        public static void ConvertMaterials(DredgeLookSettings s)
        {
            var lit = Shader.Find("Dredge/StylizedLit");
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null || urpLit == null) return;

            if (!AssetDatabase.IsValidFolder(LookMatDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/DredgeLook/Generated"))
                    AssetDatabase.CreateFolder("Assets/DredgeLook", "Generated");
                AssetDatabase.CreateFolder("Assets/DredgeLook/Generated", "Materials");
            }

            var cache = new Dictionary<Material, Material>();
            int swapped = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (m.shader == lit) { TuneStylized(m, s); continue; }
                    if (m.shader != urpLit) continue;
                    if (!cache.TryGetValue(m, out var styl))
                    {
                        styl = MakeStylized(m, lit, s);
                        cache[m] = styl;
                    }
                    mats[i] = styl;
                    changed = true;
                }
                if (changed) { r.sharedMaterials = mats; swapped++; }
            }

            Debug.Log($"[Dredge] {cache.Count} materyal StylizedLit'e çevrildi ({swapped} renderer).");
        }

        static Material MakeStylized(Material source, Shader lit, DredgeLookSettings s)
        {
            string path = $"{LookMatDir}/M_{source.name}_Stylized.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(lit) { name = "M_" + source.name + "_Stylized" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = lit;

            var baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            var baseMap = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            mat.SetColor("_BaseColor", baseColor);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

            TuneStylized(mat, s);
            return mat;
        }

        /// <summary>Bant/gölge ayarları ve palet renkleri ayar dosyasından.</summary>
        static void TuneStylized(Material mat, DredgeLookSettings s)
        {
            mat.SetFloat("_Bands", s.bands);
            mat.SetFloat("_BandSoftness", s.bandSoftness);
            mat.SetFloat("_LightWrap", s.lightWrap);
            mat.SetFloat("_ShadowStrength", s.shadowStrength);
            mat.SetFloat("_AmbientStrength", s.ambientStrength);
            mat.SetFloat("_UseAtmosphereTint", 1f);
            mat.EnableKeyword("_USE_ATMOSPHERE_TINT");
            mat.SetFloat("_SpecularOn", 0f);
            mat.DisableKeyword("_SPECULAR_ON");
            mat.SetFloat("_RimStrength", s.rimStrength);

            string n = mat.name;
            string ln = n.ToLowerInvariant();
            // Kenney Nature Kit materyal adları → palet
            if (ln.Contains("leafsfall")) mat.SetColor("_BaseColor", s.autumnColor);
            else if (ln.Contains("leafs") || ln.Contains("grass")) mat.SetColor("_BaseColor", s.pineColor);
            else if (ln.Contains("woodbark") || ln.Contains("woodbirch") || ln.Contains("wooddark")) mat.SetColor("_BaseColor", s.trunkColor);
            else if (ln.Contains("_defaultmat") || ln.Contains("dirt")) mat.SetColor("_BaseColor", s.rockColor);
            // LowPoly Environment Pack renk-adlı materyaller (Green.3, Brown.5, Orange.3, Gray.1, Purple.1, Pink.1, Yellow.2)
            else if (ln.Contains("_green.")) mat.SetColor("_BaseColor", s.pineColor * (ln.EndsWith("2") || ln.EndsWith("5") || ln.EndsWith("8") ? 1.25f : 1f));
            else if (ln.Contains("_brown.")) mat.SetColor("_BaseColor", s.trunkColor);
            else if (ln.Contains("_orange.") || ln.Contains("_gray.")) mat.SetColor("_BaseColor", s.rockColor);
            else if (ln.Contains("_purple.")) mat.SetColor("_BaseColor", new Color(0.42f, 0.36f, 0.52f));
            else if (ln.Contains("_pink.")) mat.SetColor("_BaseColor", new Color(0.62f, 0.42f, 0.48f));
            else if (ln.Contains("_yellow.")) mat.SetColor("_BaseColor", new Color(0.70f, 0.60f, 0.34f));
            else if (n.Contains("Rock")) mat.SetColor("_BaseColor", s.rockColor);
            else if (n.Contains("FoliageAutumn")) { mat.SetColor("_BaseColor", s.autumnColor); mat.SetFloat("_ShadowStrength", Mathf.Min(1f, s.shadowStrength + 0.05f)); }
            else if (n.Contains("Foliage")) { mat.SetColor("_BaseColor", s.pineColor); mat.SetFloat("_ShadowStrength", Mathf.Min(1f, s.shadowStrength + 0.05f)); }
            else if (n.Contains("Trunk")) mat.SetColor("_BaseColor", s.trunkColor);
            else if (n.Contains("Cloud"))
            {
                mat.SetColor("_BaseColor", s.cloudColor);
                mat.SetFloat("_Bands", 2f);
                mat.SetFloat("_ShadowStrength", s.cloudShadowStrength);
                mat.SetFloat("_LightWrap", 0.45f);
                mat.SetFloat("_AmbientStrength", 1.1f);
            }

            EditorUtility.SetDirty(mat);
        }

        // ---------------------------------------------------------------- kamera

        static void ConfigureCamera(InfiniteWaterPlane plane, DredgeLookSettings s)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.fieldOfView = s.fieldOfView;
            cam.farClipPlane = s.farClip;
            plane.targetCamera = cam;
            EditorUtility.SetDirty(cam);
        }

        // ------------------------------------------------------------------ ses

        static void ConfigureAudio(DredgeLookSettings s)
        {
            var sea = Object.FindFirstObjectByType<SeaAudio>();
            if (sea != null) { sea.volume = s.seaVolume; EditorUtility.SetDirty(sea); }
            var eng = Object.FindFirstObjectByType<EngineAudio>();
            if (eng != null) { eng.volume = s.engineVolume; EditorUtility.SetDirty(eng); }
        }

        // ------------------------------------------------------------ volume asset

        static void EmbedVolumeComponents(VolumeProfile profile)
        {
            if (profile == null) return;
            string path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path)) return;

            foreach (var c in profile.components)
            {
                if (c == null || EditorUtility.IsPersistent(c)) continue;
                c.hideFlags = HideFlags.HideInHierarchy;
                try { AssetDatabase.AddObjectToAsset(c, profile); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Dredge] Volume bileşeni gömülemedi ({c.GetType().Name}): {e.Message}");
                }
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
    }
}
