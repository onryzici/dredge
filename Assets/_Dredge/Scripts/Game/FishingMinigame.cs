using UnityEngine;
using UnityEngine.InputSystem;

namespace Dredge.Game
{
    /// <summary>
    /// DREDGE tarzı zamanlama mini-oyunu: daire üstünde dönen ibre, 1–3 yeşil dilim.
    /// Dilimdeyken SPACE → vuruş. Gerekli vuruş sayısına ulaşınca balık ambara girer.
    /// Her deneme oyun zamanı harcar; ıskalama daha çok harcar. ESC vazgeç.
    /// </summary>
    public class FishingMinigame : MonoBehaviour
    {
        public bool Active { get; private set; }

        FishingSpot spot;
        FishSpecies species;
        int hits;
        float angle;          // ibre açısı (derece)
        float speed;          // derece/sn
        int direction = 1;
        float[] zoneStart, zoneSize;   // derece
        float feedbackUntil; string feedback; Color feedbackColor;
        float cooldown;
        System.Random rng = new System.Random();

        Texture2D circleTex, dotTex, hookTex;

        public void Begin(FishingSpot s)
        {
            spot = s;
            var clock = GameSession.Instance.clock;
            var habitat = s.habitat;
            if (habitat != Habitat.Night && clock != null && clock.IsNight && rng.NextDouble() < 0.25) habitat = Habitat.Night;   // gecede aberasyon şansı
            species = FishSpecies.Pick(habitat, rng);
            hits = 0;
            angle = 0f;
            speed = Mathf.Lerp(95f, 240f, species.difficulty);
            direction = 1;
            NewZones();
            Active = true;
            feedback = null;
            cooldown = 0.2f;
            GameSession.Instance.SetMode(GameMode.Fishing);
        }

        void NewZones()
        {
            int n = species.difficulty < 0.35f ? 2 : species.difficulty < 0.65f ? 2 : 1;
            zoneStart = new float[n]; zoneSize = new float[n];
            float size = Mathf.Lerp(70f, 28f, species.difficulty);
            for (int i = 0; i < n; i++)
            {
                zoneSize[i] = size;
                zoneStart[i] = (float)rng.NextDouble() * 360f;
            }
        }

        void Update()
        {
            if (!Active) return;
            var kb = Keyboard.current;
            cooldown -= Time.deltaTime;

            angle = (angle + direction * speed * Time.deltaTime) % 360f;
            if (angle < 0f) angle += 360f;

            if (kb != null && kb.escapeKey.wasPressedThisFrame) { End(false, "Vazgeçildi"); return; }
            if (kb == null || cooldown > 0f) return;

            if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
            {
                bool inZone = false;
                for (int i = 0; i < zoneStart.Length; i++)
                {
                    float d = Mathf.Repeat(angle - zoneStart[i], 360f);
                    if (d <= zoneSize[i]) { inZone = true; break; }
                }
                var clock = GameSession.Instance.clock;
                if (inZone)
                {
                    hits++;
                    clock?.AdvanceSmooth(0.15f);                 // her vuruş ~9 dk (yumuşak)
                    Feedback("İyi!", new Color(0.5f, 1f, 0.6f)); hitFlash = 1f;
                    speed *= 1.09f;
                    direction = rng.NextDouble() < 0.35 ? -direction : direction;
                    NewZones();
                    if (hits >= species.hitsNeeded) { Catch(); return; }
                }
                else
                {
                    clock?.AdvanceSmooth(0.5f);                  // ıskalama: 30 dk (yumuşak)
                    Feedback("Kaçtı...", new Color(1f, 0.55f, 0.5f)); missShake = 1f;
                    hits = Mathf.Max(0, hits - 1);
                }
                cooldown = 0.15f;
            }
        }

        void Catch()
        {
            var session = GameSession.Instance;
            var item = new FishItem(species);
            if (!session.inventory.TryAdd(item))
            {
                End(false, $"Ambar dolu — {species.name} sığmadı ({species.width}×{species.height}).");
                return;
            }
            spot.stock--;
            if (item.aberration && session.panic != null) session.panic.OnAberration();
            End(true, $"{species.name} yakalandı (+{species.value}₺ değerinde)");
        }

        void Feedback(string t, Color c) { feedback = t; feedbackColor = c; feedbackUntil = Time.time + 0.7f; }

