// Ayrıntılı deniz ızgarasının bittiği yerden gerçek ufka kadar görüşü dolduran
// düzlem.
//
// Kritik nokta: bu shader'ın DepthOnly geçişi YOK. Dolayısıyla URP'nin derinlik
// ön-geçişine girmiyor ve _CameraDepthTexture'da görünmüyor. Görünseydi su
// shader'ı "altımda 2 metrede zemin var" diye okuyup bütün denizi sığ renkle
// boyardı. Bu haliyle su, ızgaranın ötesinde sonsuz derinlik görüyor.
Shader "Dredge/Horizon"
{
    Properties
    {
        _BaseColor ("Renk", Color) = (0.72, 0.66, 0.63, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry-1" }

        Pass
        {
            Name "Horizon"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float fogFactor : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, IN.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
