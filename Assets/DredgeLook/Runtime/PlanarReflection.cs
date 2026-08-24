using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DredgeLook
{
    /// <summary>
    /// Düzlemsel (ayna) yansıma: ana kamerayı su düzlemine göre aynalayıp sahneyi bir
    /// dokuya çizer; su shader'ı bunu ekran UV'siyle örnekler. DREDGE'in suyunda
    /// adalar, tekne ve fener gerçekten yansır — gökyüzü gradyanı tek başına bunu vermez.
    ///
    /// Su objesine ekle. Su objesinin layer'ı yansımadan hariç tutulur (kendini yansıtmasın).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Dredge Look/Planar Reflection")]
    public class PlanarReflection : MonoBehaviour
    {
        [Tooltip("Yansıma dokusunun ekran çözünürlüğüne oranı. 0.5 = yarı çözünürlük (stilize için yeterli).")]
        [Range(0.1f, 1f)] public float textureScale = 0.5f;
        [Tooltip("Yansıma düzleminin dünya yüksekliği (deniz seviyesi).")]
        public float planeY = 0f;
        [Tooltip("Kırpma düzlemini biraz aşağı alır; kıyıdaki geometri diplerde kesilmesin.")]
        public float clipPlaneOffset = 0.07f;
        [Tooltip("Yansımada çizilecek katmanlar. Su katmanı otomatik çıkarılır.")]
        public LayerMask reflectLayers = ~0;
        [Tooltip("Yansımada gölge çizilsin mi (maliyetli, stilize için gereksiz).")]
        public bool renderShadows = false;
        [Tooltip("Scene View'da da çalışsın.")]
        public bool renderInSceneView = true;

        static readonly int TexId = Shader.PropertyToID("_DL_PlanarReflection");
        static readonly int OnId = Shader.PropertyToID("_DL_PlanarReflectionOn");
        static bool rendering;

        Camera reflCam;
        RenderTexture rt;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(OnId, 0f);
            if (rt != null) { rt.Release(); DestroyImmediate(rt); rt = null; }
            if (reflCam != null) { DestroyImmediate(reflCam.gameObject); reflCam = null; }
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (rendering) return;
            if (cam.cameraType == CameraType.Reflection || cam.cameraType == CameraType.Preview) return;
            if (cam.cameraType == CameraType.SceneView && !renderInSceneView) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            EnsureCamera(cam);
            EnsureTexture(cam);

            // Ayna dönüşümü: y = planeY düzlemine göre.
            Vector3 pos = new Vector3(0f, planeY, 0f);
            Vector3 normal = Vector3.up;
            float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, d);
            Matrix4x4 reflection = ReflectionMatrix(plane);

            reflCam.CopyFrom(cam);
            reflCam.cameraType = CameraType.Reflection;
            reflCam.cullingMask = reflectLayers & ~(1 << gameObject.layer);
            reflCam.useOcclusionCulling = false;
            reflCam.targetTexture = rt;
            reflCam.clearFlags = CameraClearFlags.Skybox;

            Vector3 camPos = cam.transform.position;
            reflCam.transform.position = reflection.MultiplyPoint(camPos);
            Vector3 fwd = reflection.MultiplyVector(cam.transform.forward);
            Vector3 up = reflection.MultiplyVector(cam.transform.up);
            reflCam.transform.rotation = Quaternion.LookRotation(fwd, up);
            reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

            // Su altındaki geometri yansımaya girmesin: eğik kırpma düzlemi.
            Vector4 clipPlane = CameraSpacePlane(reflCam.worldToCameraMatrix, pos - normal * clipPlaneOffset, normal, 1f);
            reflCam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);

            var data = reflCam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderShadows = renderShadows;
                data.renderPostProcessing = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.antialiasing = AntialiasingMode.None;
                data.renderType = CameraRenderType.Base;
            }

            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(reflCam, request)) return;

            rendering = true;
            bool oldCull = GL.invertCulling;
            GL.invertCulling = !oldCull;      // ayna dönüşümü üçgen yönünü çevirir
            try { RenderPipeline.SubmitRenderRequest(reflCam, request); }
            finally
            {
                GL.invertCulling = oldCull;
                rendering = false;
            }

            Shader.SetGlobalTexture(TexId, rt);
            Shader.SetGlobalFloat(OnId, 1f);
        }

        void EnsureCamera(Camera src)
        {
            if (reflCam != null) return;
            var go = new GameObject("Planar Reflection Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            reflCam = go.AddComponent<Camera>();
            reflCam.enabled = false;
            go.AddComponent<UniversalAdditionalCameraData>();
        }

        void EnsureTexture(Camera cam)
        {
            int w = Mathf.Max(64, Mathf.RoundToInt(cam.pixelWidth * textureScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(cam.pixelHeight * textureScale));
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            rt = new RenderTexture(w, h, 16, RenderTextureFormat.DefaultHDR)
            {
                name = "DL_PlanarReflection",
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        static Vector4 CameraSpacePlane(Matrix4x4 worldToCam, Vector3 pos, Vector3 normal, float sign)
        {
            Vector3 cpos = worldToCam.MultiplyPoint(pos);
            Vector3 cnormal = worldToCam.MultiplyVector(normal).normalized * sign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }
    }
}
