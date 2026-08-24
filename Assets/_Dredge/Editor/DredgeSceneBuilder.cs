using System.Collections.Generic;
using Dredge.Game;
using DredgeLook;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dredge.EditorTools
{
    /// <summary>
    /// Dredge havasında bir deniz sahnesi kurar: Gerstner dalgalı stilize deniz,
    /// prosedürel kayalık adalar ve çamlar, alacakaranlık ışığı, yüzen ve
    /// sürülebilen tekne.
    ///
    /// Menü:  Dredge / 1) Deniz Sahnesini Kur
    /// </summary>
    public static class DredgeSceneBuilder
    {
        const string BoatPrefab = "Assets/Low-Poly 3D Boat Model/Prefab/boat Prefab.prefab";
        const string ScenePath  = "Assets/_Dredge/Scenes/Sea.unity";
        const string MatDir     = "Assets/_Dredge/Materials";
        const string MeshDir    = "Assets/_Dredge/Generated";
        const string ProfilePath = "Assets/_Dredge/Materials/SeaVolumeProfile.asset";
        const string FoamTexture = "Assets/_Dredge/Textures/FoamSwirl.png";
        const string SeaAmbience = "Assets/_Dredge/Audio/SeaAmbience.wav";

        const float OceanSize = 520f;
        const int   OceanCells = 340;

        // Teknenin su hattı, pivotunun 1.42 m üstünde (paketin demo sahnesinden ölçüldü).
        const float Waterline = 1.42f;

        static readonly List<string> Warnings = new List<string>();

        // Her ağaç/kaya için ayrı mesh üretmek yüzlerce asset ve batch demek;
        // birkaç çeşit üretip rastgele döndürerek kullanıyoruz.
        static Mesh[] pinePool, rockPool;

        // --------------------------------------------------------------------- menü

        [MenuItem("Dredge/1) Deniz Sahnesini Kur", false, 0)]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Warnings.Clear();

            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();

            EnableDepthTexture();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldRoot = new GameObject("=== WORLD ===").transform;
            var lightRoot = new GameObject("=== LIGHTING ===").transform;

            var sun = SetupLighting(lightRoot);
            var ocean = BuildOcean(worldRoot);
            BuildIslands(worldRoot);
            BuildClouds(worldRoot);
            var boat = BuildBoat();
            BuildCamera(boat);

            // Deniz ızgarası tekneyi takip etsin ki kenarına varılamasın.
            var oceanSO = new SerializedObject(ocean);
            oceanSO.FindProperty("follow").objectReferenceValue = boat;
            oceanSO.FindProperty("cellSize").floatValue = OceanSize / OceanCells;
            oceanSO.ApplyModifiedProperties();

            // Dredge Look paketi: atmosfer, sonsuz su, bantlı materyaller, kamera.
            DredgeLookIntegration.Apply();

            // Oyun mekanikleri: saat, liman, balık noktaları, mini-oyun, ambar, panik, HUD.
            DredgeWorldDressing.BuildGame(worldRoot, boat);
            DredgeLookIntegration.ConvertMaterials(DredgeLookIntegration.LoadOrCreateSettings());   // Kenney modelleri de bantlı gölgelemeye

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AddToBuildSettings(ScenePath);

            if (Warnings.Count > 0) Debug.LogWarning("[Dredge]\n - " + string.Join("\n - ", Warnings));
            Debug.Log($"[Dredge] Sahne hazır: {ScenePath}\nPlay → W/S gaz, A/D dümen, sağ tık basılı fare ile bak.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            if (sun == null) Warnings.Add("Güneş oluşturulamadı.");
        }

        // ------------------------------------------------------------------ deniz

        static OceanSurface BuildOcean(Transform root)
        {
            // Ayrıntılı ızgara 520 m; ötesi boş kalınca denizin kenarı gerçek ufkun
            // ALTINDA bitiyor ve her şey aşağı kaymış gibi duruyordu. Bu düzlem
            // görüşü ufka kadar dolduruyor; sis yüzünden tamamen sis rengi çıkıyor,
            // yani deniz gökle tam göz hizasında birleşiyor.
            var horizon = GameObject.CreatePrimitive(PrimitiveType.Plane);
            horizon.name = "Ufuk Duzlemi";
            horizon.transform.SetParent(root, false);
            horizon.transform.position = new Vector3(0f, -2f, 0f);   // dalga çukurlarının altında
            horizon.transform.localScale = Vector3.one * 800f;          // 8 km
            Object.DestroyImmediate(horizon.GetComponent<MeshCollider>());
            var hr = horizon.GetComponent<MeshRenderer>();
            hr.shadowCastingMode = ShadowCastingMode.Off;
            hr.receiveShadows = false;
            var horizonMat = MakeMaterial("Horizon", "Dredge/Horizon");
            if (horizonMat != null)
            {
                horizonMat.SetColor("_BaseColor", new Color(0.72f, 0.66f, 0.63f));
                EditorUtility.SetDirty(horizonMat);
            }
            hr.sharedMaterial = horizonMat;

            var mesh = SaveMesh(MeshFactory.OceanGrid(OceanSize, OceanCells), "OceanGrid");

            var go = new GameObject("Ocean");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = WaterMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;   // dalgalar gölge atmasın, kirletir
            mr.receiveShadows = false;

            return go.AddComponent<OceanSurface>();
        }

        static Material WaterMaterial()
        {
            var mat = MakeMaterial("Water", "Dredge/Stylized Water");
            if (mat == null) return null;
            // Referanstaki deniz açık turkuaz; önceki dip rengi neredeyse siyahtı
            // ve sahneyi tek başına öldürüyordu.
            mat.SetColor("_ShallowColor", new Color(0.30f, 0.47f, 0.47f));
            mat.SetColor("_DeepColor", new Color(0.08f, 0.19f, 0.22f));
            mat.SetFloat("_DepthFade", 13f);
            mat.SetColor("_SkyTint", new Color(0.60f, 0.55f, 0.52f));
            mat.SetFloat("_FresnelPower", 4f);
            mat.SetColor("_FoamColor", new Color(0.92f, 0.96f, 0.96f));
            mat.SetFloat("_FoamDepth", 0.9f);
            mat.SetFloat("_FoamCutoff", 0.30f);
            mat.SetFloat("_FoamStrength", 0.65f);
            mat.SetColor("_GlintColor", new Color(1f, 0.86f, 0.62f));
            mat.SetFloat("_GlintPower", 200f);
            mat.SetFloat("_GlintStrength", 1.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ adalar

        struct IslandSpec
        {
            public string name; public Vector3 pos; public float radius, height;
            public int seed, pines; public bool autumn;
        }

        static void BuildIslands(Transform root)
        {
            var islands = new[]
            {
                // Ön plan: teknenin solunda yükselen kayalık burun.
                new IslandSpec { name = "Kaya Burnu",   pos = new Vector3(-36f, 0f,  54f), radius = 24f, height = 27f, seed = 1101, pines = 16 },
                // Sağda, uzayan ormanlık kıyı.
                new IslandSpec { name = "Cam Adasi",    pos = new Vector3( 62f, 0f,  80f), radius = 33f, height = 13f, seed = 2203, pines = 54, autumn = true },
                // Teknenin önündeki küçük kayalık.
                new IslandSpec { name = "Kucuk Kayalik",pos = new Vector3( 11f, 0f,  33f), radius =  7f, height =  4f, seed = 3307, pines = 3 },
                new IslandSpec { name = "Bati Adasi",   pos = new Vector3(-78f, 0f, 108f), radius = 30f, height = 21f, seed = 4409, pines = 24 },
                // Uzak siluetler — derinlik hissi için, sis içinde eriyorlar.
                new IslandSpec { name = "Uzak Ada 1",   pos = new Vector3(-12f, 0f, 186f), radius = 42f, height = 24f, seed = 5501, pines = 18 },
                new IslandSpec { name = "Uzak Ada 2",   pos = new Vector3(142f, 0f, 158f), radius = 46f, height = 19f, seed = 6603, pines = 20, autumn = true },
                new IslandSpec { name = "Uzak Ada 3",   pos = new Vector3(-152f,0f, 132f), radius = 38f, height = 26f, seed = 7707, pines = 14 },
            };

            var rock = RockMaterial();
            var trunk = MakeLit("Trunk", new Color(0.22f, 0.17f, 0.13f), 0.05f);
            var leafDark = MakeLit("Foliage", new Color(0.10f, 0.16f, 0.14f), 0.03f);
            var leafAutumn = MakeLit("FoliageAutumn", new Color(0.42f, 0.26f, 0.14f), 0.03f);

            var group = new GameObject("Islands").transform;
            group.SetParent(root, false);

            Random.InitState(90210);

            pinePool = new Mesh[6];
            for (int i = 0; i < pinePool.Length; i++)
                pinePool[i] = SaveMesh(MeshFactory.Pine(4200 + i * 37), $"Pine_{i}");

            rockPool = new Mesh[8];
            for (int i = 0; i < rockPool.Length; i++)
                rockPool[i] = SaveMesh(MeshFactory.Rock(7700 + i * 53, 1f), $"Rock_{i}");

            foreach (var spec in islands)
            {
                var mesh = SaveMesh(MeshFactory.Island(spec.seed, spec.radius, spec.height), "Island_" + spec.seed);

                var go = new GameObject(spec.name);
                go.transform.SetParent(group, false);
                go.transform.position = spec.pos;
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = rock;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;

                Physics.SyncTransforms();

                if (spec.pines > 0)
                    ScatterPines(go, spec, trunk, spec.autumn ? leafAutumn : leafDark);

                ScatterShoreRocks(go, spec, rock);
            }

            // Teknenin yakınında, su yüzeyinin hemen altında duran sığlıklar —
            // referanstaki suyun altından beliren karanlık kütleler.
            var shallows = new GameObject("Sigliklar").transform;
            shallows.SetParent(root, false);
            for (int i = 0; i < 9; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float r = Random.Range(14f, 46f);
                var pos = new Vector3(Mathf.Cos(a) * r, Random.Range(-3.2f, -0.8f), Mathf.Sin(a) * r + 18f);
                SpawnRock(shallows, pos, Random.Range(2.5f, 6f), rock, 8000 + i);
            }
        }

        static void ScatterPines(GameObject island, IslandSpec spec, Material trunk, Material leaf)
        {
            var group = new GameObject("Pines").transform;
            group.SetParent(island.transform, true);

            int placed = 0, attempts = 0;
            while (placed < spec.pines && attempts++ < spec.pines * 40)
            {
                float a = Random.value * Mathf.PI * 2f;
                float r = spec.radius * Mathf.Sqrt(Random.value) * 0.72f;
                float x = spec.pos.x + Mathf.Cos(a) * r;
                float z = spec.pos.z + Mathf.Sin(a) * r;

                if (!Physics.Raycast(new Vector3(x, spec.height + 40f, z), Vector3.down,
                                     out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.gameObject != island) continue;
                if (hit.point.y < 2.2f) continue;                    // su kenarına ağaç dikme
                if (hit.normal.y < 0.72f) continue;                  // dik kayaya da

                // Kenney Nature Kit ağaçları (CC0); sonbahar adalarında yaprak döken türler.
                bool autumn = leaf != null && leaf.name.Contains("Autumn");
                string[] pines = { "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD", "tree_pineDefaultA", "tree_pineRoundC" };
                string[] falls = { "tree_simple_fall", "tree_tall_fall", "tree_thin_fall", "tree_default_fall", "tree_pineTallB" };
                string model = autumn ? falls[Random.Range(0, falls.Length)] : pines[Random.Range(0, pines.Length)];
                float size = Random.Range(3.2f, 5.2f);
                var tree = DredgeWorldDressing.PlaceFrom(DredgeWorldDressing.Nature, model, group, hit.point - Vector3.up * 0.2f,
                                                          Random.Range(0f, 360f), size, "Pine", bottomAlign: true);
                if (tree == null)
                {
                    var t = new GameObject("Pine");
                    t.transform.SetParent(group, true);
                    t.transform.position = hit.point - Vector3.up * 0.15f;
                    t.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    t.AddComponent<MeshFilter>().sharedMesh = pinePool[Random.Range(0, pinePool.Length)];
                    t.AddComponent<MeshRenderer>().sharedMaterials = new[] { trunk, leaf };
                }
                else foreach (var c in tree.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // ağaç collider'ı gereksiz
                placed++;
            }
        }

        static void ScatterShoreRocks(GameObject island, IslandSpec spec, Material rock)
        {
            var group = new GameObject("ShoreRocks").transform;
            group.SetParent(island.transform, true);

            int count = Mathf.Clamp(Mathf.RoundToInt(spec.radius * 0.35f), 3, 14);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float r = spec.radius * Random.Range(0.80f, 1.10f);
                var pos = new Vector3(spec.pos.x + Mathf.Cos(a) * r, Random.Range(-1.4f, 0.9f),
                                      spec.pos.z + Mathf.Sin(a) * r);
                SpawnRock(group, pos, Random.Range(1.2f, 3.4f), rock, spec.seed * 31 + i);
            }
        }

        static void SpawnRock(Transform parent, Vector3 pos, float size, Material mat, int seed)
        {
            // Kenney Nature Kit kayaları (CC0); yoksa prosedürel kaya.
            string[] rocks = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_largeE", "rock_largeF" };   // tall varyantlar dev monolit oluyordu
            string model = rocks[Mathf.Abs(seed) % rocks.Length];
            var k = DredgeWorldDressing.PlaceFrom(DredgeWorldDressing.Nature, model, parent, pos - Vector3.up * (size * 0.35f),
                                                   Random.Range(0f, 360f), size * 1.5f, "Rock", bottomAlign: true);
            if (k != null)
            {
                k.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                return;
            }
            var mesh = rockPool[Mathf.Abs(seed) % rockPool.Length];
            var go = new GameObject("Rock");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
            go.transform.localScale = new Vector3(size, size * Random.Range(0.6f, 1.05f), size);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static Material RockMaterial() => MakeLit("Rock", new Color(0.62f, 0.60f, 0.63f), 0.08f);

        // -------------------------------------------------------------------- ışık

        static Light SetupLighting(Transform root)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(6f, 145f, 0f);   // ufka yakın, ön-sol
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.80f, 0.62f);
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.88f;
            sun.shadowBias = 0.08f;
            sun.shadowNormalBias = 0.5f;

            var sky = MakeMaterial("Sky", "Dredge/Gradient Sky");
            if (sky != null)
            {
                sky.SetColor("_ZenithColor", new Color(0.50f, 0.48f, 0.56f));
                sky.SetColor("_HorizonColor", new Color(0.92f, 0.70f, 0.55f));
                sky.SetColor("_GroundColor", new Color(0.26f, 0.28f, 0.30f));
                sky.SetFloat("_HorizonBlend", 0.42f);
                sky.SetColor("_SunColor", new Color(1f, 0.90f, 0.74f));
                sky.SetFloat("_SunSize", 0.9973f);
                sky.SetFloat("_SunSoftness", 0.0018f);
                sky.SetFloat("_SunGlow", 8f);
                sky.SetFloat("_SunGlowStrength", 0.32f);
                sky.SetColor("_CloudDark", new Color(0.52f, 0.50f, 0.56f));
                sky.SetColor("_CloudLit", new Color(0.96f, 0.84f, 0.76f));
                sky.SetFloat("_CloudCoverage", 0.46f);
                sky.SetFloat("_CloudSoftness", 0.055f);
                sky.SetFloat("_CloudScale", 0.9f);
                sky.SetFloat("_CloudSpeed", 0.006f);
                sky.SetFloat("_CloudOpacity", 0.92f);
                sky.SetVector("_SunDirection", -sunGo.transform.forward);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Ortam ışığı önce çok düşüktü; sahne gece gibi görünüyordu.
            // Referansta kayanın aydınlık/gölge oranı yalnızca ~1.7 — yani ışığın
            // büyük kısmı gökten geliyor, güneş sert değil. Denge ona göre.
            RenderSettings.ambientSkyColor     = new Color(0.40f, 0.44f, 0.56f);   // gökten soğuk mavi
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.30f, 0.30f);
            RenderSettings.ambientGroundColor  = new Color(0.10f, 0.10f, 0.12f);

            // Uzak adaları eritip derinlik veren deniz pusu.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.60f, 0.58f);   // ufkun sıcak tonu
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 320f;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.reflectionIntensity = 0.7f;

            var volumeGo = new GameObject("Global Volume");
            volumeGo.transform.SetParent(root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildVolumeProfile();

            return sun;
        }

        static VolumeProfile BuildVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else
            {
                for (int i = profile.components.Count - 1; i >= 0; i--)
                    Object.DestroyImmediate(profile.components[i], true);
                profile.components.Clear();
            }

            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0f;
            color.contrast.value = 14f;
            color.saturation.value = 8f;

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.value = 4f;

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.value = new Vector4(0.86f, 0.94f, 1.14f, -0.02f);
            smh.highlights.value = new Vector4(1.10f, 0.99f, 0.88f, 0.02f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.45f;
            bloom.intensity.value = 0.30f;             // fener ve ufuk parlasın
            bloom.scatter.value = 0.72f;
            bloom.tint.value = new Color(1f, 0.86f, 0.72f);
            bloom.highQualityFiltering.value = true;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.26f;
            vignette.smoothness.value = 0.45f;

            var grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.value = 0.14f;

            // profile.Add<> yalnızca bellekte örnek yaratır; sub-asset olarak
            // gömülmezse kayıtta hepsi {fileID: 0} olur ve post-process hiç çalışmaz.
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInHierarchy;
                if (!AssetDatabase.IsSubAsset(c)) AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath);
            return profile;
        }

        // ------------------------------------------------------------------ tekne

        static Transform BuildBoat()
        {
            var root = new GameObject("Boat").transform;
            root.position = new Vector3(0f, -Waterline, -10f);

            // Tekne modeli: Kenney Watercraft Kit "boat-fishing-small" (CC0); yoksa eski Low-Poly prefab.
            var kenneyBoat = DredgeWorldDressing.PlaceFrom("Assets/ThirdParty/Kenney/Watercraft/", "boat-fishing-small", root,
                                                            root.position, 0f, 7.6f, "Model", bottomAlign: false);
            if (kenneyBoat != null)
            {
                foreach (var c in kenneyBoat.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // gövde küresi ile çakışmasın
                // Su hattı: root su seviyesinin 1.42 m altında; modelin su hattı ~%25 yükseklikte olsun
                var bb = new Bounds(root.position, Vector3.zero); bool first = true;
                foreach (var r in kenneyBoat.GetComponentsInChildren<Renderer>()) { if (first) { bb = r.bounds; first = false; } else bb.Encapsulate(r.bounds); }
                float draft = bb.size.y * 0.22f;
                kenneyBoat.transform.localPosition = new Vector3(0f, Waterline - draft - (bb.min.y - root.position.y), 0f);
                kenneyBoat.transform.localRotation = Quaternion.identity;
                // Kenney tekneler +X'e bakar mı? Bounds'a göre uzun eksen Z değilse 90° çevir
                if (bb.size.x > bb.size.z) kenneyBoat.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoatPrefab);
                if (prefab == null) Warnings.Add("Tekne prefabı bulunamadı: " + BoatPrefab);
                else
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.name = "Model";
                }
            }

            root.gameObject.AddComponent<BoatController>();


            // Direk feneri — sahnenin tek sıcak noktası.
            var lantern = AddLight(root, "Direk Feneri", new Vector3(0f, 3.80f, 0.20f),
                                   LightType.Point, new Color(1f, 0.72f, 0.42f), 4.5f, 20f);
            lantern.shadows = LightShadows.Soft;
            lantern.gameObject.AddComponent<LanternFlicker>();

            // Kamara camından sızan ışık.
            AddLight(root, "Kamara", new Vector3(0f, 1.95f, -0.55f),
                     LightType.Point, new Color(1f, 0.80f, 0.55f), 2.2f, 9f);

            // Önü aydınlatan projektör.
            var head = AddLight(root, "Projektor", new Vector3(0f, 2.70f, 1.30f),
                                LightType.Spot, new Color(1f, 0.90f, 0.78f), 5.5f, 50f);
            head.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            head.spotAngle = 58f;
            head.innerSpotAngle = 24f;

            BuildWake(root);
            return root;
        }

        /// <summary>
        /// Teknenin ardında bıraktığı köpük. Emisyon <b>mesafeye</b> bağlı
        /// (rateOverDistance), yani köpük yalnızca tekne hareket ederken çıkar —
        /// duruyorken hiç parçacık doğmaz.
        /// </summary>
        static void BuildWake(Transform boat)
        {
            // İz artık parçacık bulutu değil: kıçın iki köşesinden çıkan, V şeklinde
            // açılan iki düz köpük şeridi (TrailRenderer, suya yatık). Doku boyuna
            // çizgili köpük; şerit uzaklaştıkça genişler ve solar — DREDGE'in izi.
            var streak = WakeStreakMaterial();
            MakeWakeTrail(boat, "Iz Sancak",  new Vector3( 1.15f, Waterline + 0.28f, -3.2f), streak);
            MakeWakeTrail(boat, "Iz Iskele",  new Vector3(-1.15f, Waterline + 0.28f, -3.2f), streak);

            // Baş dalgası: küçük, seyrek serpinti (eskisinin yarısı).
            var foam = FoamMaterial();
            MakeWake(boat, "Bas Serpintisi", new Vector3(0f, Waterline + 0.04f, 3.9f),
                     boxScale: new Vector3(1.2f, 0.02f, 0.2f),
                     ratePerMetre: 2.5f, lifetime: 1.4f, sizeMin: 0.6f, sizeMax: 1.0f,
                     growTo: 2.0f, alpha: 0.55f, spread: 0.7f, foam);
        }

        static void MakeWakeTrail(Transform boat, string name, Vector3 localPos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(boat, false);
            go.transform.localPosition = localPos;
            // TransformZ hizalaması: şerit, objenin +Z eksenine dik yatar → Z'yi yukarı çevir.
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var tr = go.AddComponent<TrailRenderer>();
            tr.alignment = LineAlignment.TransformZ;
            tr.textureMode = LineTextureMode.Tile;
            tr.textureScale = new Vector2(0.12f, 1f);         // her ~8 m'de bir doku tekrarı
            tr.time = 4.5f;
            tr.minVertexDistance = 0.3f;
            tr.numCornerVertices = 0;
            tr.numCapVertices = 2;
            tr.autodestruct = false;
            tr.emitting = true;
            tr.generateLightingData = false;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.sharedMaterial = mat;

            // Genişlik: kıçta dar (0.7 m), 4.5 sn sonra 3.4 m — V açılması.
            var width = new AnimationCurve(
                new Keyframe(0f, 0.7f, 0f, 1.2f),
                new Keyframe(0.35f, 2.0f),
                new Keyframe(1f, 3.4f, 0.4f, 0f));
            tr.widthCurve = width;
            tr.widthMultiplier = 1f;

            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.55f, 0.35f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
        }

        static Material WakeStreakMaterial()
        {
            var mat = MakeMaterial("WakeStreak", "Dredge/Wake Foam");
            if (mat == null) return null;
            var tex = WakeStreakTexture();
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(0.96f, 0.98f, 1f, 0.85f));
            mat.SetFloat("_SoftFade", 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Boyuna çizgili, kenarları yumuşak köpük şeridi dokusu (512×128), Perlin ile üretilir.</summary>
        static Texture2D WakeStreakTexture()
        {
            const string path = "Assets/_Dredge/Textures/WakeStreak.png";
            const int W = 512, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) * 2.6f);   // kenar yumuşaması
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    // Boyuna uzun çizgiler: x'te düşük, y'de yüksek frekans (ve döngüsel x).
                    float n1 = Mathf.PerlinNoise(u * 6f, v * 22f + 3.1f);
                    float n2 = Mathf.PerlinNoise(u * 14f + 7.7f, v * 40f);
                    float streak = Mathf.Clamp01((n1 * 0.65f + n2 * 0.35f - 0.28f) * 3.2f) * 0.85f + 0.15f;
                    float a = Mathf.Clamp01(streak * edge);
                    byte A = (byte)(a * 255f);
                    px[y * W + x] = new Color32(255, 255, 255, A);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // -------------------------------------------------------------- bulutlar

        /// <summary>Düz tabanlı beyaz bulut mesh'leri, kameradan 180–560 m, 90–170 m yükseklikte; rüzgârla sürüklenir.</summary>
        static void BuildClouds(Transform root)
        {
            var settings = DredgeLookIntegration.LoadOrCreateSettings();
            if (!settings.clouds) return;

            var mat = MakeMaterial("Cloud", "Dredge/StylizedLit");
            if (mat == null) return;
            mat.SetColor("_BaseColor", settings.cloudColor);

            var group = new GameObject("Clouds").transform;
            group.SetParent(root, false);

            var rnd = new System.Random(4242);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            for (int i = 0; i < settings.cloudCount; i++)
            {
                int seed = 9000 + i * 17;
                float scale = R(settings.cloudScale.x, settings.cloudScale.y);
                var mesh = SaveMesh(MeshFactory.Cloud(seed, scale), $"Cloud_{i}");

                float ang = R(0f, Mathf.PI * 2f);
                float dist = R(settings.cloudDistance.x, settings.cloudDistance.y);
                var go = new GameObject($"Cloud {i}");
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(Mathf.Cos(ang) * dist, R(settings.cloudHeight.x, settings.cloudHeight.y), Mathf.Sin(ang) * dist);
                go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;   // yere düşen dev bulut gölgesi istenmiyor
                mr.receiveShadows = false;
                var drift = go.AddComponent<CloudDrift>();
                drift.wind = settings.cloudWind * R(0.7f, 1.3f);
            }
        }

        static void MakeWake(Transform boat, string name, Vector3 localPos, Vector3 boxScale,
                             float ratePerMetre, float lifetime, float sizeMin, float sizeMax,
                             float growTo, float alpha, float spread, Material foam)
        {
            var go = new GameObject(name);
            go.transform.SetParent(boat, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, alpha);
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = ratePerMetre;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxScale;

            // Yanlara açılan V izi.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-spread, spread);
            vel.z = new ParticleSystem.MinMaxCurve(-spread * 0.6f, -spread * 0.15f);

            // Kıvrımlar dursun diye ağır ağır dönsünler.
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            // Atlastaki dört fırça darbesinden biri rastgele seçilsin.
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            sheet.cycleCount = 1;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, growTo));

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.10f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;   // suyun üstüne yatık
            renderer.sharedMaterial = foam;
            renderer.sortingFudge = -25f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material FoamMaterial()
        {
            var mat = MakeMaterial("WakeFoam", "Dredge/Wake Foam");
            if (mat == null) return null;

            var importer = AssetImporter.GetAtPath(FoamTexture) as TextureImporter;
            if (importer != null && !importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FoamTexture);
            if (tex == null) Warnings.Add("Köpük dokusu bulunamadı: " + FoamTexture);
            else mat.SetTexture("_BaseMap", tex);

            mat.SetColor("_BaseColor", new Color(0.95f, 0.98f, 1f, 0.9f));
            mat.SetFloat("_SoftFade", 0.9f);      // tekneye değen köpük sönümlensin
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Light AddLight(Transform parent, string name, Vector3 localPos,
                              LightType type, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static void BuildCamera(Transform boat)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 9f, -26f);
            go.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 3000f;   // ufuk düzlemi göz hizasına kadar uzasın
            cam.fieldOfView = 46f;                       // Dredge dar açı kullanıyor, ölçek büyük dursun
            cam.allowHDR = true;
            cam.allowMSAA = true;

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresDepthOption = CameraOverrideOption.On;

            go.AddComponent<AudioListener>();

            // Deniz ambiyansı gerçek zamanlı üretiliyor (SeaAudio) — statik wav döngüsü yok.
            var audioGo = new GameObject("Sea Ambience");
            audioGo.transform.SetParent(go.transform, false);
            audioGo.AddComponent<AudioSource>();
            var sea = audioGo.AddComponent<SeaAudio>();
            sea.volume = 0.55f;

            var follow = go.AddComponent<BoatFollowCamera>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = boat;
            so.ApplyModifiedProperties();
        }

        // ----------------------------------------------------------------- altyapı

        /// <summary>Su shader'ı sahne derinliğini okuyor; URP'de bu doku kapalıysa çalışmaz.</summary>
        static void EnableDepthTexture()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) { Warnings.Add("URP asset bulunamadı, derinlik dokusu elle açılmalı."); return; }

            var so = new SerializedObject(asset);
            var prop = so.FindProperty("m_RequireDepthTexture");
            if (prop != null && !prop.boolValue)
            {
                prop.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                Debug.Log("[Dredge] URP asset'inde Depth Texture açıldı (su derinlik rengi için gerekli).");
            }
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material MakeMaterial(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Warnings.Add("Shader bulunamadı: " + shaderName); return null; }

            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        static Material MakeLit(string name, Color color, float smoothness)
        {
            var mat = MakeMaterial(name, "Universal Render Pipeline/Lit");
            if (mat == null) return null;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
