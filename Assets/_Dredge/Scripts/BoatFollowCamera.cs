using UnityEngine;
using UnityEngine.InputSystem;

namespace Dredge
{
    /// <summary>
    /// Dredge'in kamera davranışı: kamera tekneyle birlikte DÖNMEZ. Yatay açı
    /// dünya uzayında sabit kalır ve yalnızca fareyle değişir; tekne kameranın
    /// altında döner. Oyuncu bir yöne bakarken başka bir yöne seyredebilir —
    /// oyunun okyanusta yön bulma hissi buradan geliyor.
    ///
    /// (Kaynak: Black Salt Games'in v1.2.0 notlarında "tekne dönünce kamera
    /// otomatik olarak yeni yöne bakmaz" diye geçiyor.)
    /// </summary>
    public class BoatFollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Yerleşim")]
        [SerializeField] float distance = 28f;
        [SerializeField] float minDistance = 14f;
        [SerializeField] float maxDistance = 48f;
        [SerializeField] float pivotHeight = 2.5f;
        [SerializeField] float followSmoothing = 4f;

        [Header("Bakış")]
        [SerializeField] float lookSensitivity = 0.13f;
        [SerializeField] float pitch = 26f;
        [SerializeField] float pitchMin = 8f;
        [SerializeField] float pitchMax = 62f;
        [SerializeField] float zoomStep = 2.5f;

        [Header("Yumuşatma")]
        [Tooltip("Hızlanırken kamera çok yavaşça teknenin arkasına kayar. 0 = Dredge gibi hiç kaymaz.")]
        [SerializeField] float autoAlign = 0f;

        float yaw;
        Vector3 pivot;

        void Start()
        {
            if (target == null)
            {
                var boat = FindAnyObjectByType<BoatController>();
                if (boat != null) target = boat.transform;
            }
            yaw = target != null ? target.eulerAngles.y : 0f;
            pivot = PivotPoint;
            SetCursor(true);
        }

        Vector3 PivotPoint => target != null ? target.position + Vector3.up * pivotHeight : transform.position;

        void LateUpdate()
        {
            if (target == null) return;

            HandleCursor();
            if (Cursor.lockState == CursorLockMode.Locked) HandleLook();

            if (autoAlign > 0f)
            {
                float wanted = target.eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, wanted, autoAlign * Time.deltaTime);
            }

            pivot = Vector3.Lerp(pivot, PivotPoint, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 wantedPos = pivot - rot * Vector3.forward * distance;

            // Kayalığın içine girmesin.
            if (Physics.SphereCast(pivot, 0.8f, (wantedPos - pivot).normalized,
                                   out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
                wantedPos = pivot + (wantedPos - pivot).normalized * Mathf.Max(hit.distance - 0.5f, 3f);

            transform.SetPositionAndRotation(wantedPos, rot);
        }

        void HandleLook()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, pitchMin, pitchMax);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        void HandleCursor()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursor(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursor(true);
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
