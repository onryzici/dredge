using UnityEngine;

namespace DredgeLook
{
    /// <summary>
    /// DREDGE'in kamera kadrajı. Senin ekran görüntündeki en sessiz sorun buydu:
    /// kamera su seviyesine çok yakındı, kadrajın büyük kısmı su oluyordu.
    ///
    /// DREDGE referansı: yükseklik ~9m, aşağı bakış ~28°, FOV ~45, mesafe ~11m.
    /// Bu değerlerde ufuk çizgisi ekranın üst üçte birine oturur ve diorama hissi doğar.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Dredge Look/Dredge Camera Rig")]
    [RequireComponent(typeof(Camera))]
    public class DredgeCameraRig : MonoBehaviour
    {
        [Header("Hedef")]
        public Transform target;

        [Tooltip("Editörde de kamerayı konumlandır. Kadraj ayarlarken aç, sonra kapat.")]
        public bool simulateInEditMode = false;

        [Header("Kadraj")]
        [Tooltip("Hedefin arkasındaki yatay mesafe (m).")]
        [Range(3f, 40f)] public float distance = 11f;
        [Tooltip("Hedefin üzerindeki yükseklik (m). 8-12 arası DREDGE'e yakın.")]
        [Range(1f, 30f)] public float height = 9f;
        [Tooltip("Aşağı bakış açısı. 25-35° arası doğru aralık.")]
        [Range(0f, 70f)] public float pitch = 28f;
        [Tooltip("FOV. 60+ perspektifi bozar ve dioramayı öldürür.")]
        [Range(20f, 70f)] public float fieldOfView = 45f;

        [Header("Yumuşatma")]
        [Range(0.5f, 20f)] public float positionSmooth = 4f;
        [Range(0.5f, 20f)] public float rotationSmooth = 5f;
        [Tooltip("Kamera teknenin sallanmasını takip etmesin — sadece yatayda takip etsin.")]
        public bool ignoreTargetBobbing = true;

        [Header("Sinematik")]
        [Tooltip("Uzak mesafeye odaklanan görüntü için hafif kamera salınımı.")]
        [Range(0f, 2f)] public float swayAmount = 0.25f;
        [Range(0f, 2f)] public float swaySpeed = 0.4f;

        Camera _cam;
        float _smoothedTargetY;

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            if (target != null) _smoothedTargetY = target.position.y;
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam != null) _cam.fieldOfView = fieldOfView;
            if (target == null) return;
            if (!Application.isPlaying && !simulateInEditMode) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            float ty = target.position.y;
            if (ignoreTargetBobbing)
            {
                _smoothedTargetY = Mathf.Lerp(_smoothedTargetY, ty, 1f - Mathf.Exp(-1.2f * dt));
                ty = _smoothedTargetY;
            }

            Vector3 flatForward = target.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 1e-6f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 desired = new Vector3(target.position.x, ty, target.position.z)
                              - flatForward * distance
                              + Vector3.up * height;

            if (swayAmount > 0f && Application.isPlaying)
            {
                float t = Time.time * swaySpeed;
                desired += new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 0.9f) * 0.6f, 0f) * swayAmount;
            }

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSmooth * dt));

            Quaternion desiredRot = Quaternion.Euler(pitch, Quaternion.LookRotation(flatForward).eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationSmooth * dt));
        }
    }
}
