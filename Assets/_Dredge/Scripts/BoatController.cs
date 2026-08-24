using UnityEngine;
using UnityEngine.InputSystem;

namespace Dredge
{
    /// <summary>
    /// Dredge tarzı sakin tekne kontrolü.
    ///
    /// Rigidbody + kaldırma kuvveti yerine kinematik yaklaşım: tekne dalga
    /// yüzeyine doğrudan oturtulur, eğimi gövdenin dört köşesindeki su
    /// yüksekliğinden çıkarılır. Fizik motoru patlamaz, davranış deterministiktir
    /// ve sakin bir denizde gerçek kaldırma kuvvetinden ayırt edilemez.
    /// </summary>
    public class BoatController : MonoBehaviour
    {
        [Header("Hareket")]
        [SerializeField] float maxSpeed = 7f;          // m/s  (~13 knot)
        [SerializeField] float reverseSpeed = 2.5f;
        [SerializeField] float acceleration = 2.2f;
        [SerializeField] float drag = 1.1f;
        [SerializeField] float turnRate = 32f;         // derece/sn, tam hızda
        [SerializeField] float turnAtRest = 0.25f;     // duruyorken dönüşün ne kadarı kalsın

        [Header("Gövde")]
        [Tooltip("Su hattının tekne pivotuna göre yüksekliği. Bu model için 1.42 m.")]
        [SerializeField] float waterlineOffset = 1.42f;
        [SerializeField] float hullLength = 6.1f;
        [SerializeField] float hullWidth = 3.0f;
        [SerializeField] float levelSmoothing = 3.5f;

        [Header("Çarpışma")]
        [SerializeField] float hullRadius = 2.0f;
        [SerializeField] float lookAhead = 1.6f;

        [Header("Salınım")]
        [SerializeField] float rollFromTurn = 6f;      // dönüşte yatma
        [SerializeField] float bobSmoothing = 6f;

        public float Speed { get; private set; }
        public float NormalizedSpeed => maxSpeed > 0f ? Mathf.Abs(Speed) / maxSpeed : 0f;

        OceanSurface ocean;
        float turnLean;

        void Awake() => ocean = OceanSurface.Instance;

        void Update()
        {
            if (ocean == null) ocean = OceanSurface.Instance;

            ReadInput(out float throttle, out float steer);
            Drive(throttle, steer);
            FloatOnWaves(steer);
        }

        void ReadInput(out float throttle, out float steer)
        {
            throttle = 0f; steer = 0f;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
        }

        void Drive(float throttle, float steer)
        {
            float target = throttle > 0f ? throttle * maxSpeed
                         : throttle < 0f ? throttle * reverseSpeed
                         : 0f;

            Speed = Mathf.Abs(throttle) > 0.01f
                ? Mathf.MoveTowards(Speed, target, acceleration * Time.deltaTime)
                : Mathf.MoveTowards(Speed, 0f, drag * Time.deltaTime);

            // Duruyorken dümen tutmaz; hız arttıkça dönüş açılır.
            float authority = Mathf.Lerp(turnAtRest, 1f, NormalizedSpeed);
            float yaw = steer * turnRate * authority * Mathf.Sign(Speed >= 0f ? 1f : -1f) * Time.deltaTime;

            // Kayalıklara girmeyelim: gövde küresini bir adım ileri taşıyıp bakıyoruz.
            // Denizin collider'ı yok, dolayısıyla burada yalnızca kara parçaları çıkar.
            Vector3 step = transform.forward * (Speed * Time.deltaTime);
            Vector3 probe = transform.position + step + transform.forward * (Mathf.Sign(Speed) * lookAhead);
            if (Physics.CheckSphere(probe, hullRadius, ~0, QueryTriggerInteraction.Ignore))
                Speed = Mathf.MoveTowards(Speed, 0f, 12f * Time.deltaTime);
            else
                transform.position += step;

            transform.Rotate(Vector3.up, yaw, Space.World);

            turnLean = Mathf.Lerp(turnLean, -steer * rollFromTurn * NormalizedSpeed, Time.deltaTime * 2.5f);
        }

        /// <summary>Gövdenin dört köşesindeki su yüksekliğinden konum ve eğim çıkarır.</summary>
        void FloatOnWaves(float steer)
        {
            if (ocean == null) return;

            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward, right = transform.right;
            float hl = hullLength * 0.5f, hw = hullWidth * 0.5f;

            float bow   = ocean.SampleHeight(pos + fwd * hl);
            float stern = ocean.SampleHeight(pos - fwd * hl);
            float port  = ocean.SampleHeight(pos - right * hw);
            float star  = ocean.SampleHeight(pos + right * hw);

            float centre = (bow + stern + port + star) * 0.25f;
            float targetY = centre - waterlineOffset;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-bobSmoothing * Time.deltaTime));
            transform.position = pos;

            float pitch = -Mathf.Atan2(bow - stern, hullLength) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(star - port, hullWidth) * Mathf.Rad2Deg + turnLean;

            var wanted = Quaternion.Euler(pitch, transform.eulerAngles.y, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted,
                                                  1f - Mathf.Exp(-levelSmoothing * Time.deltaTime));
        }
    }
}
