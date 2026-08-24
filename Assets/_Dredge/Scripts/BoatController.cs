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
        [SerializeField] float acceleration = 3.6f;    // taban ivme; kalkışta ×1.8 (bkz. Drive)
        [SerializeField] float drag = 1.6f;
        [SerializeField] float turnRate = 34f;         // derece/sn, tam hızda
        [SerializeField] float turnAtRest = 0.4f;      // duruyorken dönüşün ne kadarı kalsın

        [Header("Gövde")]
        [Tooltip("Su hattının tekne pivotuna göre yüksekliği. Bu model için 1.42 m.")]
        [SerializeField] float waterlineOffset = 1.42f;
        [SerializeField] float hullLength = 6.1f;
        [SerializeField] float hullWidth = 3.0f;
        [SerializeField] float levelSmoothing = 3.5f;

        [Header("Çarpışma")]
        [SerializeField] float hullRadius = 1.6f;
        [SerializeField] float lookAhead = 1.4f;

        [Header("Salınım")]
        [SerializeField] float rollFromTurn = 6f;      // dönüşte yatma
        [SerializeField] float bobSmoothing = 6f;

        public float Speed { get; private set; }

        /// <summary>Kayaya çarpınca (hız m/s). BoatDamage dinler.</summary>
        public event System.Action<float> Collided;

        /// <summary>Mini-oyun / diyalog sırasında sürüş kilitlenir.</summary>
        public bool InputLocked { get; set; }

        /// <summary>Motor yükseltmesi vb. (1 = normal).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float NormalizedSpeed => maxSpeed > 0f ? Mathf.Abs(Speed) / maxSpeed : 0f;

        OceanSurface ocean;
        DredgeLook.WaterSurface water;   // Dredge Look suyu varsa öncelik onda
        float turnLean;

        void Awake() { ocean = OceanSurface.Instance; water = DredgeLook.WaterSurface.Instance; }

        bool HasWater => water != null || ocean != null;

        float WaterHeight(Vector3 p) => water != null ? water.GetHeight(p) : ocean.SampleHeight(p);

        void Update()
        {
            if (water == null) water = DredgeLook.WaterSurface.Instance;
            if (ocean == null) ocean = OceanSurface.Instance;

            ReadInput(out float throttle, out float steer);
            Drive(throttle, steer);
            FloatOnWaves(steer);
        }

        void ReadInput(out float throttle, out float steer)
        {
            throttle = 0f; steer = 0f;
            var kb = Keyboard.current;
            if (kb == null || InputLocked) return;

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
        }

        void Drive(float throttle, float steer)
        {
            float max = maxSpeed * SpeedMultiplier;
            float target = throttle > 0f ? throttle * max
                         : throttle < 0f ? throttle * reverseSpeed
                         : 0f;

            // Kalkışta pervane hemen tutar, hız yaklaştıkça ivme azalır (0'da ×1.8, tam hızda ×0.6).
            float accel = acceleration * Mathf.Lerp(1.8f, 0.6f, NormalizedSpeed);
            Speed = Mathf.Abs(throttle) > 0.01f
                ? Mathf.MoveTowards(Speed, target, accel * Time.deltaTime)
                : Mathf.MoveTowards(Speed, 0f, drag * Time.deltaTime);

            // Duruyorken dümen tutmaz; hız arttıkça dönüş açılır.
            float authority = Mathf.Lerp(turnAtRest, 1f, NormalizedSpeed);
            float yaw = steer * turnRate * authority * Mathf.Sign(Speed >= 0f ? 1f : -1f) * Time.deltaTime;

            // Kayalıklara girmeyelim: gövde küresini bir adım ileri taşıyıp bakıyoruz.
            // Denizin collider'ı yok, dolayısıyla burada yalnızca kara parçaları çıkar.
            Vector3 step = transform.forward * (Speed * Time.deltaTime);
            Vector3 probe = transform.position + step + transform.forward * (Mathf.Sign(Speed) * lookAhead);
            if (Physics.CheckSphere(probe, hullRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(Speed) > 2.5f) Collided?.Invoke(Mathf.Abs(Speed));   // kayaya çarptı (yavaş sürtünme hasar vermez)
                Speed = Mathf.MoveTowards(Speed, 0f, 12f * Time.deltaTime);
            }
            else
                transform.position += step;

            transform.Rotate(Vector3.up, yaw, Space.World);

            turnLean = Mathf.Lerp(turnLean, -steer * rollFromTurn * NormalizedSpeed, Time.deltaTime * 2.5f);
        }

        /// <summary>Gövdenin dört köşesindeki su yüksekliğinden konum ve eğim çıkarır.</summary>
        void FloatOnWaves(float steer)
        {
            if (!HasWater) return;

            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward, right = transform.right;
            float hl = hullLength * 0.5f, hw = hullWidth * 0.5f;

            float bow   = WaterHeight(pos + fwd * hl);
            float stern = WaterHeight(pos - fwd * hl);
            float port  = WaterHeight(pos - right * hw);
            float star  = WaterHeight(pos + right * hw);

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
