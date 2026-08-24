using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dredge.EditorTools
{
    /// <summary>
    /// AÇIK SAHNEYİ YERİNDE günceller — sahneyi yeniden kurmaz, elle taşınan objeleri bozmaz.
    ///   Dredge / 4) Ağaçları Yenile (Broken Vector)     — "Pine" objelerini aynı yerde yeni ağaçlarla değiştirir
    ///   Dredge / 5) Eski Tekneyi Geri Getir              — Kenney teknesini eski Low-Poly prefab ile değiştirir
    ///   Dredge / 6) İz Köpüğünü Yenile                    — iz şeridi dokusu/ayarları
    ///   Dredge / 7) Adaları Biyomlarla Giydir (Env Pack)  — LowPoly Environment Pack propları
    /// </summary>
    public static class DredgeWorldRefresh
    {
        const string TreeDir = "Assets/ThirdParty/BrokenVector/Trees/";
        const string EnvDir = "Assets/ThirdParty/LowPolyEnvironment/";
        const string BoatPrefab = "Assets/Low-Poly 3D Boat Model/Prefab/boat Prefab.prefab";
        const float Waterline = 1.42f;

        static readonly string[] AutumnIslands = { "Cam Adasi", "Uzak Ada 2" };

        // ------------------------------------------------------------ ağaçlar

        [MenuItem("Dredge/4) Agaclari Yenile (Broken Vector)", false, 20)]
        public static void RefreshTrees()
        {
            var normal = TreeMaterial("Normal");
            var fall = TreeMaterial("Fall");
            var coldMat = TreeMaterial("Cold");
            if (normal == null) { Debug.LogWarning("[Dredge] Ağaç dokuları bulunamadı: " + TreeDir); return; }

            var pines = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Pine" && t.parent != null) pines.Add(t);

            int n = 0;
            var rng = new System.Random(77);
            foreach (var old in pines)
            {
                var parent = old.parent;
                var pos = old.position; float yaw = old.eulerAngles.y;
                string island = IslandNameOf(old);
                bool autumn = System.Array.Exists(AutumnIslands, s => island.Contains(s));
                bool cold = island.Contains("Uzak Ada 3") || island.Contains("Bati");

                // Tür seçimi: iğne yapraklılar Type0/1/5 (koni), yaprak dökenler Type2/3/4/6/7
                string[] pool = autumn ? new[] { "Tree Type2", "Tree Type3", "Tree Type4", "Tree Type6" }
                                       : new[] { "Tree Type0", "Tree Type1", "Tree Type5", "Tree Type7" };
                string type = pool[rng.Next(pool.Length)];
                int variant = 1 + rng.Next(type == "Tree Type1" || type == "Tree Type0" ? 5 : 4);
                string model = $"{type} {variant:00}";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreeDir + model + ".dae");
                if (prefab == null) { Debug.LogWarning("[Dredge] Ağaç yok: " + model); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = "Pine";
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                float h = (float)(3.6 + rng.NextDouble() * 3.0);   // 3.6–6.6 m boy
                FitHeight(go, h);
                var mat = autumn ? fall : (cold && coldMat != null ? coldMat : normal);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = mat; r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                Object.DestroyImmediate(old.gameObject);
                n++;
            }
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[Dredge] {n} ağaç yenilendi (Broken Vector Low Poly Tree Pack).");
        }

        static string IslandNameOf(Transform t)
        {
            // Pine → Islands grubunun altındaki ada objesi kardeş; en yakın ada collider'ını ada adı olarak al
            float best = float.MaxValue; string name = "";
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (mc.bounds.size.magnitude < 12f) continue;
                float d = Vector3.Distance(mc.bounds.center, t.position);
                if (d < best) { best = d; name = mc.name; }
            }
            return name;
        }

        static Material TreeMaterial(string sheet)
        {
            string path = $"Assets/_Dredge/Materials/Tree_{sheet}.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TreeDir + $"Textures/Colorsheet Tree {sheet}.png");
            if (tex == null) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Dredge/StylizedLit")) { name = "Tree_" + sheet };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Bands", 3f); mat.SetFloat("_BandSoftness", 0.08f); mat.SetFloat("_LightWrap", 0.15f);
            mat.SetFloat("_ShadowStrength", 0.8f); mat.SetFloat("_AmbientStrength", 0.85f);
            mat.SetFloat("_UseAtmosphereTint", 1f); mat.EnableKeyword("_USE_ATMOSPHERE_TINT");
            mat.SetFloat("_SpecularOn", 0f); mat.DisableKeyword("_SPECULAR_ON");
            mat.SetFloat("_RimStrength", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Objeyi, dünya yüksekliği targetHeight olacak şekilde ölçekler ve altını pivot konumuna oturtur.</summary>
        public static void FitHeight(GameObject go, float targetHeight)
        {
            go.transform.localScale = Vector3.one;
            var b = new Bounds(go.transform.position, Vector3.zero); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float k = targetHeight / Mathf.Max(b.size.y, 0.01f);
            go.transform.localScale = Vector3.one * k;
            float bottom = (b.min.y - go.transform.position.y) * k;
            go.transform.position -= Vector3.up * bottom;
        }

        // ------------------------------------------------------------ tekne

        [MenuItem("Dredge/5) Eski Tekneyi Geri Getir", false, 21)]
        public static void RestoreOldBoat()
        {
            var boat = GameObject.Find("Boat");
            if (boat == null) { Debug.LogWarning("[Dredge] 'Boat' bulunamadı."); return; }
            var old = boat.transform.Find("Model");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoatPrefab);
            if (prefab == null) { Debug.LogWarning("[Dredge] Eski tekne prefabı yok: " + BoatPrefab); return; }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boat.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[Dredge] Eski Low-Poly tekne geri geldi.");
        }

        // ------------------------------------------------------------ iz köpüğü

        [MenuItem("Dredge/6) Iz Kopugunu Yenile", false, 22)]
        public static void RefreshWake()
        {
            var tex = SoftFoamTexture();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Dredge/Materials/WakeStreak.mat");
            if (mat == null) { mat = new Material(Shader.Find("Dredge/Wake Foam")) { name = "WakeStreak" }; AssetDatabase.CreateAsset(mat, "Assets/_Dredge/Materials/WakeStreak.mat"); }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(0.93f, 0.97f, 1f, 0.7f));
            mat.SetFloat("_SoftFade", 0.8f);
            EditorUtility.SetDirty(mat);

            int n = 0;
            foreach (var tr in Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None))
            {
                if (!tr.name.StartsWith("Iz")) continue;
                tr.sharedMaterial = mat;
                tr.time = 6.5f;
                tr.minVertexDistance = 0.25f;
                tr.textureMode = LineTextureMode.Tile;
                tr.textureScale = new Vector2(0.07f, 1f);        // ~14 m'de bir doku
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1.4f, 0f, 2f), new Keyframe(0.3f, 3.6f), new Keyframe(1f, 6.5f, 1f, 0f));
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.45f, 0.3f), new GradientAlphaKey(0.15f, 0.7f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = g;
                // şeritler kıçta birbirine yakın dursun: merkeze 0.7 m
                var lp = tr.transform.localPosition; lp.x = Mathf.Sign(lp.x) * 0.7f; lp.y = Waterline + 0.22f; tr.transform.localPosition = lp;
                n++;
            }
            // Baş serpintisi: daha az, daha iri, daha yumuşak
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (ps.name != "Bas Serpintisi") continue;
                var main = ps.main; main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.0f); main.startLifetime = 1.8f; main.startColor = new Color(1f, 1f, 1f, 0.45f);
                var em = ps.emission; em.rateOverDistance = 1.8f;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            }
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[Dredge] İz köpüğü yenilendi ({n} şerit).");
        }

        // ------------------------------------------------------------ biyomlar

        enum Biome { Forest, Autumn, Rocky, Alpine }

        static Biome BiomeOf(string island)
        {
            if (island.Contains("Cam Adasi") || island.Contains("Uzak Ada 2")) return Biome.Autumn;
            if (island.Contains("Bati") || island.Contains("Uzak Ada 3")) return Biome.Alpine;
            if (island.Contains("Kucuk")) return Biome.Rocky;
            return Biome.Forest;
        }

        [MenuItem("Dredge/7) Adalari Biyomlarla Giydir (Env Pack)", false, 23)]
        public static void DressBiomes()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnvDir + "Rock_1.fbx") == null) { Debug.LogWarning("[Dredge] Environment Pack bulunamadı: " + EnvDir); return; }
            var rng = new System.Random(4242);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // 1) Mevcut kayaları (Kenney/prosedürel) aynı yerde Env Pack kayalarıyla değiştir
            var rocks = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Rock" && t.parent != null) rocks.Add(t);
            int nr = 0;
            foreach (var old in rocks)
            {
                var b = BoundsOf(old.gameObject);
                float h = Mathf.Clamp(b.size.y, 1.2f, 5f);
                var parent = old.parent; var pos = new Vector3(b.center.x, b.min.y, b.center.z); float yaw = old.eulerAngles.y;
                Object.DestroyImmediate(old.gameObject);
                string model = rng.NextDouble() < 0.8 ? $"Rock_{1 + rng.Next(6)}" : "Stone_1";
                var go = Spawn(model, parent, pos, yaw, h * R(0.9f, 1.3f), "Rock", collider: true);
                if (go != null) nr++;
            }

            // 2) Her adayı biyomuna göre giydir
            var islands = new List<MeshCollider>();
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                if (mc.transform.root.name.Contains("WORLD") && mc.bounds.size.magnitude > 20f && !mc.name.StartsWith("Iskele") && mc.transform.parent != null && mc.transform.parent.name == "Islands")
                    islands.Add(mc);

            int props = 0;
            foreach (var isl in islands)
            {
                var biome = BiomeOf(isl.name);
                var group = new GameObject(isl.name + " - Biyom").transform;
                group.SetParent(isl.transform.parent, false);
                float r = Mathf.Max(isl.bounds.extents.x, isl.bounds.extents.z);
                var c = isl.bounds.center;

                int count = Mathf.RoundToInt(r * (biome == Biome.Rocky ? 1.2f : 2.2f));
                for (int i = 0; i < count; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), d = R(0.1f, 0.92f) * r;
                    var p = new Vector3(c.x + Mathf.Cos(a) * d, 80f, c.z + Mathf.Sin(a) * d);
                    if (!Physics.Raycast(p, Vector3.down, out var hit, 120f) || hit.collider != isl) continue;
                    if (hit.point.y < 1.2f) continue;                 // su kenarı
                    bool steep = hit.normal.y < 0.7f;
                    string model; float h;
                    switch (biome)
                    {
                        case Biome.Forest:
                            if (steep) { model = $"Rock_{1 + rng.Next(6)}"; h = R(1f, 2.4f); }
                            else { double q = rng.NextDouble(); model = q < 0.45 ? $"Bush_{1 + rng.Next(3)}" : q < 0.7 ? $"Grass_{1 + rng.Next(2)}" : q < 0.85 ? $"Plant_{1 + rng.Next(7)}" : $"Log_{1 + rng.Next(3)}"; h = model.StartsWith("Log") ? R(0.6f, 1.0f) : R(0.5f, 1.3f); }
                            break;
                        case Biome.Autumn:
                            if (steep) { model = $"Stone_1"; h = R(1f, 2f); }
                            else { double q = rng.NextDouble(); model = q < 0.5 ? $"Bush_{1 + rng.Next(3)}" : q < 0.8 ? $"Plant_{1 + rng.Next(7)}" : $"Log_{1 + rng.Next(3)}"; h = R(0.5f, 1.2f); }
                            break;
                        case Biome.Alpine:
                            { double q = rng.NextDouble(); model = q < 0.6 ? $"Rock_{1 + rng.Next(6)}" : q < 0.8 ? "Stone_1" : $"Grass_{1 + rng.Next(2)}"; h = model.StartsWith("Grass") ? R(0.4f, 0.8f) : R(1.2f, 3.2f); }
                            break;
                        default:
                            { model = $"Rock_{1 + rng.Next(6)}"; h = R(0.8f, 2f); }
                            break;
                    }
                    var go = Spawn(model, group, hit.point - Vector3.up * 0.1f, R(0f, 360f), h, model, collider: model.StartsWith("Rock") || model.StartsWith("Stone"));
                    if (go != null) props++;
                }

                // Alpine: tepeye dağ parçası
                if (biome == Biome.Alpine)
                {
                    if (Physics.Raycast(new Vector3(c.x, 120f, c.z), Vector3.down, out var top, 200f) && top.collider == isl)
                    {
                        var m = Spawn($"Mounting_{1 + rng.Next(3)}", group, top.point - Vector3.up * 2f, R(0f, 360f), R(14f, 22f), "Dag", collider: true);
                        if (m != null) props++;
                    }
                }
            }
            DredgeLookIntegration.ConvertMaterials(DredgeLookIntegration.LoadOrCreateSettings());
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[Dredge] Biyomlar giydirildi: {nr} kaya değişti, {props} prop eklendi ({islands.Count} ada).");
        }

        // ------------------------------------------------ fener & dağ yerleşimi

        [MenuItem("Dredge/8) Fener ve Daglari Yerlestir", false, 24)]
        public static void FixLighthouseAndMountains()
        {
            // Deniz feneri: bulunduğu XZ'de zemine indir, %20 göm, altına kaya kaide
            var tower = GameObject.Find("Deniz Feneri");
            if (tower != null)
            {
                var b = BoundsOf(tower);
                var top = new Vector3(b.center.x, 120f, b.center.z);
                float ground = 0f;
                foreach (var hit in Physics.RaycastAll(top, Vector3.down, 200f))
                    if (!hit.transform.IsChildOf(tower.transform) && hit.point.y > ground) ground = hit.point.y;
                float h = b.size.y;
                float bottomTarget = ground - h * 0.18f;
                tower.transform.position += Vector3.up * (bottomTarget - b.min.y);
                // kaide: Env Pack düz kaya, kule genişliğinin 1.6 katı
                var old = GameObject.Find("Fener Kaidesi"); if (old != null) Object.DestroyImmediate(old);
                float w = Mathf.Max(b.size.x, b.size.z) * 1.6f;
                var ped = Spawn("Rock_3", tower.transform.parent, new Vector3(b.center.x, ground - 2.5f, b.center.z), 20f, 4.5f, "Fener Kaidesi", collider: true);
                if (ped != null) { var pb = BoundsOf(ped); float k = w / Mathf.Max(pb.size.x, pb.size.z); ped.transform.localScale = new Vector3(ped.transform.localScale.x * k, ped.transform.localScale.y, ped.transform.localScale.z * k); }
                Debug.Log($"[Dredge] Deniz feneri zemine oturtuldu (zemin {ground:0.0} m).");
            }

            // Dağlar: yalnızca Alpine adalara, tepeye %55 gömülü, ada genişliğine göre
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t != null && t.name == "Dag") Object.DestroyImmediate(t.gameObject);
            var rng = new System.Random(99);
            int placed = 0;
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (mc.transform.parent == null || mc.transform.parent.name != "Islands" || mc.bounds.size.magnitude < 20f) continue;
                if (BiomeOf(mc.name) != Biome.Alpine) continue;
                var c = mc.bounds.center;
                if (!Physics.Raycast(new Vector3(c.x, 150f, c.z), Vector3.down, out var top, 250f) || top.collider != mc) continue;
                float islandW = Mathf.Max(mc.bounds.size.x, mc.bounds.size.z);
                float h = islandW * 0.45f;
                var m = Spawn($"Mounting_{1 + rng.Next(3)}", mc.transform.parent, top.point - Vector3.up * (h * 0.55f), (float)rng.NextDouble() * 360f, h, "Dag", collider: true);
                if (m != null) { var mb = BoundsOf(m); float k = islandW * 0.7f / Mathf.Max(mb.size.x, mb.size.z); m.transform.localScale = new Vector3(m.transform.localScale.x * k, m.transform.localScale.y, m.transform.localScale.z * k); placed++; }
            }
            DredgeLookIntegration.ConvertMaterials(DredgeLookIntegration.LoadOrCreateSettings());
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[Dredge] {placed} dağ yerleştirildi (gömülü).");
        }

        static Bounds BoundsOf(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }

        static GameObject Spawn(string model, Transform parent, Vector3 pos, float yaw, float height, string name, bool collider)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvDir + model + ".fbx");
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            FitHeight(go, height);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            if (collider) foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        /// <summary>Yumuşak, bulutumsu köpük: düşük frekanslı Perlin, kenarlar eriyen, ortası hafif boşluklu.</summary>
        static Texture2D SoftFoamTexture()
        {
            const string path = "Assets/_Dredge/Textures/WakeSoft.png";
            const int W = 512, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) * 2.2f);
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    float n1 = Mathf.PerlinNoise(u * 4f + 11f, v * 5f + 2f);
                    float n2 = Mathf.PerlinNoise(u * 9f + 3f, v * 11f + 8f);
                    float n3 = Mathf.PerlinNoise(u * 22f, v * 26f + 5f);
                    float foam = Mathf.Clamp01((n1 * 0.55f + n2 * 0.3f + n3 * 0.15f - 0.38f) * 2.4f);
                    // ortada (şeridin merkezinde) köpük daha yoğun
                    float mid = 1f - Mathf.Abs(v - 0.5f) * 1.2f;
                    float a = Mathf.Clamp01(foam * edge * (0.55f + 0.45f * mid));
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px); tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null) { imp.alphaIsTransparency = true; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
