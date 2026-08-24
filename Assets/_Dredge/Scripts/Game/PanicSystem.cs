using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Akıl/panik: gece açıkta kaldıkça artar, gündüz ve limanda azalır. Yükseldikçe
    /// sis koyulaşır, renk çekilir (GameClock uygular); tam panikte "hayalet kayalar"
    /// belirir — çarpışmalar daha sık olur.
    /// </summary>
    public class PanicSystem : MonoBehaviour
    {
        [Range(0f, 1f)] public float Panic01;
        public float riseAtNight = 1f / 150f;      // saniyede (gece açıkta): 2.5 dk'da tam
        public float fallAtDay = 1f / 40f;
        public float fallAtHarbor = 1f / 8f;
        public float aberrationHit = 0.18f;

        [Header("Hayalet kayalar")]
        public int ghostRocks = 5;
        public float ghostDistance = 45f;
        GameObject[] ghosts;
        float ghostTimer;

        void Update()
        {
            var s = GameSession.Instance;
            if (s == null || s.clock == null) return;

            bool atHarbor = s.homeHarbor != null && s.boat != null && s.homeHarbor.InRange(s.boat.transform.position);
            float night = s.clock.Night01;
            if (atHarbor) Panic01 -= fallAtHarbor * Time.deltaTime;
            else if (night > 0.55f) Panic01 += riseAtNight * night * (s.upgrades != null && s.upgrades.lantern ? 0.5f : 1f) * Time.deltaTime;
            if (s.boat != null) s.boat.SpeedMultiplier = s.upgrades != null && s.upgrades.engine ? 1.35f : 1f;
            else Panic01 -= fallAtDay * Time.deltaTime;
            Panic01 = Mathf.Clamp01(Panic01);

            UpdateGhosts(s);
        }

        public void OnAberration() { Panic01 = Mathf.Clamp01(Panic01 + aberrationHit); }
        public void Reset() { Panic01 = 0f; }

        void UpdateGhosts(GameSession s)
        {
            bool show = Panic01 > 0.6f && s.boat != null;
            if (ghosts == null)
            {
                ghosts = new GameObject[ghostRocks];
                for (int i = 0; i < ghostRocks; i++)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    g.name = "Hayalet Kaya";
                    g.transform.SetParent(transform, false);
                    g.transform.localScale = new Vector3(4f, 2.2f, 3.2f);
                    var mr = g.GetComponent<MeshRenderer>();
                    var lit = Shader.Find("Dredge/StylizedLit");
                    if (lit != null) { mr.sharedMaterial = new Material(lit); mr.sharedMaterial.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.15f)); }
                    g.SetActive(false);
                    ghosts[i] = g;
                }
            }

            ghostTimer -= Time.deltaTime;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (!show) { ghosts[i].SetActive(false); continue; }
                if (!ghosts[i].activeSelf || ghostTimer <= 0f)
                {
                    // Teknenin önüne, rastgele açıyla yerleştir
                    var b = s.boat.transform;
                    float ang = Random.Range(-50f, 50f);
                    var dir = Quaternion.Euler(0f, ang, 0f) * b.forward;
                    var p = b.position + dir * Random.Range(ghostDistance * 0.6f, ghostDistance);
                    p.y = -0.6f;
                    ghosts[i].transform.position = p;
                    ghosts[i].transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                    ghosts[i].SetActive(true);
                }
            }
            if (ghostTimer <= 0f) ghostTimer = 25f;
        }
    }
}
