// ─────────────────────────────────────────────────────────────────────────────
// Dredge/StylizedSky  —  3 renkli gradyan + ufuk bandı + güneş diski + hale
// Skybox materyali olarak kullanılır (Lighting > Environment > Skybox Material).
// StylizedAtmosphere bu materyalin değerlerini preset'ten sürer.
// ─────────────────────────────────────────────────────────────────────────────
Shader "Dredge/StylizedSky"
{
    Properties
    {
        _ZenithColor     ("Zenit (tepe)", Color) = (0.294, 0.549, 0.769, 1)
        _HorizonColor    ("Ufuk", Color) = (0.780, 0.858, 0.902, 1)
        _GroundColor     ("Yer / pus", Color) = (0.713, 0.772, 0.803, 1)
        _HorizonPower    ("Ufuk Keskinligi", Range(0.2, 8)) = 1.8
        _HorizonGlow     ("Ufuk Parlakligi", Range(0, 2)) = 0.35

        _SunDiscColor    ("Gunes Diski", Color) = (1, 0.972, 0.909, 1)
        _SunDiscSize     ("Gunes Boyutu", Range(0, 0.05)) = 0.006
        _SunDiscSoftness ("Gunes Kenar Yumusakligi", Range(0.0005, 0.02)) = 0.0025
        _SunGlowColor    ("Hale Rengi", Color) = (1, 0.913, 0.768, 1)
        _SunGlowFalloff  ("Hale Darligi", Range(2, 512)) = 48

        _StarColor       ("Yildiz Rengi", Color) = (0.85, 0.9, 1, 1)
        _StarStrength    ("Yildiz Siddeti (gece icin)", Range(0, 2)) = 0
        _StarDensity     ("Yildiz Yogunlugu", Range(50, 900)) = 340

        _Exposure        ("Pozlama", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DredgeCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _GroundColor;
                float  _HorizonPower;
                float  _HorizonGlow;
                float4 _SunDiscColor;
                float  _SunDiscSize;
                float  _SunDiscSoftness;
                float4 _SunGlowColor;
                float  _SunGlowFalloff;
                float4 _StarColor;
                float  _StarStrength;
                float  _StarDensity;
                float  _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Skybox mesh'i identity rotasyonla çizilir; yine de döndürülmüş bir
                // sky-dome mesh'ine atanırsa doğru çalışsın diye dünya uzayına çeviriyoruz.
                OUT.dir = mul((float3x3)unity_ObjectToWorld, IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);
                float h = dir.y;

                float hp = max(_HorizonPower, 0.0001);
                float up = pow(saturate(h), 1.0 / hp);
                float dn = pow(saturate(-h), 1.0 / hp);

                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, up);
                col = lerp(col, _GroundColor.rgb, dn);

                // Ufuk çizgisindeki parlak bant
                float band = pow(saturate(1.0 - abs(h)), 8.0);
                col += _HorizonColor.rgb * band * _HorizonGlow;

                float3 sunDir = normalize(_DL_SunDirection.xyz + float3(0, 1e-5, 0));
                float sd = dot(dir, sunDir);

                // Yıldızlar (gece preset'inde _StarStrength > 0)
                if (_StarStrength > 0.001)
                {
                    // Nokta yıldızlar: hücre başına tek, yuvarlak, seyrek (eski sürüm kare hücreler yakıyordu)
                    float2 suv = dir.xz / max(dir.y + 0.15, 0.2) * _StarDensity * 0.6;
                    float2 cell = floor(suv), f = frac(suv);
                    float r = DL_Hash21(cell);
                    float2 pos = float2(DL_Hash21(cell + 7.1), DL_Hash21(cell + 3.3));
                    float d = length(f - pos);
                    float twinkle = 0.7 + 0.3 * sin(_Time.y * (1.5 + r * 3.0) + r * 40.0);
                    float star = (r > 0.975) ? smoothstep(0.08, 0.0, d) * twinkle : 0.0;
                    star *= saturate(h * 2.0);
                    col += _StarColor.rgb * star * _StarStrength * 3.0;
                }

                // Hale — su yansıması da bunu tekrarlar, güneş yolu böyle oluşur
                col += _SunGlowColor.rgb * pow(saturate(sd), max(_SunGlowFalloff, 1.0)) * _HorizonGlow;

                // Disk
                float disc = smoothstep(1.0 - _SunDiscSize - _SunDiscSoftness,
                                        1.0 - _SunDiscSize, sd);
                col = lerp(col, _SunDiscColor.rgb, saturate(disc));

                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
