using System.Collections.Generic;
using DredgeLook;
using UnityEngine;

namespace Dredge
{
    /// <summary>
    /// <see cref="DredgeLookSettings"/> asset'ini her kare sahneye uygular — editörde ve
    /// Play'de. Asset'te (Project penceresinden seçip Inspector'da) değiştirdiğin her değer
    /// anında görünür; Play'den çıkınca da kaybolmaz çünkü asset'te durur.
    ///
    /// Tek doğruluk kaynağı bu asset'tir: StylizedAtmosphere'in Live Values'ı buradan yazılır.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Dredge/Dredge Look Live")]
    public class DredgeLookLive : MonoBehaviour
    {
        public DredgeLookSettings settings;

        [Header("Bağlantılar (kurucu doldurur)")]
        public StylizedAtmosphere atmosphere;
        public Material waterMaterial;
        public PlanarReflection reflection;
        public Camera mainCamera;
        public SeaAudio seaAudio;
        public EngineAudio engineAudio;
        public List<Material> stylizedMaterials = new List<Material>();

        static readonly int CrestFoam = Shader.PropertyToID("_CrestFoam");
        static readonly int FoamSoftness = Shader.PropertyToID("_FoamSoftness");
        static readonly int NormalStrength = Shader.PropertyToID("_NormalStrength");
        static readonly int SpecularPower = Shader.PropertyToID("_SpecularPower");
        static readonly int SpecularSoftness = Shader.PropertyToID("_SpecularSoftness");
        static readonly int AlphaShallow = Shader.PropertyToID("_AlphaShallow");
        static readonly int WaveFadeDistance = Shader.PropertyToID("_WaveFadeDistance");
        static readonly int ReflectionBase = Shader.PropertyToID("_ReflectionBase");
        static readonly int PlanarStrength = Shader.PropertyToID("_PlanarStrength");
        static readonly int PlanarDistortion = Shader.PropertyToID("_PlanarDistortion");

        static readonly int Bands = Shader.PropertyToID("_Bands");
        static readonly int BandSoftness = Shader.PropertyToID("_BandSoftness");
        static readonly int LightWrap = Shader.PropertyToID("_LightWrap");
        static readonly int ShadowStrength = Shader.PropertyToID("_ShadowStrength");
        static readonly int AmbientStrength = Shader.PropertyToID("_AmbientStrength");
        static readonly int RimStrength = Shader.PropertyToID("_RimStrength");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Update() { ApplyNow(); }

        public void ApplyNow()
        {
            if (settings == null) return;
            var s = settings;

            // Atmosfer: asset → Live Values (StylizedAtmosphere kendi Update'inde sahneye basar)
            if (atmosphere != null)
            {
                atmosphere.usePresets = false;
                atmosphere.continuousUpdate = true;
                atmosphere.values = s.atmosphere;
            }

            if (waterMaterial != null)
            {
                waterMaterial.SetFloat(CrestFoam, s.crestFoam);
                waterMaterial.SetFloat(FoamSoftness, s.foamSoftness);
                waterMaterial.SetFloat(NormalStrength, s.normalStrength);
                waterMaterial.SetFloat(SpecularPower, s.specularPower);
                waterMaterial.SetFloat(SpecularSoftness, s.specularSoftness);
                waterMaterial.SetFloat(AlphaShallow, s.alphaShallow);
                waterMaterial.SetFloat(WaveFadeDistance, s.waveFadeDistance);
                waterMaterial.SetFloat(ReflectionBase, s.reflectionBase);
                waterMaterial.SetFloat(PlanarStrength, s.planarStrength);
                waterMaterial.SetFloat(PlanarDistortion, s.planarDistortion);
            }

            if (reflection != null && !Mathf.Approximately(reflection.textureScale, s.reflectionTextureScale))
                reflection.textureScale = s.reflectionTextureScale;

            foreach (var m in stylizedMaterials)
            {
                if (m == null) continue;
                m.SetFloat(Bands, s.bands);
                m.SetFloat(BandSoftness, s.bandSoftness);
                m.SetFloat(LightWrap, s.lightWrap);
                m.SetFloat(ShadowStrength, s.shadowStrength);
                m.SetFloat(AmbientStrength, s.ambientStrength);
                m.SetFloat(RimStrength, s.rimStrength);

                string n = m.name;
                string ln = n.ToLowerInvariant();
                if (ln.Contains("leafsfall")) m.SetColor(BaseColor, s.autumnColor);
                else if (ln.Contains("leafs") || ln.Contains("grass")) m.SetColor(BaseColor, s.pineColor);
                else if (ln.Contains("woodbark") || ln.Contains("woodbirch") || ln.Contains("wooddark")) m.SetColor(BaseColor, s.trunkColor);
                else if (ln.Contains("_defaultmat") || ln.Contains("dirt")) m.SetColor(BaseColor, s.rockColor);
                else if (ln.Contains("_green.")) m.SetColor(BaseColor, s.pineColor * (ln.EndsWith("2") || ln.EndsWith("5") || ln.EndsWith("8") ? 1.25f : 1f));
                else if (ln.Contains("_brown.")) m.SetColor(BaseColor, s.trunkColor);
                else if (ln.Contains("_orange.") || ln.Contains("_gray.")) m.SetColor(BaseColor, s.rockColor);
                else if (ln.Contains("_purple.")) m.SetColor(BaseColor, new Color(0.42f, 0.36f, 0.52f));
                else if (ln.Contains("_pink.")) m.SetColor(BaseColor, new Color(0.62f, 0.42f, 0.48f));
                else if (ln.Contains("_yellow.")) m.SetColor(BaseColor, new Color(0.70f, 0.60f, 0.34f));
                else if (n.Contains("Rock")) m.SetColor(BaseColor, s.rockColor);
                else if (n.Contains("FoliageAutumn")) { m.SetColor(BaseColor, s.autumnColor); m.SetFloat(ShadowStrength, Mathf.Min(1f, s.shadowStrength + 0.05f)); }
                else if (n.Contains("Foliage")) { m.SetColor(BaseColor, s.pineColor); m.SetFloat(ShadowStrength, Mathf.Min(1f, s.shadowStrength + 0.05f)); }
                else if (n.Contains("Trunk")) m.SetColor(BaseColor, s.trunkColor);
                else if (n.Contains("Cloud"))
                {
                    m.SetColor(BaseColor, s.cloudColor);
                    m.SetFloat(Bands, 2f);
                    m.SetFloat(ShadowStrength, s.cloudShadowStrength);
                    m.SetFloat(LightWrap, 0.45f);
                    m.SetFloat(AmbientStrength, 1.1f);
                }
            }

            if (mainCamera != null)
            {
                if (!Mathf.Approximately(mainCamera.fieldOfView, s.fieldOfView)) mainCamera.fieldOfView = s.fieldOfView;
                if (!Mathf.Approximately(mainCamera.farClipPlane, s.farClip)) mainCamera.farClipPlane = s.farClip;
            }

            if (seaAudio != null) seaAudio.volume = s.seaVolume;
            if (engineAudio != null) engineAudio.volume = s.engineVolume;
        }
    }
}
