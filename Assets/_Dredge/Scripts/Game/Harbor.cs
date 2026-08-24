using UnityEngine;

namespace Dredge.Game
{
    /// <summary>Liman/iskele: menzilde E → ambardaki balıkları sat, gövdeyi onar, akıl dinlen.</summary>
    public class Harbor : MonoBehaviour
    {
        public float interactRadius = 11f;
        public Transform respawnPoint;
        [Tooltip("Onarım ücreti (gövde puanı başına).")]
        public int repairCostPerPoint = 1;

        public bool InRange(Vector3 pos) => Vector3.Distance(pos, transform.position) < interactRadius;

        public void SellAll(GameSession s)
        {
            int total = 0; int aberr = 0;
            foreach (var it in s.inventory.items) { total += it.value; if (it.aberration) aberr++; }
            if (total == 0) { s.Notify("Satacak balık yok."); return; }
            s.money += total;
            s.inventory.Clear();
            s.Notify($"Satıldı: +{total}₺" + (aberr > 0 ? $"  (tüccar {aberr} tuhaf balığa dikkatle baktı)" : ""));
        }

        public void Repair(GameSession s)
        {
            if (s.damage == null) return;
            int missing = Mathf.RoundToInt(s.damage.maxHull - s.damage.hull);
            if (missing <= 0) { s.Notify("Gövde sağlam."); return; }
            int cost = missing * repairCostPerPoint;
            if (s.money < cost) { s.Notify($"Onarım {cost}₺ — paran yetmiyor."); return; }
            s.money -= cost;
            s.damage.hull = s.damage.maxHull;
            s.Notify($"Gövde onarıldı (−{cost}₺).");
        }

        public void Rest(GameSession s)
        {
            // Sabaha kadar uyu: saat 07:00
            var c = s.clock;
            float target = 7f;
            float delta = target - c.hour; if (delta <= 0f) delta += 24f;
            c.Advance(delta);
            if (s.panic != null) s.panic.Reset();
            s.Notify($"Dinlendin. Gün {c.day}, sabah.");
        }
    }
}
