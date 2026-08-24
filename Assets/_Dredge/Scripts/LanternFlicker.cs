using UnityEngine;

namespace Dredge
{
    /// <summary>Fener ve kamara ışıkları için yumuşak titreşim.</summary>
    [RequireComponent(typeof(Light))]
    public class LanternFlicker : MonoBehaviour
    {
        [SerializeField] float minMultiplier = 0.85f;
        [SerializeField] float maxMultiplier = 1.1f;
        [SerializeField] float speed = 2.4f;

        Light target;
        float baseIntensity;
        float seed;

        void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            target.intensity = baseIntensity * Mathf.Lerp(minMultiplier, maxMultiplier, n);
        }
    }
}
