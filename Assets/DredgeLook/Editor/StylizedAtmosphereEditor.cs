using UnityEditor;
using UnityEngine;

namespace DredgeLook.EditorTools
{
    [CustomEditor(typeof(StylizedAtmosphere))]
    public class StylizedAtmosphereEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var atmo = (StylizedAtmosphere)target;

            EditorGUILayout.HelpBox(
                "AYAR SIRASI (bozma):\n" +
                "1) Post Processing'i kapat\n" +
                "2) Gökyüzü gradyanı\n" +
                "3) Sis rengini ufuk rengine eşitle, density'yi aç\n" +
                "4) Güneş açısı (elevation 12-25° dramatik)\n" +
                "5) Ambient 3 renk\n" +
                "6) Su\n" +
                "7) En son Post Processing",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
                atmo.Apply();

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sis Rengini Ufuk Rengine Esitle", GUILayout.Height(24)))
                {
                    Undo.RecordObject(atmo, "Match Fog To Horizon");
                    if (atmo.usePresets && atmo.presetA != null)
                    {
                        Undo.RecordObject(atmo.presetA, "Match Fog");
                        atmo.presetA.values.fogColor = atmo.presetA.values.skyHorizon;
                        EditorUtility.SetDirty(atmo.presetA);
                    }
                    else
                    {
                        atmo.values.fogColor = atmo.values.skyHorizon;
                    }
                    atmo.Apply();
                }

                if (GUILayout.Button("Sahnedeki Su Materyallerini Bul", GUILayout.Height(24)))
                {
                    Undo.RecordObject(atmo, "Collect Water Materials");
                    atmo.CollectWaterMaterials();
                    atmo.Apply();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preset A -> Live Values"))
                {
                    Undo.RecordObject(atmo, "Copy Preset");
                    atmo.CopyFromPresetA();
                    atmo.Apply();
                }
                if (GUILayout.Button("Live Values -> Preset A"))
                {
                    atmo.SaveToPresetA();
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Simdi Uygula", GUILayout.Height(22)))
                atmo.Apply();

            // Sık yapılan hataların uyarıları
            var v = atmo.Current;
            float dist = ColorDistance(v.fogColor, v.skyHorizon);
            if (dist > 0.12f)
            {
                EditorGUILayout.HelpBox(
                    "Sis rengi ufuk renginden belirgin şekilde farklı. DREDGE görünümünde bu ikisi " +
                    "neredeyse aynıdır; farklı olursa ufuk çizgisi kaybolur ve sahne 'kirli cam' gibi görünür.",
                    MessageType.Warning);
            }

            if (Brightness(v.ambientSky) > v.sunIntensity * 0.75f)
            {
                EditorGUILayout.HelpBox(
                    "Ambient, güneşe göre çok güçlü. Bu, kontrastı öldürür ve sahneyi düz gösterir. " +
                    "ambientSky'ı düşür veya sunIntensity'yi yükselt.",
                    MessageType.Warning);
            }

            if (v.fogDensity < 0.004f)
            {
                EditorGUILayout.HelpBox(
                    "Sis çok zayıf. Atmosferik perspektif olmadan derinlik hissi oluşmaz (0.010-0.020 arası dene).",
                    MessageType.Warning);
            }
        }

        static float Brightness(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static float ColorDistance(Color a, Color b) =>
            (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3f;
    }
}
