// Köpük parçacıkları için özel unlit shader.
//
// URP'nin Particles/Unlit malzemesini kod ile saydama çevirmek güvenilir değil:
// _Surface/_Blend alanlarını yazmak yetmiyor, malzeme doğrulaması editörde
// çalışmadığı için karışım durumu opak kalabiliyor ve parçacıklar beyaz KARE
// olarak görünüyor. Burada karışım shader'ın içinde sabit — kaçacak yer yok.
Shader "Dredge/Wake Foam"
{
    Properties
    {
        _BaseMap  ("Kopuk atlasi", 2D) = "white" {}
        _BaseColor("Renk", Color) = (1, 1, 1, 1)
        _SoftFade ("Yumusak sonumleme (m)", Float) = 0.9
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WakeFoam"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _SoftFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : TEXCOORD1;
                float4 screenPos   : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);      // atlas karesi burada geliyor
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 col = tex * _BaseColor * IN.color;

                // Tekneye ya da kayaya değen köpük sertçe kesilmesin.
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fade = saturate((sceneEye - IN.screenPos.w) / max(_SoftFade, 0.01));
                col.a *= fade;

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
