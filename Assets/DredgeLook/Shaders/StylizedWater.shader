// ─────────────────────────────────────────────────────────────────────────────
// Dredge/StylizedWater  —  URP 12+ (Unity 2022.3+)
//
// Tasarım kararları (rastgele değil):
//  • Dalgalar VERTEX'te Gerstner ile üretilir. Yüksek frekanslı normal map YOK;
//    "ölü su" görüntüsünün ve titremenin ana sebebi odur.
//  • Renk, sahne derinliğinden okunur: sığ → derin lerp. Bu tek başına suya
//    hacim kazandırır.
//  • Kıyı köpüğü depth farkından üretilir (kıyı mesh'i gerekmez).
//  • Yansıma, skybox ile AYNI prosedürel fonksiyondan gelir → renk ayrışması olmaz.
//  • Tek ve geniş bir stilize güneş yolu vardır; sahnedeki tek parlak yüzey budur.
//
// GEREKSİNİM: URP Asset > Depth Texture AÇIK olmalı.
// ─────────────────────────────────────────────────────────────────────────────
Shader "Dredge/StylizedWater"
{
    Properties
    {
        [Header(Renk)]
        _ShallowColor      ("Sig Renk", Color) = (0.306, 0.549, 0.576, 1)
        _DeepColor         ("Derin Renk", Color) = (0.070, 0.141, 0.184, 1)
        _DepthFade         ("Derinlik Gecisi (m)", Range(0.5, 30)) = 6.5
        _AlphaShallow      ("Sigda Saydamlik", Range(0, 1)) = 0.55

        [Header(Kopuk)]
        _FoamColor         ("Kopuk Rengi", Color) = (0.917, 0.952, 0.960, 1)
        _FoamDistance      ("Kopuk Bandi (m)", Range(0, 8)) = 1.4
        _FoamSoftness      ("Kopuk Yumusakligi", Range(0.01, 0.6)) = 0.18
        _FoamNoiseScale    ("Kopuk Gurultu Olcegi", Range(0.05, 3)) = 0.55
        _FoamSpeed         ("Kopuk Hizi", Range(0, 2)) = 0.35
        _CrestFoam         ("Dalga Tepesi Kopugu", Range(0, 1)) = 0.25

        [Header(Dalgalar)]
        _WaveA             ("Dalga A (dirX,dirZ,diklik,dalgaboyu)", Vector) = (1, 0.15, 0.22, 22)
        _WaveB             ("Dalga B", Vector) = (0.7, 0.7, 0.16, 13)
        _WaveC             ("Dalga C", Vector) = (-0.5, 0.9, 0.10, 7)
        _WaveD             ("Dalga D", Vector) = (0.2, -1.0, 0.06, 3.5)
        _WaveAmplitude     ("Genel Dalga Siddeti", Range(0, 3)) = 1
        _WaveSpeed         ("Dalga Hizi", Range(0, 3)) = 1
        _WaveFadeDistance  ("Dalga Detayi Sonme Mesafesi", Range(20, 600)) = 180
        _NormalStrength    ("Normal Siddeti", Range(0, 2)) = 1

        [Header(Isik)]
        _SpecularColor     ("Parilti Rengi", Color) = (1, 0.964, 0.886, 1)
        _SpecularIntensity ("Parilti Siddeti", Range(0, 8)) = 2.4
        _SpecularPower     ("Parilti Sertligi", Range(4, 512)) = 90
        _SpecularSoftness  ("Parilti Kenar Yumusakligi", Range(0.001, 0.5)) = 0.12
        _ReflectionStrength("Yansima Gucu", Range(0, 1)) = 0.55
        _FresnelPower      ("Fresnel Ustu", Range(1, 8)) = 4
        _ReflectionBase    ("Yansima Tabani (fresnel disi)", Range(0, 1)) = 0.3
        _PlanarStrength    ("Duzlemsel Yansima (PlanarReflection gerekir)", Range(0, 1)) = 1
        _PlanarDistortion  ("Yansima Bozulmasi", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "DredgeCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthFade;
                float  _AlphaShallow;

                float4 _FoamColor;
                float  _FoamDistance;
                float  _FoamSoftness;
                float  _FoamNoiseScale;
                float  _FoamSpeed;
                float  _CrestFoam;

                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float4 _WaveD;
                float  _WaveAmplitude;
                float  _WaveSpeed;
                float  _WaveFadeDistance;
                float  _NormalStrength;

                float4 _SpecularColor;
                float  _SpecularIntensity;
                float  _SpecularPower;
                float  _SpecularSoftness;
                float  _ReflectionStrength;
                float  _FresnelPower;
                float  _ReflectionBase;
                float  _PlanarStrength;
                float  _PlanarDistortion;
            CBUFFER_END

            // PlanarReflection.cs tarafından her kare global olarak verilir.
            TEXTURE2D(_DL_PlanarReflection); SAMPLER(sampler_DL_PlanarReflection);
            float _DL_PlanarReflectionOn;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float  waveHeight : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Tek bir Gerstner dalgası. Tangent/binormal biriktirilir → analitik normal.
            float3 GerstnerWave(float4 wave, float3 p, float ampScale, float t,
                                inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z * ampScale;
                float wavelength = max(wave.w, 0.01);
                float k = 6.28318530718 / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy + 1e-5);
                float f = k * (dot(d, p.xz) - c * t);
                float a = steepness / k;

                float sf = sin(f);
                float cf = cos(f);

                tangent  += float3(-d.x * d.x * (steepness * sf),
                                    d.x * (steepness * cf),
                                   -d.x * d.y * (steepness * sf));
                binormal += float3(-d.x * d.y * (steepness * sf),
                                    d.y * (steepness * cf),
                                   -d.y * d.y * (steepness * sf));

                return float3(d.x * (a * cf), a * sf, d.y * (a * cf));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 basePos = posWS;

                float t = _Time.y * _WaveSpeed;
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 offset = 0;

                offset += GerstnerWave(_WaveA, basePos, _WaveAmplitude, t, tangent, binormal);
                offset += GerstnerWave(_WaveB, basePos, _WaveAmplitude, t, tangent, binormal);
                offset += GerstnerWave(_WaveC, basePos, _WaveAmplitude, t, tangent, binormal);
                offset += GerstnerWave(_WaveD, basePos, _WaveAmplitude, t, tangent, binormal);

                posWS += offset;

                OUT.positionWS = posWS;
                OUT.normalWS = normalize(cross(binormal, tangent));
                OUT.waveHeight = offset.y;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);

                // ── Sahne derinliği ────────────────────────────────────────────
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEye = IN.screenPos.w;
                float depthDiff = max(sceneEye - surfaceEye, 0);

                // ── Mesafeye göre dalga detayını söndür (uzakta titreme olmasın) ─
                float3 V = _WorldSpaceCameraPos - IN.positionWS;
                float viewDist = length(V);
                V = V / max(viewDist, 1e-5);

                float detail = saturate(1.0 - viewDist / max(_WaveFadeDistance, 1.0));
                float3 N = normalize(lerp(float3(0, 1, 0), IN.normalWS, detail * _NormalStrength));

                // ── Sığ / derin renk ───────────────────────────────────────────
                float depthT = saturate(1.0 - exp(-depthDiff / max(_DepthFade, 0.01)));
                half3 waterCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);

                // ── Yansıma (skybox ile aynı fonksiyon) + fresnel ──────────────
                float3 R = reflect(-V, N);
                R.y = abs(R.y) * 0.85 + 0.05;           // ufuk altına düşen yansımaları kırp
                half3 skyRefl = DL_SkyColor(R);
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // Düzlemsel yansıma: ayna kameranın çizdiği doku, ekran UV + dalga normaliyle
                // hafif bozulma. Adalar/tekne suda gerçekten görünür (DREDGE'in su hissi).
                half3 refl = skyRefl;
                float planarOn = _DL_PlanarReflectionOn * _PlanarStrength;
                if (planarOn > 0.001)
                {
                    // Dalga normali sakin denizde neredeyse düz; yansımanın "su gibi"
                    // titremesi için ayrı, yavaş bir gürültü alanı kullanılıyor.
                    float2 rippleUV = IN.positionWS.xz * 0.35 + _Time.y * float2(0.05, 0.035);
                    float2 ripple = float2(DL_FBM(rippleUV), DL_FBM(rippleUV + 17.3)) - 0.5;
                    float2 ruv = screenUV + (N.xz * 0.5 + ripple * 0.35) * _PlanarDistortion * lerp(0.6, 1.0, detail);
                    half3 planar = SAMPLE_TEXTURE2D(_DL_PlanarReflection, sampler_DL_PlanarReflection, ruv).rgb;
                    refl = lerp(skyRefl, planar, planarOn);
                }
                float reflW = saturate(fresnel * _ReflectionStrength + _ReflectionBase * _ReflectionStrength);
                waterCol = lerp(waterCol, refl, reflW);

                // ── Stilize güneş parıltısı (sahnedeki tek speküler) ───────────
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecularPower);
                spec = smoothstep(0.5 - _SpecularSoftness, 0.5 + _SpecularSoftness, spec);
                waterCol += _SpecularColor.rgb * mainLight.color * spec * _SpecularIntensity;

                // ── Köpük ─────────────────────────────────────────────────────
                float edge = 1.0 - saturate(depthDiff / max(_FoamDistance, 0.001));
                float2 noiseUV = IN.positionWS.xz * _FoamNoiseScale + _Time.y * _FoamSpeed * float2(0.3, 0.17);
                float n = DL_FBM(noiseUV);
                float foam = smoothstep(0.5 - _FoamSoftness, 0.5 + _FoamSoftness, edge * (0.55 + 0.9 * n));

                // Dalga tepelerinde ince köpük
                float crest = saturate(IN.waveHeight / max(_WaveAmplitude, 0.01) - 0.55);
                foam = saturate(foam + crest * _CrestFoam * n * detail);

                waterCol = lerp(waterCol, _FoamColor.rgb, foam);

                // ── Saydamlık ─────────────────────────────────────────────────
                float alpha = lerp(_AlphaShallow, 1.0, depthT);
                alpha = saturate(alpha + foam);

                // ── Sis (kara ile aynı sis → ufuk çizgisi kaybolmaz) ───────────
                half3 final = MixFog(waterCol, IN.fogFactor);
                return half4(final, alpha);
            }
            ENDHLSL
        }
    }

    // Fallback YOK: fallback'in ShadowCaster pass'i dalgasız (düz) mesh'in gölgesini
    // düşürür ve deniz tabanına düz bir dikdörtgen gölge basar.
    Fallback Off
}
