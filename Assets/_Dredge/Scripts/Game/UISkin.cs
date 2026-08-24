using System.Collections.Generic;
using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Oyun UI'sının tek görsel dili: Kenney Future fontu, Fantasy UI border çerçeveleri
    /// (Resources/UI), Kenney Fish Pack ikonları (Resources/Fish). IMGUI üzerinde çalışır.
    /// Bütün ekranlar (HUD, ambar, diyalog, mini-oyun, günlük) buradan çizer.
    /// </summary>
    public static class UISkin
    {
        public static Font Font, FontNarrow;
        public static Texture2D Border, BorderThin, BorderOrnate, Divider, DividerEdge, Star, Dot, White;
        public static GUIStyle Title, Header, Body, BodyDim, Small, Button, ButtonActive, Center, Big, Mono;
        static readonly Dictionary<string, Texture2D> fishIcons = new Dictionary<string, Texture2D>();
        static bool ready;

        public static readonly Color Ink = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color InkDim = new Color(0.66f, 0.74f, 0.82f);
        public static readonly Color Accent = new Color(1f, 0.80f, 0.45f);
        public static readonly Color Good = new Color(0.45f, 1f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.5f, 0.45f);
        public static readonly Color PanelFill = new Color(0.03f, 0.05f, 0.08f, 0.78f);
        public static readonly Color PanelLine = new Color(0.80f, 0.88f, 0.96f, 0.9f);

        public static float U => Screen.height / 1080f;

        public static void Ensure()
        {
            if (ready) return;
            ready = true;
            White = Texture2D.whiteTexture;
            Font = Resources.Load<Font>("Fonts/KenneyFuture");
            FontNarrow = Resources.Load<Font>("Fonts/KenneyFutureNarrow");
            Border = Resources.Load<Texture2D>("UI/panel-transparent-border-010");
            BorderThin = Resources.Load<Texture2D>("UI/panel-transparent-border-000");
            BorderOrnate = Resources.Load<Texture2D>("UI/panel-transparent-border-020");
            Divider = Resources.Load<Texture2D>("UI/divider-000");
            DividerEdge = Resources.Load<Texture2D>("UI/divider-005");
            Star = Resources.Load<Texture2D>("UI/star");
            Dot = Resources.Load<Texture2D>("UI/icon_circle");

            float u = U;
            Body = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * u), alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true };
            Body.normal.textColor = Ink;
            BodyDim = new GUIStyle(Body); BodyDim.normal.textColor = InkDim;
            Small = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(14 * u) }; Small.normal.textColor = InkDim;
            Header = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold }; Header.normal.textColor = Accent;
            Title = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(34 * u), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; Title.normal.textColor = Ink;
            Center = new GUIStyle(Body) { alignment = TextAnchor.MiddleCenter };
            Big = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(30 * u), alignment = TextAnchor.MiddleCenter, font = Font };   // Kenney Future: yalnız rakam/saat
            Mono = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(16 * u) };
            Button = new GUIStyle(Body) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(18 * u) };
            ButtonActive = new GUIStyle(Button); ButtonActive.normal.textColor = Accent;
        }

        public static Texture2D FishIcon(string species)
        {
            Ensure();
            if (fishIcons.TryGetValue(species, out var t)) return t;
            string file = species switch
            {
                "Ringa" => "fish_grey",
                "Uskumru" => "fish_blue",
                "Levrek" => "fish_grey_long_a",
                "Kefal" => "fish_green",
                "Morina" => "fish_brown",
                "Kılıçbalığı" => "fish_grey_long_b",
                "Ahtapot" => "fish_pink",
                "Kör Yılanbalığı" => "fish_blue_skeleton",
                "Kanlı Ringa" => "fish_red_skeleton",
                _ => "fish_orange"
            };
            t = Resources.Load<Texture2D>("Fish/" + file);
            fishIcons[species] = t;
            return t;
        }

        // ------------------------------------------------------------- çizim

        /// <summary>Koyu dolgu + 9 dilimli süslü çerçeve.</summary>
        /// <summary>Sade panel: yarı saydam koyu dolgu, altta ince sıcak çizgi. Süs yok.</summary>
        public static void Panel(Rect r, float alpha = 1f, bool ornate = false)
        {
            Ensure();
            float u = U;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.62f * alpha);
            GUI.DrawTexture(r, White);
            GUI.color = new Color(Accent.r, Accent.g, Accent.b, (ornate ? 0.9f : 0.55f) * alpha);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2 * u, r.width, 2 * u), White);
            GUI.color = Color.white;
        }

        static void NineSlice(Rect r, Texture2D tex, float b)
        {
            GUI.color = PanelLine;
            const float s = 1f / 3f;   // 48 px doku: 16 px kenar
            float w = r.width, h = r.height;
            void Piece(float x, float y, float pw, float ph, float u0, float v0, float uw, float vh)
                => GUI.DrawTextureWithTexCoords(new Rect(r.x + x, r.y + y, pw, ph), tex, new Rect(u0, v0, uw, vh));
            // köşeler
            Piece(0, 0, b, b, 0, 1 - s, s, s);
            Piece(w - b, 0, b, b, 1 - s, 1 - s, s, s);
            Piece(0, h - b, b, b, 0, 0, s, s);
            Piece(w - b, h - b, b, b, 1 - s, 0, s, s);
            // kenarlar
            Piece(b, 0, w - 2 * b, b, s, 1 - s, s, s);
            Piece(b, h - b, w - 2 * b, b, s, 0, s, s);
            Piece(0, b, b, h - 2 * b, 0, s, s, s);
            Piece(w - b, b, b, h - 2 * b, 1 - s, s, s, s);
        }

        public static void Bar(Rect r, float t, Color c, string name, string right = null)
        {
            Ensure();
            float u = U;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, White);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x + 2 * u, r.y + 2 * u, (r.width - 4 * u) * Mathf.Clamp01(t), r.height - 4 * u), White);
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), White); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), White);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), White); GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), White);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y - 22 * u, r.width, 20 * u), name, Small);
            if (right != null)
            {
                var rs = new GUIStyle(Small) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(r.x, r.y - 22 * u, r.width, 20 * u), right, rs);
            }
        }

        public static void DividerLine(Rect r)
        {
            Ensure();
            if (Divider == null) return;
            GUI.color = new Color(PanelLine.r, PanelLine.g, PanelLine.b, 0.7f);
            GUI.DrawTexture(r, Divider, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }

        /// <summary>Klavye ile seçilen menü satırı (▸ işaretli).</summary>
        public static void Choice(Rect r, string text, bool selected, string key)
        {
            Ensure();
            float u = U;
            if (selected)
            {
                GUI.color = new Color(1f, 0.85f, 0.5f, 0.12f);
                GUI.DrawTexture(r, White);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(r.x + 8 * u, r.y, 60 * u, r.height), $"[{key}]", selected ? Header : BodyDim);
            GUI.Label(new Rect(r.x + 56 * u, r.y, r.width - 64 * u, r.height), text, selected ? Body : BodyDim);
        }

        public static void FullscreenTint(Color c)
        {
            GUI.color = c; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), White); GUI.color = Color.white;
        }
    }
}
