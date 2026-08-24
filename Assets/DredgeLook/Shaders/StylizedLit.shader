// ─────────────────────────────────────────────────────────────────────────────
// Dredge/StylizedLit  —  kaya, arazi, ağaç, prop için bantlı (toon) aydınlatma
//
// Tasarım kararları:
//  • Işık 2-3 basamaklı bir rampadan geçer; PBR'nin yumuşak gradyanı yok.
//  • Gölge rengi siyah değil, StylizedAtmosphere'den gelen shadowTint.
//  • Speküler YOK (isteğe bağlı açılabilir). DREDGE'de tek parlak yüzey sudur.
//  • Sis, su ile aynı RenderSettings sisini kullanır → ufuk hep tutarlı.
// ─────────────────────────────────────────────────────────────────────────────
Shader "Dredge/StylizedLit"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Doku", 2D) = "white" {}
        [MainColor]   _BaseColor ("Renk", Color) = (0.85, 0.85, 0.83, 1)

        [Header(Bantli Isik)]
        _Bands           ("Basamak Sayisi", Range(1, 5)) = 2
        _BandSoftness    ("Basamak Yumusakligi", Range(0.001, 0.5)) = 0.06
        _LightWrap       ("Isik Sarilmasi", Range(0, 1)) = 0.25
        _ShadowColor     ("Golge Rengi (kullanilmaz ise atmosferden gelir)", Color) = (0.243, 0.357, 0.478, 1)
        _ShadowStrength  ("Golge Koyulugu", Range(0, 1)) = 0.75
        [Toggle(_USE_ATMOSPHERE_TINT)] _UseAtmosphereTint ("Golge Rengini Atmosferden Al", Float) = 1

        [Header(Ambient)]
        _AmbientStrength ("Ambient Siddeti", Range(0, 2)) = 1

        [Header(Rim)]
        _RimColor    ("Rim Rengi", Color) = (1, 1, 1, 1)
        _RimPower    ("Rim Ustu", Range(0.5, 12)) = 4
        _RimStrength ("Rim Siddeti", Range(0, 2)) = 0.15

        [Header(Spekuler (varsayilan kapali))]
        [Toggle(_SPECULAR_ON)] _SpecularOn ("Spekuler Ac", Float) = 0
        _SpecColor2   ("Spekuler Rengi", Color) = (1, 1, 1, 1)
        _SpecPower    ("Spekuler Sertligi", Range(4, 256)) = 48
        _SpecStrength ("Spekuler Siddeti", Range(0, 2)) = 0.2

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (yapraklar icin)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ─────────────────────────────── FORWARD ───────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma shader_feature_local_fragment _USE_ATMOSPHERE_TINT
            #pragma shader_feature_local_fragment _SPECULAR_ON
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // Forward+ renderer'da bu keyword olmazsa ek ışıklar tamamen kaybolur:
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DredgeCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Bands;
                float  _BandSoftness;
                float  _LightWrap;
                float4 _ShadowColor;
                float  _ShadowStrength;
                float  _AmbientStrength;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float4 _SpecColor2;
                float  _SpecPower;
                float  _SpecStrength;
                float  _Cull;
                float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Yumuşak kenarlı basamak rampası — toon gölgelemenin kalbi
            float BandRamp(float x, float bands, float softness)
            {
                bands = max(bands, 1.0);
                float t = saturate(x) * bands;
                float f = floor(t);
                float fr = frac(t);
                return (f + smoothstep(0.0, max(softness, 0.001), fr)) / bands;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = tex * _BaseColor;

                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 L = normalize(mainLight.direction);
                float ndl = dot(N, L);

                // Işık sarılması: terminatörü yumuşatır, sert siyah kenar oluşmaz
                float wrapped = saturate((ndl + _LightWrap) / (1.0 + _LightWrap));

                float shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);
                float ramp = BandRamp(wrapped * shadowAtten, _Bands, _BandSoftness);

                // Gölge rengi: atmosferden ya da materyalden
                #if defined(_USE_ATMOSPHERE_TINT)
                    half3 shadeTint = _DL_ShadowTint.rgb;
                #else
                    half3 shadeTint = _ShadowColor.rgb;
                #endif

                half3 lightCol = mainLight.color;
                half3 litCol = albedo.rgb * lightCol;
                half3 shadeCol = albedo.rgb * shadeTint;

                half3 color = lerp(shadeCol, litCol, ramp);

                // Ambient (Trilight gradient) — SampleSH normal yönüne göre okur
                half3 ambient = SampleSH(N) * _AmbientStrength;
                color += albedo.rgb * ambient;

                // SSAO
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    float2 nsUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(nsUV);
                    color *= ao.indirectAmbientOcclusion;
                #endif

                // Ek ışıklar (fener, ateş) — bunlar da bantlanır.
                // LIGHT_LOOP_BEGIN makrosu Forward+ cluster'ı ile klasik döngüyü
                // aynı kodla çalıştırır; 'inputData' adında bir değişken bekler.
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light li = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                        float lndl = saturate((dot(N, li.direction) + _LightWrap) / (1.0 + _LightWrap));
                        float lramp = BandRamp(lndl, _Bands, _BandSoftness);
                        color += albedo.rgb * li.color * lramp * li.distanceAttenuation * li.shadowAttenuation;
                    LIGHT_LOOP_END
                #endif

                // Rim — kayaların sisten ayrılmasını sağlar
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                rim = smoothstep(0.3, 0.7, rim);
                color += _RimColor.rgb * rim * _RimStrength * ramp;

                // Opsiyonel speküler (varsayılan kapalı — bilerek)
                #if defined(_SPECULAR_ON)
                    float3 H = normalize(L + V);
                    float sp = pow(saturate(dot(N, H)), _SpecPower);
                    sp = smoothstep(0.45, 0.55, sp);
                    color += _SpecColor2.rgb * lightCol * sp * _SpecStrength * ramp;
                #endif

                color = MixFog(color, IN.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // ─────────────────────────────── SHADOW ───────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Bands;
                float  _BandSoftness;
                float  _LightWrap;
                float4 _ShadowColor;
                float  _ShadowStrength;
                float  _AmbientStrength;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float4 _SpecColor2;
                float  _SpecPower;
                float  _SpecStrength;
                float  _Cull;
                float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            SVaryings ShadowVert(SAttributes IN)
            {
                SVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(SVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                    clip(a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────── DEPTH ───────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Bands;
                float  _BandSoftness;
                float  _LightWrap;
                float4 _ShadowColor;
                float  _ShadowStrength;
                float  _AmbientStrength;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float4 _SpecColor2;
                float  _SpecPower;
                float  _SpecStrength;
                float  _Cull;
                float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct DAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings DepthVert(DAttributes IN)
            {
                DVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthFrag(DVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                    clip(a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────── DEPTH NORMALS (SSAO icin) ───────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Bands;
                float  _BandSoftness;
                float  _LightWrap;
                float4 _ShadowColor;
                float  _ShadowStrength;
                float  _AmbientStrength;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float4 _SpecColor2;
                float  _SpecPower;
                float  _SpecStrength;
                float  _Cull;
                float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct NAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            NVaryings DNVert(NAttributes IN)
            {
                NVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DNFrag(NVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                    clip(a - _Cutoff);
                #endif
                // URP DepthNormals HAM dünya normali bekler (0.5+0.5 kodlaması YOK)
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
