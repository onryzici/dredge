using UnityEngine;

namespace DredgeLook
{
    /// <summary>
    /// Tek bir atmosfer durumunu saklayan asset.
    /// Assets > Create > Dredge Look > Atmosphere Preset
    /// </summary>
    [CreateAssetMenu(fileName = "Atmosphere_", menuName = "Dredge Look/Atmosphere Preset", order = 0)]
    public class AtmospherePreset : ScriptableObject
    {
        [Tooltip("Bu preset'in tüm görsel değerleri. Sahnedeki StylizedAtmosphere bunları uygular.")]
        public AtmosphereValues values = AtmosphereValues.Default;

        private void Reset()
        {
            values = AtmosphereValues.Default;
        }
    }
}
