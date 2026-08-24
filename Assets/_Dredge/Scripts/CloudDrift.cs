using UnityEngine;

namespace Dredge
{
    /// <summary>Bulutları rüzgâr yönünde yavaşça sürükler; kameradan çok uzaklaşanı karşı tarafa alır.</summary>
    public class CloudDrift : MonoBehaviour
    {
        public Vector3 wind = new Vector3(0.9f, 0f, 0.35f);   // m/s
        public float wrapRadius = 700f;

        void Update()
        {
            transform.position += wind * Time.deltaTime;

            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > wrapRadius * wrapRadius)
            {
                // Karşı kenara taşı (aynı yükseklik), rüzgâr yönünün gerisine
                Vector3 back = -wind.normalized * wrapRadius * 0.95f;
                transform.position = new Vector3(cam.transform.position.x + back.x, transform.position.y, cam.transform.position.z + back.z)
                                   + Vector3.Cross(Vector3.up, wind.normalized) * Random.Range(-wrapRadius * 0.6f, wrapRadius * 0.6f);
            }
        }
    }
}
