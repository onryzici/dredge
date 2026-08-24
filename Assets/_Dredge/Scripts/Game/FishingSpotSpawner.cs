using System.Collections.Generic;
using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Adaların çevresine balık noktaları serper: kıyıya yakın olanlar kıyı türleri,
    /// açıkta olanlar derin türler, birkaç tanesi yalnız gece görünen aberasyonlar.
    /// Halka görseli ve serpinti parçacıkları çalışma zamanında üretilir.
    /// </summary>
    public class FishingSpotSpawner : MonoBehaviour
    {
        public int coastalSpots = 10;
        public int deepSpots = 6;
        public int nightSpots = 3;
        public float coastalMin = 14f, coastalMax = 30f;     // kıyıdan uzaklık
        public float deepMin = 45f, deepMax = 110f;
        public float worldRadius = 260f;
        public int seed = 1337;
        public float respawnDelay = 90f;                     // stok bitince yeniden doğma (s)

        readonly List<FishingSpot> spots = new List<FishingSpot>();
        readonly List<Collider> islands = new List<Collider>();
        Material ringMat, splashMat;
        Mesh ringMesh;
        System.Random rng;

        public IReadOnlyList<FishingSpot> Spots => spots;

        void Start()
        {
            rng = new System.Random(seed);
            foreach (var mc in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                if (mc.transform.root.name.Contains("WORLD") && mc.bounds.size.magnitude > 8f) islands.Add(mc);

            BuildAssets();
            for (int i = 0; i < coastalSpots; i++) Spawn(Habitat.Coastal, false);
            for (int i = 0; i < deepSpots; i++) Spawn(Habitat.Deep, false);
            for (int i = 0; i < nightSpots; i++) Spawn(Habitat.Night, true);
        }

        void Update()
        {
            // Tükenen noktaları zamanla başka yere taşı
            foreach (var s in spots)
            {
                if (!s.Depleted) continue;
                if (!s.TryGetComponent<RespawnTimer>(out var t)) { t = s.gameObject.AddComponent<RespawnTimer>(); t.at = Time.time + respawnDelay; }
                else if (Time.time >= t.at)
                {
                    Destroy(t);
                    Place(s);
                    s.stock = s.habitat == Habitat.Deep ? 2 : 3;
                }
            }
        }

        class RespawnTimer : MonoBehaviour { public float at; }

        void Spawn(Habitat h, bool nightOnly)
        {
            var go = new GameObject($"Balik Noktasi ({h})");
            go.transform.SetParent(transform, false);
            var spot = go.AddComponent<FishingSpot>();
            spot.habitat = h;
            spot.nightOnly = nightOnly;
            spot.stock = h == Habitat.Deep ? 2 : 3;

            // İki halka (farklı fazda genişleyip söner)
            spot.rings = new Transform[2];
            for (int ri = 0; ri < 2; ri++)
            {
                var ring = new GameObject("Halka " + ri);
                ring.transform.SetParent(go.transform, false);
                ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                var mr = ring.AddComponent<MeshRenderer>();
                mr.sharedMaterial = ringMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                spot.rings[ri] = ring.transform;
            }

            // Serpinti
            var ps = new GameObject("Serpinti").AddComponent<ParticleSystem>();
            ps.transform.SetParent(go.transform, false);
            ps.transform.localPosition = Vector3.up * 0.2f;
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.7f; main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.gravityModifier = 1.4f; main.maxParticles = 60;
            main.startColor = new Color(0.95f, 1f, 1f, 0.9f);
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.4f;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = splashMat; pr.renderMode = ParticleSystemRenderMode.Billboard;
            spot.splash = ps;

            Place(spot);
            spots.Add(spot);
        }

        void Place(FishingSpot spot)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Vector3 p;
                float shoreDist;
                if (spot.habitat == Habitat.Coastal && islands.Count > 0)
                {
                    var isl = islands[rng.Next(islands.Count)];
                    var c = isl.bounds.center; c.y = 0f;
                    float r = Mathf.Max(isl.bounds.extents.x, isl.bounds.extents.z);
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float d = r + coastalMin + (float)rng.NextDouble() * (coastalMax - coastalMin);
                    p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * d;
                }
                else
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float d = 40f + (float)rng.NextDouble() * (worldRadius - 40f);
                    p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * d;
                }
                p.y = 0f;
                // Su mu? Nokta ve çevresi (halka yarıçapı) karaya/kayaya değmemeli.
                if (!IsClearWater(p, spot.habitat == Habitat.Coastal ? 9f : 14f)) continue;
                shoreDist = DistanceToLand(p);
                bool ok = spot.habitat == Habitat.Coastal ? shoreDist < coastalMax + 12f
                                                          : shoreDist > deepMin;
                if (!ok) continue;
                // Limanın dibinde olmasın
                var harbor = GameSession.Instance != null ? GameSession.Instance.homeHarbor : null;
                if (harbor != null && Vector3.Distance(p, harbor.transform.position) < 40f) continue;
                // Diğer noktalara çok yakın olmasın
                bool crowded = false;
                foreach (var o in spots) if (o != spot && (o.transform.position - p).sqrMagnitude < 18f * 18f) { crowded = true; break; }
                if (crowded) continue;

                spot.transform.position = p;
                return;
            }
            spot.transform.position = new Vector3((float)rng.NextDouble() * 100f - 50f, 0f, (float)rng.NextDouble() * 100f + 60f);
        }

        /// <summary>
        /// Noktanın altında ve çevresinde (8 yön × radius) collider yoksa su kabul edilir.
        /// Suyun collider'ı olmadığı için "aşağı ışın bir şeye çarpıyorsa kara" kuralı yeterli.
        /// </summary>
        static bool IsClearWater(Vector3 p, float radius)
        {
            if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore)) return false;
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (Physics.Raycast(q + Vector3.up * 60f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        /// <summary>Kıyıya yaklaşık uzaklık: ada merkezine 2B mesafe − ada yarıçapı (bounds'tan).</summary>
        float DistanceToLand(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var c in islands)
            {
                var center = c.bounds.center; center.y = 0f;
                float r = Mathf.Max(c.bounds.extents.x, c.bounds.extents.z) * 0.85f;
                best = Mathf.Min(best, Mathf.Max(0f, Vector3.Distance(p, center) - r));
            }
            return best;
        }

        void BuildAssets()
        {
            var shader = Shader.Find("Dredge/Wake Foam");
            ringMat = new Material(shader) { name = "FishingRing (runtime)" };
            ringMat.SetTexture("_BaseMap", RingTexture());
            ringMat.SetFloat("_SoftFade", 0.5f);

            splashMat = new Material(shader) { name = "Splash (runtime)" };
            splashMat.SetTexture("_BaseMap", DotTexture());
            splashMat.SetFloat("_SoftFade", 0.3f);

            ringMesh = new Mesh { name = "FishingRing" };
            float s = 6f;
            ringMesh.vertices = new[] { new Vector3(-s, 0, -s), new Vector3(s, 0, -s), new Vector3(s, 0, s), new Vector3(-s, 0, s) };
            ringMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            ringMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            ringMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            ringMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            ringMesh.RecalculateBounds();
        }

        static Texture2D RingTexture()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                // tek ince yumuşak halka, hafif açısal dalgalanma
                float ring1 = Mathf.Exp(-Mathf.Pow((r - 0.80f) / 0.035f, 2f));
                float dash = 0.75f + 0.25f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + 10f, Mathf.Sin(ang) * 2f + 10f);
                float a = Mathf.Clamp01(ring1 * dash);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static Texture2D DotTexture()
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        /// <summary>Tekneye en yakın, menzildeki kullanılabilir nokta.</summary>
        public FishingSpot Nearest(Vector3 pos)
        {
            FishingSpot best = null; float bd = float.MaxValue;
            foreach (var s in spots)
            {
                if (!s.IsAvailable) continue;
                float d = Vector3.Distance(pos, s.transform.position);
                if (d < s.interactRadius && d < bd) { bd = d; best = s; }
            }
            return best;
        }
    }
}
