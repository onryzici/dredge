using UnityEngine;
using UnityEngine.InputSystem;

namespace Dredge.Game
{
    /// <summary>
    /// HUD ve etkileşim (UISkin ile): gün/saat, para, gövde, akıl, ambar özeti,
    /// dünya-uzayı balık işareti, ipuçları; [E] limanda menü / balık noktasında mini-oyun;
    /// [TAB] ambar (Kenney balık ikonlarıyla).
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public FishingSpotSpawner spawner;
        public FishingMinigame minigame;
        public float markerRange = 90f;

        bool inventoryOpen;
        public bool InventoryOpen => inventoryOpen;
        public void ToggleInventory() { inventoryOpen = !inventoryOpen; GameSession.Instance.SetMode(inventoryOpen ? GameMode.Inventory : GameMode.Sailing); }

        Texture2D ringTex;

        void Update()
        {
            var s = GameSession.Instance;
            var kb = Keyboard.current;
            if (s == null || kb == null || s.boat == null) return;
            if (minigame != null && minigame.Active) return;
            if (s.story != null && s.story.AnyUIOpen) return;

            if (kb.tabKey.wasPressedThisFrame) ToggleInventory();
            if (inventoryOpen) { if (kb.escapeKey.wasPressedThisFrame) ToggleInventory(); return; }

            if (kb.eKey.wasPressedThisFrame) TryInteract();
        }

        static bool AtHarbor(GameSession s) => s.homeHarbor != null && s.homeHarbor.InRange(s.boat.transform.position);

        public void TryInteract()
        {
            var s = GameSession.Instance;
            if (s == null || s.boat == null) return;
            var pos = s.boat.transform.position;
            if (AtHarbor(s))
            {
                if (s.story != null) s.story.OpenHarborMenu();
                else s.homeHarbor.SellAll(s);
                return;
            }
            var spot = spawner != null ? spawner.Nearest(pos) : null;
            if (spot == null) return;
            if (Mathf.Abs(s.boat.Speed) > 1.5f) { s.Notify("Balık tutmak için yavaşla ([S] fren)."); return; }
            minigame.Begin(spot);
        }

        // ------------------------------------------------------------------ çizim