        void End(bool success, string msg)
        {
            Active = false;
            GameSession.Instance.SetMode(GameMode.Sailing);
            GameSession.Instance.Notify(msg, success ? 3f : 3.5f);
        }

        // ------------------------------------------------------------------ UI

        float hitFlash;      // vuruşta halka nabzı
        float missShake;

        void OnGUI()
        {
            if (!Active) return;
            EnsureTextures();
            UISkin.Ensure();

            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 2.5f);
            missShake = Mathf.MoveTowards(missShake, 0f, Time.deltaTime * 3f);

            float u = UISkin.U;
            float S = Mathf.Min(Screen.width, Screen.height) * 0.30f;      // halka çapı
            var panel = new Rect(Screen.width * 0.5f - 470 * u, Screen.height * 0.5f - 200 * u, 940 * u, 400 * u);
            UISkin.FullscreenTint(new Color(0f, 0.01f, 0.03f, 0.35f));
            // Koyu panel (referans: siyah, ince açık çerçeve)
            GUI.color = new Color(0.04f, 0.035f, 0.04f, 0.92f); GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.75f, 0.6f, 0.35f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2 * u), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - 2 * u, panel.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var center = new Vector2(panel.x + 260 * u, panel.y + panel.height * 0.5f);
            if (missShake > 0f) center += new Vector2(Mathf.Sin(Time.time * 60f) * 6f * missShake, 0f);
            float rad = S * 0.5f;

            // Halka: kestane rengi kalın ray, ince açık kenarlar (referans)
            DrawArc(center, rad, 0f, 360f, S * 0.10f, new Color(0.30f, 0.12f, 0.14f, 1f));
            DrawArc(center, rad + S * 0.055f, 0f, 360f, S * 0.008f, new Color(0.85f, 0.78f, 0.66f, 0.8f));
            DrawArc(center, rad - S * 0.055f, 0f, 360f, S * 0.008f, new Color(0.85f, 0.78f, 0.66f, 0.5f));

            // Yeşil dilimler
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
            for (int i = 0; i < zoneStart.Length; i++)
                DrawArc(center, rad, zoneStart[i], zoneSize[i], S * 0.10f, new Color(0.30f, 0.78f, 0.42f, pulse));

            // İşaretçi: halkanın üstünde küçük kanca simgesi (açık halka + iz)
            {
                for (int k = 1; k <= 5; k++)
                {
                    float ta = (angle - direction * k * 4f) * Mathf.Deg2Rad;
                    var tp = center + new Vector2(Mathf.Cos(ta), -Mathf.Sin(ta)) * rad;
                    float td = S * 0.045f * (1f - k / 6f);
                    GUI.color = new Color(0.95f, 0.85f, 0.7f, 0.45f * (1f - k / 6f));
                    GUI.DrawTexture(new Rect(tp.x - td / 2f, tp.y - td / 2f, td, td), dotTex);
                }
                float a = angle * Mathf.Deg2Rad;
                var tip = center + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * rad;
                float d = S * 0.14f * (1f + hitFlash * 0.3f);
                GUI.color = new Color(0.95f, 0.88f, 0.75f, 1f);
                GUI.DrawTexture(new Rect(tip.x - d / 2f, tip.y - d / 2f, d, d), hookTex);
            }
            if (hitFlash > 0f)
                DrawArc(center, rad + (1f - hitFlash) * S * 0.22f, 0f, 360f, S * 0.02f, new Color(0.6f, 1f, 0.75f, hitFlash));

