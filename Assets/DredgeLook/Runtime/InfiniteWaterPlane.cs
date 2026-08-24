using UnityEngine;

namespace DredgeLook
{
    /// <summary>
    /// Kameranın altında kalan, bölünmüş bir su düzlemi üretir ve kamerayı takip eder.
    /// ÖNEMLİ: Gerstner dalgaları VERTEX'te çalışır — düşük çözünürlüklü bir düzlemde
    /// dalga oluşmaz. Varsayılan 160x160 segment, 900m boyut iyi bir denge.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Dredge Look/Infinite Water Plane")]
    public class InfiniteWaterPlane : MonoBehaviour
    {
        [Header("Mesh")]
        [Tooltip("Düzlemin kenar uzunluğu (metre).")]
        [Range(50f, 4000f)] public float size = 900f;
        [Tooltip("Kenar başına segment sayısı. Dalga kalitesini bu belirler.")]
        [Range(16, 400)] public int resolution = 160;
        [Tooltip("Merkeze doğru yoğunlaşan grid — uzakta boşuna vertex harcanmaz.")]
        public bool centerDense = true;

        [Header("Takip")]
        public bool followCamera = true;
        [Tooltip("Takip ederken hücre boyutuna yuvarla — dalgaların kaymasını önler.")]
        public bool snapToGrid = true;
        public Camera targetCamera;

        Mesh _mesh;
        int _builtRes = -1;
        float _builtSize = -1f;
        bool _builtDense;

        void OnEnable() { Rebuild(); }
        void OnValidate() { _builtRes = -1; }

        void Update()
        {
            if (_mesh == null || _builtRes != resolution || !Mathf.Approximately(_builtSize, size) || _builtDense != centerDense)
                Rebuild();

            if (!followCamera) return;

            var cam = targetCamera != null ? targetCamera : Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (cam == null) return;

            Vector3 p = cam.transform.position;
            float cell = size / Mathf.Max(resolution, 1);
            float x = snapToGrid ? Mathf.Floor(p.x / cell) * cell : p.x;
            float z = snapToGrid ? Mathf.Floor(p.z / cell) * cell : p.z;
            transform.position = new Vector3(x, transform.position.y, z);
        }

        public void Rebuild()
        {
            int res = Mathf.Max(2, resolution);

            // Eski mesh'i temizle (editörde sürekli rebuild bellek sızdırmasın)
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }

            _mesh = new Mesh { name = "DredgeWaterPlane" };
            _mesh.indexFormat = (res + 1) * (res + 1) > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            int vCount = (res + 1) * (res + 1);
            var verts = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var norms = new Vector3[vCount];

            for (int z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++)
                {
                    int i = z * (res + 1) + x;
                    float fx = (float)x / res * 2f - 1f;   // -1..1
                    float fz = (float)z / res * 2f - 1f;

                    if (centerDense)
                    {
                        // Merkeze doğru yoğunlaştır (kübik dağılım)
                        fx = fx * fx * fx * 0.65f + fx * 0.35f;
                        fz = fz * fz * fz * 0.65f + fz * 0.35f;
                    }

                    verts[i] = new Vector3(fx * size * 0.5f, 0f, fz * size * 0.5f);
                    uvs[i] = new Vector2((float)x / res, (float)z / res);
                    norms[i] = Vector3.up;
                }
            }

            var tris = new int[res * res * 6];
            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = z * (res + 1) + x;
                    tris[t++] = i;
                    tris[t++] = i + res + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + res + 1;
                    tris[t++] = i + res + 2;
                }
            }

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.normals = norms;
            _mesh.triangles = tris;
            // Dalgalar vertex'te oluştuğu için culling bounds'ı elle büyütmek şart:
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 40f, size));

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _builtRes = resolution;
            _builtSize = size;
            _builtDense = centerDense;
        }
    }
}
