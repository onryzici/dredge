using UnityEngine;

namespace Dredge.Game
{
    /// <summary>Bir ada üstünde daire çizen martı sürüsü: her kuş kanat çırpan iki üçgen (billboard değil, gerçek mesh), gündüz görünür.</summary>
    public class Seagulls : MonoBehaviour
    {
        public int count = 6;
        public float radius = 26f;
        public float height = 18f;
        public float speed = 0.35f;

        Transform[] birds;
        Mesh[] frames;
        MeshFilter[] filters;
        float[] phase;

        void Start()
        {
            birds = new Transform[count]; filters = new MeshFilter[count]; phase = new float[count];
            frames = new[] { Bird(0.55f), Bird(0.15f), Bird(-0.35f), Bird(0.15f) };
            var mat = new Material(Shader.Find("Dredge/StylizedLit"));
            mat.SetColor("_BaseColor", new Color(0.62f, 0.64f, 0.68f));   // 0.95 bloom halesi yapıyordu
            mat.SetFloat("_RimStrength", 0f);
            mat.SetFloat("_Bands", 2f);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Marti");
                go.transform.SetParent(transform, false);
                filters[i] = go.AddComponent<MeshFilter>();
                filters[i].sharedMesh = frames[0];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                birds[i] = go.transform;
                phase[i] = i / (float)count * Mathf.PI * 2f;
            }
        }

        void Update()
        {
            var s = GameSession.Instance;
            float night = s != null && s.clock != null ? s.clock.Night01 : 0f;
            bool show = night < 0.6f;
            for (int i = 0; i < count; i++)
            {
                birds[i].gameObject.SetActive(show);
                if (!show) continue;
                float t = Time.time * speed + phase[i];
                float r = radius * (0.7f + 0.3f * Mathf.Sin(t * 0.7f + i));
                var p = new Vector3(Mathf.Cos(t) * r, height + Mathf.Sin(t * 1.3f + i) * 3f, Mathf.Sin(t) * r);
                var next = new Vector3(Mathf.Cos(t + 0.05f) * r, p.y, Mathf.Sin(t + 0.05f) * r);
                birds[i].localPosition = p;
                birds[i].rotation = Quaternion.LookRotation(transform.TransformDirection(next - p), Vector3.up);
                int f = Mathf.FloorToInt((Time.time * 6f + i * 1.7f) % frames.Length);
                filters[i].sharedMesh = frames[f];
            }
        }

        static Mesh Bird(float wingY)
        {
            // Gövde noktası + iki kanat üçgeni (~1.2 m açıklık)
            var m = new Mesh { name = "Gull" };
            var v = new[]
            {
                new Vector3(0, 0, 0.35f), new Vector3(-0.65f, wingY, -0.15f), new Vector3(0, 0, -0.25f),
                new Vector3(0, 0, 0.35f), new Vector3(0, 0, -0.25f), new Vector3(0.65f, wingY, -0.15f),
            };
            m.vertices = v;
            m.triangles = new[] { 0, 1, 2, 3, 4, 5, 2, 1, 0, 5, 4, 3 };   // iki taraf
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