            // Merkez: balık silüeti
            var icon = UISkin.FishIcon(species.name);
            if (icon != null)
            {
                float iw = S * 0.46f, ih = S * 0.23f;
                GUI.color = new Color(0.96f, 0.96f, 0.96f, 1f);
                GUI.DrawTexture(new Rect(center.x - iw / 2f, center.y - ih / 2f, iw, ih), icon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            // Sağ: bilgi
            float tx = panel.x + 540 * u, ty = panel.y + 60 * u;
            var big = new GUIStyle(UISkin.Title) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(34 * u) };
            GUI.Label(new Rect(tx, ty, 380 * u, 44 * u), spot.habitat == Habitat.Night ? "KARANLIK SU" : "KIPIRDANAN SU", big); ty += 62 * u;
            string stockTxt = spot.stock >= 3 ? "<color=#73ff99>Yüksek</color>" : spot.stock == 2 ? "<color=#ffd973>Orta</color>" : "<color=#ff8a73>Düşük</color>";
            GUI.Label(new Rect(tx, ty, 380 * u, 30 * u), "Stok: " + stockTxt, new GUIStyle(UISkin.Body) { fontSize = Mathf.RoundToInt(22 * u) }); ty += 42 * u;
            string tag = spot.habitat == Habitat.Deep ? "DERİN" : spot.habitat == Habitat.Night ? "GECE" : "KIYI";
            var tagRect = new Rect(tx, ty, 120 * u, 30 * u);
            GUI.color = spot.habitat == Habitat.Night ? new Color(0.45f, 0.15f, 0.2f) : new Color(0.28f, 0.22f, 0.52f);
            GUI.DrawTexture(tagRect, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(tagRect, tag, new GUIStyle(UISkin.Center) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(16 * u) }); ty += 44 * u;
            GUI.Label(new Rect(tx, ty, 380 * u, 28 * u), species.name + "   ·   " + hits + "/" + species.hitsNeeded, UISkin.BodyDim); ty += 40 * u;

            // "Çek" butonu (referans: kırmızı buton + tuş)
            var btn = new Rect(tx, panel.yMax - 90 * u, 300 * u, 54 * u);
            GUI.color = new Color(0.40f, 0.14f, 0.16f, 1f); GUI.DrawTexture(btn, Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.75f, 0.6f, 0.5f); GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, 1), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(btn.x, btn.yMax - 1, btn.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(btn.x + 16 * u, btn.y, 180 * u, btn.height), "Çek", new GUIStyle(UISkin.Body) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold });
            var key = new Rect(btn.xMax - 90 * u, btn.y + 11 * u, 70 * u, 32 * u);
            GUI.color = new Color(0.9f, 0.85f, 0.75f, 1f); GUI.DrawTexture(key, Texture2D.whiteTexture); GUI.color = Color.white;
            var ks = new GUIStyle(UISkin.Center) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(15 * u) }; ks.normal.textColor = new Color(0.1f, 0.08f, 0.08f);
            GUI.Label(key, "SPACE", ks);

            if (Time.time < feedbackUntil && feedback != null)
            {
                var fb = new GUIStyle(UISkin.Body) { fontSize = Mathf.RoundToInt(20 * u), fontStyle = FontStyle.Bold }; fb.normal.textColor = feedbackColor;
                GUI.Label(new Rect(tx + 320 * u, panel.yMax - 84 * u, 200 * u, 40 * u), feedback, fb);
            }
            GUI.Label(new Rect(panel.x, panel.yMax + 8 * u, panel.width, 24 * u), "[ESC] vazgeç", new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>Halka yayı: küçük döndürülmüş dikdörtgenlerle pürüzsüz bant.</summary>
        void DrawArc(Vector2 c, float r, float startDeg, float sizeDeg, float thickness, Color col)
        {
            GUI.color = col;
            float stepDeg = Mathf.Max(1.5f, 180f / Mathf.PI * (thickness * 0.5f) / r);
            int steps = Mathf.CeilToInt(sizeDeg / stepDeg);
            float segLen = r * stepDeg * Mathf.Deg2Rad * 1.35f;
            for (int k = 0; k <= steps; k++)
            {
                float a = startDeg + Mathf.Min(k * stepDeg, sizeDeg);
                var p = c + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), -Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                var prev = GUI.matrix;
                GUIUtility.RotateAroundPivot(-a + 90f, p);
                GUI.DrawTexture(new Rect(p.x - thickness / 2f, p.y - segLen / 2f, thickness, segLen), Texture2D.whiteTexture);
                GUI.matrix = prev;
            }
            GUI.color = Color.white;
        }

        void EnsureTextures()
        {
            if (dotTex != null) return;
            dotTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            hookTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var dp = new Color32[64 * 64];
            var hp = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x + 0.5f) / 64f * 2f - 1f, v = (y + 0.5f) / 64f * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((0.98f - r) * 12f);
                dp[y * 64 + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.62f) / 0.16f);
                float core = Mathf.Clamp01((0.22f - r) * 10f);
                hp[y * 64 + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Mathf.Max(ring, core)) * 255f));
            }
            dotTex.SetPixels32(dp); dotTex.Apply();
            hookTex.SetPixels32(hp); hookTex.Apply();
        }
    }
}