        void OnGUI()
        {
            var s = GameSession.Instance;
            if (s == null) return;
            UISkin.Ensure();
            EnsureRing();
            if (s.story != null && !s.story.introDone) return;   // açılış ekranı tek başına

            float W = Screen.width, H = Screen.height, u = UISkin.U;
            bool fishing = minigame != null && minigame.Active;
            bool storyUI = s.story != null && s.story.AnyUIOpen;
            Vector3 pos = s.boat != null ? s.boat.transform.position : Vector3.zero;

            // Ekran işareti kaldırıldı: nokta sudaki halkalarla keşfedilir; menzilde alttaki [E] ipucu çıkar.

            // Üst orta: gün & saat
            var top = new Rect(W / 2 - 130 * u, 14 * u, 260 * u, 78 * u);
            UISkin.Panel(top);
            GUI.Label(new Rect(top.x, top.y + 12 * u, top.width, 22 * u), $"GÜN {s.clock.day}" + (s.clock.Night01 > 0.5f ? "   ·   GECE" : ""), new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(top.x, top.y + 32 * u, top.width, 40 * u), s.clock.TimeString, UISkin.Big);

            // Sol üst: para & barlar
            var left = new Rect(16 * u, 14 * u, 300 * u, 150 * u);
            UISkin.Panel(left);
            GUI.Label(new Rect(left.x + 24 * u, left.y + 16 * u, 260 * u, 32 * u), $"₺ {s.money:N0}", UISkin.Header);
            UISkin.Bar(new Rect(left.x + 24 * u, left.y + 78 * u, 252 * u, 16 * u), s.damage != null ? s.damage.hull / s.damage.maxHull : 1f, new Color(0.90f, 0.50f, 0.32f), "GÖVDE", s.damage != null ? $"{Mathf.RoundToInt(s.damage.hull)}/{Mathf.RoundToInt(s.damage.maxHull)}" : null);
            float sanity = s.panic != null ? 1f - s.panic.Panic01 : 1f;
            UISkin.Bar(new Rect(left.x + 24 * u, left.y + 122 * u, 252 * u, 16 * u), sanity, new Color(0.55f, 0.72f, 0.98f), "AKIL", $"{Mathf.RoundToInt(sanity * 100)}%");

            // Sağ alt: ambar
            var inv = new Rect(W - 276 * u, H - 92 * u, 260 * u, 76 * u);
            UISkin.Panel(inv);
            GUI.Label(new Rect(inv.x + 20 * u, inv.y + 12 * u, 230 * u, 24 * u), $"AMBAR   {s.inventory.Count} balık · {s.inventory.TotalValue()}₺", UISkin.Body);
            GUI.Label(new Rect(inv.x + 20 * u, inv.y + 40 * u, 230 * u, 22 * u), "[TAB] ambar    [J] günlük", UISkin.Small);

            // Alt orta: ipucu + bildirim
            string hint = null;
            if (fishing || storyUI) hint = null;
            else if (inventoryOpen) hint = "[TAB] kapat";
            else if (AtHarbor(s)) hint = "KUZEY MARLİN LİMANI     [E] iskeleye çık";
            else
            {
                var spot = spawner != null ? spawner.Nearest(pos) : null;
                if (spot != null)
                    hint = Mathf.Abs(s.boat.Speed) > 1.5f ? "Balık noktası — yavaşla ([S]), sonra [E]"
                         : spot.habitat == Habitat.Night ? "[E]  ...bir şey kıpırdıyor" : "[E]  balık tut";
            }
            if (hint != null)
            {
                var hr = new Rect(W / 2 - 340 * u, H - 78 * u, 680 * u, 52 * u);
                UISkin.Panel(hr);
                GUI.Label(new Rect(hr.x, hr.y, hr.width, hr.height), hint, UISkin.Center);
            }
            if (s.HasNotice && !storyUI)
            {
                float a = Mathf.Clamp01((s.noticeUntil - Time.time) / 0.6f);
                var nr = new Rect(W / 2 - 380 * u, H - 144 * u, 760 * u, 54 * u);
                UISkin.Panel(nr, a);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(nr.x + 16 * u, nr.y, nr.width - 32 * u, nr.height), s.notice, UISkin.Center);
                GUI.color = Color.white;
            }

            if (inventoryOpen) DrawInventory(s, u);

