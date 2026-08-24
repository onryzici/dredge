// Alacakaranlık gökyüzü: zenit → ufuk gradyanı, büyük yumuşak güneş ve
// keskin kenarlı düşük poligon bulut katmanları.
// Bulutlar Dredge'in gökyüzü karakterinin yarısı; düz gradyan cansız kalıyor.
Shader "Dredge/Gradient Sky"
{
    Properties
    {
        [Header(Gradyan)]
        _ZenithColor  ("Zenit", Color)            = (0.24, 0.30, 0.42, 1)
        _HorizonColor ("Ufuk", Color)             = (0.95, 0.68, 0.48, 1)
        _GroundColor  ("Ufuk alti", Color)        = (0.30, 0.33, 0.36, 1)
        _HorizonBlend ("Gecis keskinligi", Float) = 0.42

        [Header(Gunes)]
        _SunColor     ("Gunes", Color)            = (1.0, 0.92, 0.72, 1)
        _SunSize      ("Gunes capi", Range(0.9,1)) = 0.9975
        _SunSoftness  ("Kenar yumusakligi", Float) = 0.0016
        _SunGlow      ("Hale keskinligi", Float)  = 9
        _SunGlowStrength ("Hale gucu", Float)     = 0.32
        _SunDirection ("Gunes yonu", Vector)      = (0.5, 0.12, 0.85, 0)

        [Header(Bulut)]
        _CloudDark    ("Bulut golgesi", Color)    = (0.32, 0.33, 0.42, 1)
        _CloudLit     ("Bulut isikli", Color)     = (1.0, 0.80, 0.64, 1)
        _CloudCoverage("Kaplama", Range(0,1))     = 0.46
        _CloudSoftness("Kenar yumusakligi", Range(0.001,0.4)) = 0.06
        _CloudScale   ("Olcek", Float)            = 0.9
        _CloudSpeed   ("Suruklenme hizi", Float)  = 0.006
        _CloudOpacity ("Yogunluk", Range(0,1))    = 0.92
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor, _HorizonColor, _GroundColor;
                float  _HorizonBlend;
                float4 _SunColor, _SunDirection;
                float  _SunSize, _SunSoftness, _SunGlow, _SunGlowStrength;
                float4 _CloudDark, _CloudLit;
                float  _CloudCoverage, _CloudSoftness, _CloudScale, _CloudSpeed, _CloudOpacity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 dirOS : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirOS = IN.positionOS.xyz;
                return OUT;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0, amp = 0.55;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    sum += amp * ValueNoise(p);
                    p = p * 2.03 + 17.3;
                    amp *= 0.5;
                }
                return sum;
            }

            // Gökyüzü kubbesini düzleme yansıtıp bulut örtüsünü orada hesaplıyoruz.
            float CloudLayer(float3 d, float scale, float2 drift, float coverage)
            {
                float2 uv = d.xz / max(d.y, 0.10) * scale + drift;
                float n = Fbm(uv);
                return smoothstep(coverage, coverage + _CloudSoftness, n);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dirOS);
                float3 sunDir = normalize(_SunDirection.xyz);

                // --- gradyan ------------------------------------------------------
                float t = pow(saturate(d.y), max(_HorizonBlend, 0.01));
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);

                // Güneşin bulunduğu tarafta ufuk daha sıcak yansın.
                float sunSide = saturate(dot(normalize(float3(d.x, 0, d.z)), normalize(float3(sunDir.x, 0, sunDir.z))));
                col = lerp(col, _HorizonColor.rgb * 1.25, pow(sunSide, 3.0) * saturate(1.0 - d.y * 3.0) * 0.55);
                col = lerp(_GroundColor.rgb, col, saturate(d.y * 12.0 + 0.5));

                // --- güneş --------------------------------------------------------
                float sd = dot(d, sunDir);
                col += _SunColor.rgb * _SunGlowStrength * pow(saturate(sd), _SunGlow);
                // Kurs beyaza patlamasın; parlaklığın çoğu bloom'dan geliyordu.
                col = lerp(col, _SunColor.rgb * 1.05, smoothstep(_SunSize - _SunSoftness, _SunSize + _SunSoftness, sd));

                // --- bulutlar -----------------------------------------------------
                float time = _Time.y;
                float far  = CloudLayer(d, _CloudScale * 0.55, float2(time * _CloudSpeed * 0.6, time * _CloudSpeed * 0.25), _CloudCoverage + 0.06);
                float near = CloudLayer(d, _CloudScale, float2(time * _CloudSpeed + 40.0, time * _CloudSpeed * 0.4), _CloudCoverage);

                float horizonMask = smoothstep(0.0, 0.16, d.y);            // ufkun dibinde bulut yok
                float3 cloudCol = lerp(_CloudDark.rgb, _CloudLit.rgb, pow(saturate(sd * 0.5 + 0.5), 2.5));

                col = lerp(col, cloudCol * 0.92, far  * horizonMask * _CloudOpacity * 0.6);
                col = lerp(col, cloudCol,        near * horizonMask * _CloudOpacity);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
