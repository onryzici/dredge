using UnityEngine;

namespace DredgeLook
{
    /// <summary>
    /// Tekneyi dalgaların üstünde sallar. Fizik simülasyonu değil — DREDGE'deki gibi
    /// "kinematik" ve okunaklı bir sallanma. Rigidbody gerektirmez.
    ///
    /// Kural: teknenin sallanması dalgadan HAFİFÇE gecikmeli olmalı (smoothing),
    /// birebir takip ederse oyuncakvari görünür.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Dredge Look/Buoyant Object")]
    public class BuoyantObject : MonoBehaviour
    {
        [Tooltip("Boşsa sahnedeki WaterSurface bulunur.")]
        public WaterSurface water;

        [Tooltip("Editörde de çalışsın (Play'e basmadan). Sahneyi sürekli dirty yapar, " +
                 "sadece ayar yaparken aç.")]
        public bool simulateInEditMode = false;

        [Header("Ölçüler")]
        [Tooltip("Teknenin uzunluğu (ileri-geri örnekleme mesafesi).")]
        public float length = 4f;
        [Tooltip("Teknenin genişliği (sağ-sol örnekleme mesafesi).")]
        public float width = 1.8f;
        [Tooltip("Su hattı ofseti. Tekne çok batıyorsa artır.")]
        public float waterlineOffset = 0.15f;

        [Header("Tepki")]
        [Range(0.5f, 20f)] public float heightSmooth = 6f;
        [Range(0.5f, 20f)] public float rotationSmooth = 4f;
        [Tooltip("Eğilmenin abartılması. 1 = gerçek eğim, 1.5-2 daha okunaklı.")]
        [Range(0f, 3f)] public float tiltExaggeration = 1.4f;
        [Tooltip("Maksimum eğim açısı (derece). Sınırlamazsan yüksek dalgada takla atar.")]
        [Range(0f, 45f)] public float maxTilt = 14f;

        [Header("Ekstra Hareket")]
        [Tooltip("Dalgadan bağımsız, sürekli hafif salınım. DREDGE'in 'canlı' hissi buradan gelir.")]
        [Range(0f, 5f)] public float idleRollAmplitude = 1.2f;
        [Range(0f, 2f)] public float idleRollSpeed = 0.55f;

        float _velY;
        float _currentY;

        void OnEnable()
        {
            if (water == null) water = WaterSurface.Instance;
            _currentY = transform.position.y;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying && !simulateInEditMode) return;

            if (water == null)
            {
                water = WaterSurface.Instance;
                if (water == null) return;
            }

            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;

            float hC = water.GetHeight(pos);
            float hF = water.GetHeight(pos + fwd * (length * 0.5f));
            float hB = water.GetHeight(pos - fwd * (length * 0.5f));
            float hR = water.GetHeight(pos + right * (width * 0.5f));
            float hL = water.GetHeight(pos - right * (width * 0.5f));

            float target = (hC * 2f + hF + hB + hR + hL) / 6f + waterlineOffset;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            _currentY = Mathf.SmoothDamp(_currentY, target, ref _velY, 1f / heightSmooth, Mathf.Infinity, dt);
            transform.position = new Vector3(pos.x, _currentY, pos.z);

            // Eğim: burun-kıç ve sancak-iskele farkından
            float pitch = Mathf.Atan2(hF - hB, Mathf.Max(length, 0.01f)) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(hR - hL, Mathf.Max(width, 0.01f)) * Mathf.Rad2Deg;

            float time = Application.isPlaying ? Time.time : 0f;
            roll += Mathf.Sin(time * idleRollSpeed * 6.28318f * 0.25f) * idleRollAmplitude;

            pitch = Mathf.Clamp(-pitch * tiltExaggeration, -maxTilt, maxTilt);
            roll = Mathf.Clamp(-roll * tiltExaggeration, -maxTilt, maxTilt);

            Quaternion targetRot = Quaternion.Euler(pitch, transform.eulerAngles.y, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                  1f - Mathf.Exp(-rotationSmooth * dt));
        }
    }
}