            if (s.panic != null && s.panic.Panic01 > 0.01f)
                UISkin.FullscreenTint(new Color(0.02f, 0f, 0.02f, s.panic.Panic01 * 0.35f));
        }

        void DrawSpotMarker(GameSession s, Vector3 pos, float u)
        {
            var cam = Camera.main; if (cam == null) return;
            FishingSpot best = null; float bd = float.MaxValue;
            foreach (var sp in spawner.Spots)
            {
                if (!sp.IsAvailable) continue;
                float d = Vector3.Distance(pos, sp.transform.position);
                if (d < bd) { bd = d; best = sp; }
            }
            if (best == null || bd > markerRange) return;
            var sp3 = cam.WorldToScreenPoint(best.transform.position + Vector3.up * 1.2f);
            if (sp3.z < 0f) return;
            float x = sp3.x, y = Screen.height - sp3.y;
            bool inRange = bd < best.interactRadius;
            float size = Mathf.Lerp(28f, 46f, Mathf.Clamp01(1f - bd / markerRange)) * u;
            GUI.color = inRange ? UISkin.Good : (best.habitat == Habitat.Night ? UISkin.Bad : new Color(1f, 1f, 1f, 0.85f));
            GUI.DrawTexture(new Rect(x - size / 2f, y - size / 2f, size, size), ringTex);
            GUI.color = Color.white;
            string txt = best.habitat == Habitat.Night ? "?" : best.habitat == Habitat.Deep ? "Derin balık" : "Balık";
            var st = new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(x - 110 * u, y + size / 2f, 220 * u, 22 * u), inRange ? $"{txt}   [E]" : $"{txt}   {bd:0} m", st);
        }

        void DrawInventory(GameSession s, float u)
        {
            var inv = s.inventory;
            float cell = 84f * u, pad = 5f * u;
            float gw = inv.columns * cell, gh = inv.rows * cell;
            var origin = new Vector2(Screen.width / 2f - gw / 2f, Screen.height / 2f - gh / 2f + 10 * u);
            UISkin.Panel(new Rect(origin.x - 40 * u, origin.y - 96 * u, gw + 80 * u, gh + 150 * u), 1f, true);
            GUI.Label(new Rect(origin.x, origin.y - 84 * u, gw, 40 * u), "AMBAR", UISkin.Title);
            UISkin.DividerLine(new Rect(origin.x + gw / 2 - 110 * u, origin.y - 44 * u, 220 * u, 22 * u));

            for (int y = 0; y < inv.rows; y++)
            for (int x = 0; x < inv.columns; x++)
            {
                var c = new Rect(origin.x + x * cell + pad, origin.y + y * cell + pad, cell - pad * 2, cell - pad * 2);
                GUI.color = new Color(1f, 1f, 1f, 0.06f); GUI.DrawTexture(c, UISkin.White);
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(new Rect(c.x, c.y, c.width, 1), UISkin.White); GUI.DrawTexture(new Rect(c.x, c.y, 1, c.height), UISkin.White);
            }
            foreach (var it in inv.items)
            {
                if (it.gridX < 0) continue;
                var r = new Rect(origin.x + it.gridX * cell + pad, origin.y + it.gridY * cell + pad, it.width * cell - pad * 2, it.height * cell - pad * 2);
                GUI.color = it.aberration ? new Color(0.35f, 0.10f, 0.12f, 0.9f) : new Color(0.10f, 0.20f, 0.28f, 0.9f);
                GUI.DrawTexture(r, UISkin.White);
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), UISkin.White); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), UISkin.White);
                GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), UISkin.White); GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), UISkin.White);
                GUI.color = Color.white;
                var icon = UISkin.FishIcon(it.speciesName);
                if (icon != null)
                {
                    // ikon: hücreye sığdır, dikey balıkları döndür
                    float iw = r.width - 12 * u, ih = r.height - 30 * u;
                    var ir = new Rect(r.x + 6 * u, r.y + 4 * u, iw, ih);
                    if (it.height > it.width)
                    {
                        var prev = GUI.matrix;
                        GUIUtility.RotateAroundPivot(90f, ir.center);
                        GUI.DrawTexture(new Rect(ir.center.x - ih / 2, ir.center.y - iw / 2, ih, iw), icon, ScaleMode.ScaleToFit);
                        GUI.matrix = prev;
                    }
                    else GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit);
                }
                GUI.Label(new Rect(r.x + 6 * u, r.yMax - 26 * u, r.width - 12 * u, 22 * u), $"{it.speciesName}  <color=#ffcc73>{it.value}₺</color>", UISkin.Small);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(origin.x, origin.y + gh + 12 * u, gw, 26 * u), $"Toplam {inv.TotalValue()}₺   ·   {inv.columns}×{inv.rows} ızgara   ·   limanda [E] ile sat", new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter });
        }

        void EnsureRing()
        {
            if (ringTex != null) return;
            const int N = 64;
            ringTex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float a = (x + 0.5f) / N * 2f - 1f, b = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(a * a + b * b);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.09f, 2f)) + Mathf.Clamp01((0.22f - r) * 12f);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ring) * 255f));
            }
            ringTex.SetPixels32(px); ringTex.Apply();
        }
    }
}
