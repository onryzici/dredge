// Dredge tarzı stilize deniz.
//   • Gerstner dalgaları (4 adet) — parametreler C# tarafından global olarak veriliyor,
//     böylece teknenin yüzdüğü CPU dalga alanı ile birebir aynı.
//   • Derinliğe göre renk geçişi: sığ turkuaz → dip lacivert (sahne derinlik dokusu).
//   • Kıyı köpüğü, güneş parıltısı, Fresnel gökyüzü yansıması, sis.
Shader "Dredge/Stylized Water"
{
    Properties
    {
        [Header(Renk)]
        _ShallowColor ("Sig su", Color)            = (0.19, 0.45, 0.45, 1)
        _DeepColor    ("Derin su", Color)          = (0.016, 0.055, 0.085, 1)
        _DepthFade    ("Derinlik gecisi (m)", Float) = 7
        _SkyTint      ("Gokyuzu yansimasi", Color) = (0.35, 0.44, 0.55, 1)
        _FresnelPower ("Fresnel keskinligi", Float) = 5

        [Header(Kopuk)]
        _FoamColor  ("Kopuk rengi", Color)          = (0.82, 0.90, 0.92, 1)
        _FoamDepth  ("Kopuk genisligi (m)", Float)  = 1.1
        _FoamCutoff ("Kopuk kesme", Range(0,1))     = 0.35
        _FoamStrength ("Kopuk gucu", Range(0,1))    = 0.7

        [Header(Isik)]
        _GlintColor ("Gunes parilti rengi", Color)  = (1, 0.82, 0.62, 1)
        _GlintPower ("Parilti keskinligi", Float)   = 220
        _GlintStrength ("Parilti gucu", Float)      = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-100" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthFade;
                float4 _SkyTint;
                float  _FresnelPower;
                float4 _FoamColor;
                float  _FoamDepth;
                float  _FoamCutoff;
                float  _FoamStrength;
                float4 _GlintColor;
                float  _GlintPower;
                float  _GlintStrength;
            CBUFFER_END

            // C# tarafindan Shader.SetGlobal* ile besleniyor (UnityPerMaterial disinda olmali).
            // xy = yon, z = diklik, w = dalga boyu.  Kullanilmayan yuvalar (0,0,0,1).
            #define WAVE_SLOTS 8
            float4 _Waves[WAVE_SLOTS];
            float  _WaveTime;

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 42.13);
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

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 screenPos   : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            // Tek bir Gerstner dalgasi; teget ve binormali biriktirir.
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                float wavelength = max(wave.w, 0.01);
                float k = TWO_PI / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy + 1e-5);
                float f = k * (dot(d, p.xz) - c * _WaveTime);
                float a = steepness / k;

                float sinF, cosF;
                sincos(f, sinF, cosF);

                tangent  += float3(-d.x * d.x * (steepness * sinF),
                                    d.x * (steepness * cosF),
                                   -d.x * d.y * (steepness * sinF));
                binormal += float3(-d.x * d.y * (steepness * sinF),
                                    d.y * (steepness * cosF),
                                   -d.y * d.y * (steepness * sinF));

                return float3(d.x * (a * cosF), a * sinF, d.y * (a * cosF));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 p = positionWS;

                [unroll] for (int w = 0; w < WAVE_SLOTS; w++)
                    p += GerstnerWave(_Waves[w], positionWS, tangent, binormal);

                // Gerstner toplamı kendi başına düzenli bir desen üretiyor; yavaş
                // sürüklenen gürültü onu kırıp denizi tekdüzelikten çıkarıyor.
                float2 dn = positionWS.xz * 0.11 + float2(_WaveTime * 0.035, _WaveTime * -0.021);
                p.y += (ValueNoise(dn) - 0.5) * 0.55 + (ValueNoise(dn * 2.7 + 13.0) - 0.5) * 0.22;

                OUT.positionWS = p;
                OUT.normalWS = normalize(cross(binormal, tangent));
                OUT.positionHCS = TransformWorldToHClip(p);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);

                // Suyun altindaki sahnenin ne kadar derinde oldugu
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEye = IN.screenPos.w;
                float waterDepth = max(sceneEye - surfaceEye, 0);

                float3 normalWS = normalize(IN.normalWS);

                // Yüzey normalini de kır: parıltı ve yansıma her yerde aynı olmasın.
                float2 nUV = IN.positionWS.xz * 0.19 + float2(_WaveTime * 0.03, _WaveTime * 0.017);
                float nA = ValueNoise(nUV) - 0.5;
                float nB = ValueNoise(nUV + 61.7) - 0.5;
                float nC = ValueNoise(nUV * 3.1 - 24.0) - 0.5;
                normalWS = normalize(normalWS + float3(nA * 0.34 + nC * 0.12, 0, nB * 0.34 - nC * 0.10));
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                Light mainLight = GetMainLight();

                // --- gövde rengi: derinlige gore ---------------------------------
                float depth01 = saturate(waterDepth / max(_DepthFade, 0.01));
                float3 baseCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                // Yüzeye çarpan ışık suyu biraz açsın ama düz boyamasın.
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                baseCol *= lerp(0.75, 1.15, ndotl) * (0.35 + 0.65 * mainLight.color.g + 1e-4);

                // --- gökyüzü yansıması --------------------------------------------
                // Düz bir renkle boyamak yerine skybox'ı gerçekten örnekliyoruz;
                // bulutlar ve batan güneş suyun üstünde görünüyor.
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half4 encodedSky = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, 2.0);
                float3 skyCol = DecodeHDREnvironment(encodedSky, unity_SpecCube0_HDR) * _SkyTint.rgb * 1.6;
                baseCol = lerp(baseCol, skyCol, saturate(fresnel * 0.9));

                // --- güneş parıltısı ----------------------------------------------
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float ndoth = saturate(dot(normalWS, halfDir));
                float glint = pow(ndoth, _GlintPower);                       // keskin kıvılcımlar
                float sunPath = pow(ndoth, _GlintPower * 0.04) * 0.35;       // güneşin su üstündeki yolu
                baseCol += _GlintColor.rgb * (glint * _GlintStrength + sunPath) * mainLight.shadowAttenuation;

                // --- kıyı köpüğü ---------------------------------------------------
                float foamLine = 1.0 - saturate(waterDepth / max(_FoamDepth, 0.01));
                float ripple = sin(IN.positionWS.x * 0.6 + IN.positionWS.z * 0.5 + _WaveTime * 1.6) * 0.5 + 0.5;
                // Sert step() tekne çevresinde çirkin, parlayan bir bant bırakıyordu;
                // yumuşak geçiş kıyı köpüğünü koruyup o bandı siliyor.
                float band = foamLine * (0.72 + 0.28 * ripple);
                float foam = smoothstep(_FoamCutoff, _FoamCutoff + 0.24, band);
                foam = saturate(foam * _FoamStrength);
                baseCol = lerp(baseCol, _FoamColor.rgb, foam);

                // Kıyıda yumuşak geçiş için saydamlık.
                float alpha = saturate(waterDepth * 1.6);
                alpha = max(alpha, foam);

                baseCol = MixFog(baseCol, IN.fogFactor);
                return half4(baseCol, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
