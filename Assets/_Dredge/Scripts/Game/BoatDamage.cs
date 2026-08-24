using UnityEngine;

namespace Dredge.Game
{
    /// <summary>Gövde sağlığı. Kayaya çarpınca hıza göre hasar; 0 olunca limana çekilir, balıkların yarısı gider.</summary>
    [RequireComponent(typeof(BoatController))]
    public class BoatDamage : MonoBehaviour
    {
        public float maxHull = 100f;
        public float hull = 100f;
        public float damagePerSpeed = 6f;      // 7 m/s → ~42
        public float hitCooldown = 1.2f;

        float lastHit = -10f;
        BoatController boat;

        void Awake()
        {
            boat = GetComponent<BoatController>();
            boat.Collided += OnCollided;
        }

        void OnDestroy() { if (boat != null) boat.Collided -= OnCollided; }

        void OnCollided(float speed)
        {
            if (Time.time - lastHit < hitCooldown) return;
            lastHit = Time.time;
            float dmg = speed * damagePerSpeed;
            hull = Mathf.Max(0f, hull - dmg);
            var s = GameSession.Instance;
            if (s == null) return;
            s.Notify($"Kayaya çarptın! Gövde −{dmg:0}");
            if (hull <= 0f) Sink(s);
        }

        void Sink(GameSession s)
        {
            // Balıkların yarısı kaybolur, tekne limana çekilir, gövde 40.
            int keep = s.inventory.items.Count / 2;
            var kept = s.inventory.items.GetRange(0, keep);
            s.inventory.Clear();
            foreach (var f in kept) { f.gridX = -1; f.gridY = -1; s.inventory.TryAdd(f); }
            hull = 40f;
            if (s.homeHarbor != null && s.homeHarbor.respawnPoint != null)
            {
                var rp = s.homeHarbor.respawnPoint;
                transform.position = new Vector3(rp.position.x, transform.position.y, rp.position.z);
                transform.rotation = Quaternion.Euler(0f, rp.eulerAngles.y, 0f);
            }
            s.clock.Advance(6f);
            s.Notify("Tekne battı... Sahil güvenlik seni limana çekti. Yükün yarısı denizde kaldı.", 6f);
        }
    }
}
