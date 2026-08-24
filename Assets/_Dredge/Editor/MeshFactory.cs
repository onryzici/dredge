using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dredge.EditorTools
{
    /// <summary>
    /// Sahnenin geometrisini üretir. Paketten hazır ada/ağaç gelmediği için
    /// hepsi burada, düz gölgelemeli (flat shaded) low-poly olarak oluşturuluyor —
    /// referanstaki kırıklı kaya yüzeyleri tam olarak bu yüzden öyle görünüyor.
    /// </summary>
    public static class MeshFactory
    {
        // ------------------------------------------------------------- yardımcılar

        /// <summary>Üçgen listesinden düz gölgelemeli mesh kurar (her üçgen kendi normali).</summary>
        static void FlatShade(List<Vector3> tris, List<Vector3> verts, List<Vector3> normals, List<int> indices)
        {
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) continue;          // dejenere üçgeni at
                n.Normalize();

                int baseIndex = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                indices.Add(baseIndex); indices.Add(baseIndex + 1); indices.Add(baseIndex + 2);
            }
        }

        static Mesh Finish(string name, List<Vector3> verts, List<Vector3> normals, params List<int>[] submeshes)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Noise(float phase, float x, float z) => Mathf.PerlinNoise(phase + x + 64f, phase + z + 64f) * 2f - 1f;

        // ----------------------------------------------------------------- okyanus

        /// <summary>Dalga vertex shader'ının üstünde çalışacağı düz ızgara.</summary>
        public static Mesh OceanGrid(float size, int cells)
        {
            var verts = new List<Vector3>((cells + 1) * (cells + 1));
            var normals = new List<Vector3>(verts.Capacity);
            var tris = new List<int>(cells * cells * 6);

            float step = size / cells, half = size * 0.5f;
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                verts.Add(new Vector3(x * step - half, 0f, z * step - half));
                normals.Add(Vector3.up);
            }

            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int i = z * (cells + 1) + x;
                tris.Add(i); tris.Add(i + cells + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + cells + 1); tris.Add(i + cells + 2);
            }

            return Finish("Ocean Grid", verts, normals, tris);
        }

        // -------------------------------------------------------------------- ada

        /// <summary>
        /// Kutupsal ızgaradan kayalık ada. Kıyı çizgisi açısal gürültüyle bozuluyor,
        /// yükseklik profili merkezde tepe / kenarda su altına inecek şekilde kuruluyor.
        /// </summary>
        public static Mesh Island(int seed, float radius, float height, int rings = 18, int segments = 28)
        {
            var rnd = new System.Random(seed);
            float coastPhase = (float)rnd.NextDouble() * 90f;
            float bumpPhase = (float)rnd.NextDouble() * 90f;
            float lean = (float)rnd.NextDouble() * Mathf.PI * 2f;

            var ring = new Vector3[rings + 1][];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                ring[i] = new Vector3[segments];
                for (int s = 0; s < segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    float nx = Mathf.Cos(ang), nz = Mathf.Sin(ang);

                    // kıyı çizgisi: yumuşak açısal dalgalanma + hafif asimetri
                    float coast = 1f
                        + 0.34f * Noise(coastPhase, nx * 1.7f, nz * 1.7f)
                        + 0.14f * Mathf.Sin(ang * 3f + lean);
                    float r = radius * t * Mathf.Max(coast, 0.35f);

                    // profil: merkezde tepe, t≈0.75'te su hattı, kenarda su altı etek
                    float shape = Mathf.Pow(1f - t, 2.1f) - Mathf.Pow(t, 6f) * 0.5f;
                    float bump = Noise(bumpPhase, nx * 2.4f * t, nz * 2.4f * t);
                    float y = height * (shape + bump * 0.17f * (1f - t));

                    // Kayalıkların katmanlı görünümü: yüksekliği basamaklara doğru
                    // çekiyoruz. Tam yuvarlama fazla düzenli duruyor, %70 karışım
                    // hem sahanlık hem doğal kırık veriyor.
                    float ledge = Mathf.Max(height * 0.11f, 1.2f);
                    float stepped = Mathf.Round(y / ledge) * ledge;
                    y = Mathf.Lerp(y, stepped, 0.70f);

                    ring[i][s] = new Vector3(nx * r, y, nz * r);
                }
            }

            var apex = new Vector3(0f, height * (1f + 0.05f * Noise(bumpPhase, 3f, 7f)), 0f);
            var tris = new List<Vector3>();

            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                tris.Add(apex); tris.Add(ring[1][n]); tris.Add(ring[1][s]);
            }

            for (int i = 1; i < rings; i++)
            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                tris.Add(ring[i][s]);     tris.Add(ring[i][n]);     tris.Add(ring[i + 1][s]);
                tris.Add(ring[i][n]);     tris.Add(ring[i + 1][n]); tris.Add(ring[i + 1][s]);
            }

            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var idx = new List<int>();
            FlatShade(tris, verts, normals, idx);
            return Finish($"Island_{seed}", verts, normals, idx);
        }

        // -------------------------------------------------------------------- çam

        /// <summary>Gövde + üst üste üç koni. Alt-mesh 0 gövde, 1 yaprak.</summary>
        public static Mesh Pine(int seed)
        {
            var rnd = new System.Random(seed);
            float scale = 0.8f + (float)rnd.NextDouble() * 0.6f;
            float trunkH = 1.5f * scale, trunkR = 0.16f * scale;

            var trunkTris = new List<Vector3>();
            Prism(trunkTris, 6, trunkR, 0f, trunkH);

            var leafTris = new List<Vector3>();
            float y = trunkH * 0.62f;
            float r = 1.55f * scale;
            for (int i = 0; i < 3; i++)
            {
                Cone(leafTris, 8, r, y, 2.35f * scale);
                y += 1.35f * scale;
                r *= 0.72f;
            }

            var verts = new List<Vector3>(); var normals = new List<Vector3>();
            var trunkIdx = new List<int>(); var leafIdx = new List<int>();
            FlatShade(trunkTris, verts, normals, trunkIdx);
            FlatShade(leafTris, verts, normals, leafIdx);
            return Finish($"Pine_{seed}", verts, normals, trunkIdx, leafIdx);
        }

        static void Prism(List<Vector3> tris, int sides, float radius, float y0, float y1)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, y0, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, y0, Mathf.Sin(a1) * radius);
                Vector3 p2 = p1 + Vector3.up * (y1 - y0);
                Vector3 p3 = p0 + Vector3.up * (y1 - y0);
                tris.Add(p0); tris.Add(p1); tris.Add(p2);
                tris.Add(p0); tris.Add(p2); tris.Add(p3);
            }
        }

        static void Cone(List<Vector3> tris, int sides, float radius, float baseY, float height)
        {
            var apex = new Vector3(0f, baseY + height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, baseY, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, baseY, Mathf.Sin(a1) * radius);
                tris.Add(apex); tris.Add(p0); tris.Add(p1);
                tris.Add(p0); tris.Add(new Vector3(0f, baseY, 0f)); tris.Add(p1);   // taban
            }
        }

        // ------------------------------------------------------------------- kaya

        /// <summary>Küçük, kırıklı kaya bloğu — sığlıkları işaretlemek için.</summary>
        /// <summary>
        /// Düz tabanlı, yatay uzatılmış, sert kenarlı stilize bulut (DREDGE'deki gibi
        /// dünyada duran mesh). 4–8 basık blob üst üste; her üçgen kendi normali.
        /// </summary>
        public static Mesh Cloud(int seed, float scale = 1f)
        {
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var tris = new List<Vector3>();
            int blobs = rnd.Next(4, 9);
            float spread = 14f * scale;
            for (int b = 0; b < blobs; b++)
            {
                float rad = R(6f, 13f) * scale;
                var center = new Vector3(R(-spread, spread), R(0f, 2f) * scale, R(-spread * 0.35f, spread * 0.35f));
                float sx = R(1.4f, 2.4f), sy = R(0.28f, 0.40f), sz = R(0.9f, 1.3f);
                float phase = R(0f, 90f);
                const int lat = 4, lon = 9;

                var grid = new Vector3[lat + 1][];
                for (int i = 0; i <= lat; i++)
                {
                    float theta = i / (float)lat * Mathf.PI;
                    grid[i] = new Vector3[lon];
                    for (int j = 0; j < lon; j++)
                    {
                        float phi = j / (float)lon * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                        float r = rad * (0.85f + 0.25f * Noise(phase, dir.x * 1.5f, dir.z * 1.5f));
                        var p = new Vector3(dir.x * r * sx, dir.y * r * sy, dir.z * r * sz);
                        p.y = Mathf.Max(p.y, -rad * sy * 0.35f);      // düz taban
                        grid[i][j] = center + p;
                    }
                }

                for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int n = (j + 1) % lon;
                    tris.Add(grid[i][j]);     tris.Add(grid[i][n]);     tris.Add(grid[i + 1][j]);
                    tris.Add(grid[i][n]);     tris.Add(grid[i + 1][n]); tris.Add(grid[i + 1][j]);
                }
            }

            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var idx = new List<int>();
            FlatShade(tris, verts, normals, idx);
            return Finish($"Cloud_{seed}", verts, normals, idx);
        }

        public static Mesh Rock(int seed, float size)
        {
            var rnd = new System.Random(seed);
            float phase = (float)rnd.NextDouble() * 90f;
            const int lat = 4, lon = 7;

            var grid = new Vector3[lat + 1][];
            for (int i = 0; i <= lat; i++)
            {
                float v = i / (float)lat;
                float theta = v * Mathf.PI;
                grid[i] = new Vector3[lon];
                for (int j = 0; j < lon; j++)
                {
                    float phi = j / (float)lon * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    float r = size * (0.75f + 0.35f * Noise(phase, dir.x * 2f, dir.z * 2f));
                    var p = dir * r;
                    p.y = Mathf.Max(p.y * 0.75f, -size * 0.15f);      // tabanı bastır
                    grid[i][j] = p;
                }
            }

            var tris = new List<Vector3>();
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int n = (j + 1) % lon;
                tris.Add(grid[i][j]);     tris.Add(grid[i][n]);     tris.Add(grid[i + 1][j]);
                tris.Add(grid[i][n]);     tris.Add(grid[i + 1][n]); tris.Add(grid[i + 1][j]);
            }

            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var idx = new List<int>();
            FlatShade(tris, verts, normals, idx);
            return Finish($"Rock_{seed}", verts, normals, idx);
        }
    }
}
