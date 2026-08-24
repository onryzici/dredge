# Dredge Look — Unity URP Stilize Atmosfer & Su Paketi

DREDGE'in görsel dilini Unity URP'de yeniden üretmek için hazırlanmış paket.
Her görsel parametre **Inspector'dan canlı ayarlanabilir** — Play'e basmana gerek yok.

---

## Gereksinimler

- Unity **2022.3 LTS** veya üzeri
- **Universal Render Pipeline** (URP 14+) kurulu ve Project Settings > Graphics'te atanmış
- Color Space: **Linear** (Project Settings > Player)

---

## Kurulum (3 dakika)

1. `DredgeLook` klasörünü olduğu gibi projendeki `Assets/` içine kopyala.
2. Unity'nin derlemeyi bitirmesini bekle (Console'da hata olmamalı).
3. Menüden **Tools > Dredge Look > 1 - Sahneyi Kur**'a bas.

Bu tek tık şunları yapar:
- `Assets/DredgeLook/Generated/` altında sky + su materyalleri, volume profile ve **5 hazır preset** oluşturur
- Sahneye `Dredge Atmosphere` (ışık/sis/gökyüzü/post kontrolü) ve `Dredge Water` (sonsuz su düzlemi) ekler
- Skybox'ı bağlar, URP asset'inde Depth Texture / HDR / Shadow Distance ayarlarını düzeltir

4. **SSAO'yu elle ekle:** URP Renderer asset'ini seç → `Add Renderer Feature` → `Screen Space Ambient Occlusion` → Intensity `0.5`, Radius `0.35`. (Bunu script güvenli şekilde yapamıyor.)

5. Teknen için: tekne objesine `Buoyant Object`, kameraya `Dredge Camera Rig` ekle ve `target`'a tekneyi ata.

---

## Kullanım

`Dredge Atmosphere` objesini seç. Inspector'da:

- **Preset A / Preset B / Blend** — iki atmosfer arasında 0-1 geçiş. Gün döngüsü için `blend` değerini zamanla sür.
- **usePresets kapalı** → aşağıdaki `Live Values` bloğunu doğrudan elle ayarla.
- **Live Values → Preset A** butonu ile beğendiğin ayarı preset'e kaydet.

Inspector, iki klasik hatayı otomatik uyarır: sis rengi ufuk renginden uzaklaştığında ve ambient güneşi bastırdığında.

### Ayar sırası (bozma)

1. Post Processing'i kapat → 2. Gökyüzü gradyanı → 3. Sis (rengi ufka eşitle, density'yi aç) → 4. Güneş açısı → 5. Ambient → 6. Su → 7. En son post processing.

---

## Dosyalar

```
DredgeLook/
├─ Docs/DREDGE_SANAT_YONU.md   ← Sanat yönü + Claude'a verilecek prompt (ÖNCE BUNU OKU)
├─ Runtime/
│   ├─ AtmosphereValues.cs      Tüm görsel parametreler + Lerp
│   ├─ AtmospherePreset.cs      ScriptableObject preset
│   ├─ StylizedAtmosphere.cs    Ana kontrol paneli (ışık, sis, ambient, sky, su, post)
│   ├─ InfiniteWaterPlane.cs    Kamerayı takip eden bölünmüş su mesh'i
│   ├─ WaterSurface.cs          Shader'la birebir aynı Gerstner matematiği (CPU)
│   ├─ BuoyantObject.cs         Tekne sallanması
│   └─ DredgeCameraRig.cs       DREDGE kadrajı (yükseklik 9m, pitch 28°, FOV 45)
├─ Shaders/
│   ├─ DredgeCommon.hlsl        Ortak gökyüzü fonksiyonu + gürültü
│   ├─ StylizedSky.shader       Gradyan gökyüzü + güneş diski + yıldızlar
│   ├─ StylizedWater.shader     Gerstner + derinlik rengi + köpük + güneş yolu
│   └─ StylizedLit.shader       Bantlı (toon) aydınlatma — kaya/ağaç/prop için
└─ Editor/
    ├─ DredgeLookSetup.cs           Tools menüsü + 5 preset
    └─ StylizedAtmosphereEditor.cs  Özel inspector + uyarılar
```

---

## Sık karşılaşılan sorunlar

| Belirti | Sebep | Çözüm |
|---|---|---|
| Su tamamen düz, dalga yok | Su düzlemi az bölünmüş | `InfiniteWaterPlane > resolution` en az 120 olsun |
| Köpük hiç görünmüyor | URP Depth Texture kapalı | Tools > Dredge Look > 3 - URP Ayarlarını Düzelt |
| Su siyah / yansıma yok | `StylizedAtmosphere` sahnede yok ya da kapalı | Atmosfer objesi sky global'lerini besler, aktif olmalı |
| Uzakta su titriyor | Dalga detayı çok uzağa gidiyor | `_WaveFadeDistance`'ı düşür (120-180) |
| Renkler soluk/yıkanmış | Gamma/linear uyuşmazlığı | `StylizedAtmosphere > linearColorFix` aç |
| Gölgeler kademeli/çirkin | Shadow distance çok büyük | 80-120m yap, Cascade 2 |
| Ekran çok parlak | Bloom + skyExposure birlikte yüksek | Önce `skyExposure`'ı 1'e sabitle, bloom'u 0.2'ye çek |
| Tekne dalgalara girip çıkıyor | `waterlineOffset` düşük | 0.2-0.4 arası dene, `maxTilt`'i 12-16 tut |

---

## Kendi shader'ını yazan Claude'a

`Docs/DREDGE_SANAT_YONU.md` dosyasının 6. bölümünde hazır bir prompt bloğu var.
Onu kopyala-yapıştır; kod yazan Claude ekranı göremediği için estetik kararları
o bloktaki sayısal kısıtlardan alacak.
