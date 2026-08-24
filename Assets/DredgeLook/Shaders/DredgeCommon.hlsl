#ifndef DREDGE_COMMON_INCLUDED
#define DREDGE_COMMON_INCLUDED

// ─────────────────────────────────────────────────────────────────────────────
// StylizedAtmosphere.cs tarafından set edilen global'ler.
// Su, arazi ve gökyüzü aynı kaynaktan beslendiği için renkler asla ayrışmaz.
// ─────────────────────────────────────────────────────────────────────────────
float4 _DL_SunDirection;     // güneşe DOĞRU birim vektör
float4 _DL_SunColor;         // renk * yoğunluk
float4 _DL_ShadowTint;
float4 _DL_FogColor;
float4 _DL_SkyZenith;
float4 _DL_SkyHorizon;
float4 _DL_SkyGround;
float4 _DL_SunGlowColor;
float4 _DL_SkyParams;        // x = horizonPower, y = horizonGlow, z = sunGlowFalloff, w = exposure

// ─────────────────────────── Prosedürel gürültü ───────────────────────────
float DL_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float DL_Noise21(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = DL_Hash21(i);
    float b = DL_Hash21(i + float2(1, 0));
    float c = DL_Hash21(i + float2(0, 1));
    float d = DL_Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float DL_FBM(float2 p)
{
    float v = 0.0;
    float amp = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        v += amp * DL_Noise21(p);
        p *= 2.03;
        amp *= 0.5;
    }
    return v;
}

// ───────────────────────── Prosedürel gökyüzü ─────────────────────────
// Hem skybox hem de su yansıması bu fonksiyonu kullanır → renkler birebir eşleşir.
float3 DL_SkyColor(float3 dir)
{
    dir = normalize(dir);   // çağıranlar normalize etmiş olmayabilir

    float horizonPower = max(_DL_SkyParams.x, 0.0001);
    float horizonGlow  = _DL_SkyParams.y;
    float glowFalloff  = max(_DL_SkyParams.z, 1.0);
    float exposure     = _DL_SkyParams.w;

    float h = dir.y;

    // Üst yarı: ufuk → zenit,  alt yarı: ufuk → yer
    float up = pow(saturate(h), 1.0 / horizonPower);
    float dn = pow(saturate(-h), 1.0 / horizonPower);

    float3 col = lerp(_DL_SkyHorizon.rgb, _DL_SkyZenith.rgb, up);
    col = lerp(col, _DL_SkyGround.rgb, dn);

    // Ufuk çizgisindeki parlak bant — derinlik hissinin yarısı buradan gelir
    float band = pow(saturate(1.0 - abs(h)), 8.0);
    col += _DL_SkyHorizon.rgb * band * horizonGlow;

    // Güneş halesi (sudaki güneş yolu da bunu yansıtır)
    float sd = saturate(dot(dir, normalize(_DL_SunDirection.xyz)));
    col += _DL_SunGlowColor.rgb * pow(sd, glowFalloff) * horizonGlow;

    return col * exposure;
}

#endif
