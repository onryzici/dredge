# DREDGE Görsel Dili — Teknik Sanat Yönü Dokümanı
### Unity URP için referans + Claude'a verilecek prompt

Bu doküman iki iş yapar:
1. **Senin için:** DREDGE'in "o hissi" neden verdiğini ölçülebilir parametrelere çevirir.
2. **Claude için:** En sonda kopyala-yapıştır bir prompt bloğu var. Kod yazan Claude ekranı göremez; bu yüzden ona "güzel yap" değil, **sayı** vermek gerekir. Bu doküman o sayıları içerir.

---

## 1. Teşhis — Senin sahnenle DREDGE arasındaki gerçek fark

Sorun "grafik kalitesi" değil. Dört tane spesifik, düzeltilebilir hata var:

| # | Senin sahnende | DREDGE'de | Etkisi |
|---|---|---|---|
| 1 | Su yüksek frekanslı, gerçekçi normal map'li, koyu lacivert, tek renk | Su **düşük frekanslı**, geniş dalgalı, derinliğe göre renk değiştiren, gökyüzünü yansıtan | Suyun "ölü" görünmesinin tek sebebi bu |
| 2 | Sis pembemsi-bej, her yönde eşit, ufku yok ediyor | Sis rengi **gökyüzü ufuk rengiyle birebir aynı**, mesafeyle üstel artıyor | Ufuk çizgisi geri geliyor, derinlik hissi doğuyor |
| 3 | Her nesne benzer parlaklıkta (orta ton çorbası) | **Value hiyerarşisi**: yakın ağaçlar neredeyse siyah, uzak kayalar neredeyse beyaz | Siluet ve okunabilirlik |
| 4 | Renk düzenlemesi (color grading) yok | Tonemapping + kontrast + split toning + hafif vignette | "Oyun gibi" değil "poster gibi" görünmesi |

Beşinci, daha az bariz olan: **kamera yüksekliği**. Senin ekran görüntünde kamera su seviyesine çok yakın, bu yüzden kadrajın %70'i su. DREDGE kamerası tekneden ~8-12m yukarıda ve 25-35° aşağı bakıyor; su kadrajın %45'ini geçmiyor.

---

## 2. DREDGE'in görsel dilinin 8 kuralı

### K1 — Aerial perspective (atmosferik perspektif) motorun ana efektidir
DREDGE'de derinlik hissini gölgeler değil **sis** taşır. Kural:
- Sis rengi = gökyüzünün ufuk rengi (±%5 tolerans). Farklıysa göz onu "kirli cam" olarak okur.
- `Exponential Squared`, density **0.008 – 0.022** arası (gündüz 0.010, sisli 0.030).
- Uzak nesneler sadece açılmaz, aynı zamanda **doygunluğunu kaybeder**. Fog rengini düşük doygunlukta seç.

### K2 — Palet dar tutulur: 1 soğuk + 1 sıcak + 1 aksan
Ekranda aynı anda 3'ten fazla renk ailesi olmaz.
- Soğuk taban: su + gökyüzü + gölgeler (mavi-gri)
- Sıcak taban: kayalar + kum + ışık alan yüzeyler (kırık beyaz-bej)
- Aksan: tekne kırmızısı, deniz feneri, fener ışığı, sonbahar ağaçları (turuncu-kırmızı) — kadrajın **%5'inden azı**

### K3 — Değer (value) hiyerarşisi mesafeyle ters çalışır
| Katman | Parlaklık | Doygunluk |
|---|---|---|
| Ön plan (0-30m) | %10-25 (koyu siluet) | Yüksek |
| Orta plan (30-120m) | %45-60 | Orta |
| Arka plan (120m+) | %75-90 (neredeyse gökyüzü) | Çok düşük |

Senin sahnende üç katman da %50 civarında — bu yüzden düz görünüyor.

### K4 — Gölgeleme bantlıdır, gradyan değil
PBR yumuşak geçişi yok. Her yüzey 2-3 basamaklı bir rampa ile aydınlanır:
- Basamak sayısı: **2** (kaya/arazi), **3** (karakter/prop)
- Gölge rengi siyah değil; ana ışığın **tamamlayıcı rengi** (sıcak güneş → mavi-mor gölge)
- Gölge/ışık eşiği: `0.42`, yumuşaklık `0.08`

### K5 — Speküler ışık neredeyse yok; sadece suda var
Arazi, kaya, ağaç: smoothness 0, speküler kapalı. Tek parlak şey **sudaki güneş yolu** ve fener camı. Bu, o "illüstrasyon" hissini veren en kritik ve en çok atlanan detay.

### K6 — Su, gökyüzünün aynasıdır ve derinliği renkle anlatır
- Sığ (0-1m): açık turkuaz-yeşil, yarı saydam
- Derin (6m+): koyu, doygunluğu düşük mavi-siyah
- Kıyı hattında **köpük bandı** (bu tek başına kaliteyi ikiye katlar)
- Yüzeyde geniş, yavaş Gerstner dalgalar — **normal map dalgacığı değil**
- Fresnel ile ufka doğru gökyüzü rengine döner (bu olmadan su "delik" gibi görünür)

### K7 — Bulutlar ve gökyüzü düz renkli, sert kenarlı
Volumetrik bulut yok. Ya skybox'ta düz bantlar ya da **sahnede duran düz geometri düzlemleri** (DREDGE bunu yapıyor — ekran görüntüsündeki bulutlar dünyada duran mesh'ler, gökyüzü dokusu değil). Gökyüzü 3 renkli bir gradyan + ufukta parlak bir bant.

### K8 — Post-processing zinciri sabittir
```
Tonemapping (Neutral)  →  Color Adjustments  →  Split Toning  →  Vignette  →  (çok az) Bloom
```
- **ACES kullanma.** ACES kontrastı ve doygunluğu ezip "sinematik gri" yapar; DREDGE'in temiz paleti için `Neutral` doğru seçim.
- Bloom threshold yüksek (1.1+), intensity düşük (0.15-0.35) — sadece güneş yolu ve fener parlasın.
- Vignette 0.20-0.30, yumuşak.
- Film grain 0.10-0.15 (opsiyonel ama "elle boyanmış" hissini artırır).

---

## 3. Sayısal palet (hex)

### Gündüz — Açık (senin 1. görselindeki koşul)
```
Gökyüzü zirve      #4B8CC4
Gökyüzü ufuk       #C7DBE6
Gökyüzü yer/haze   #B6C5CD
Güneş rengi        #FFF3DC   yoğunluk 1.55
Ambient gökyüzü    #7FA6C4
Ambient ekvator    #9BA9AE
Ambient yer        #4B535A
Sis                #C2D4DE   density 0.011
Su sığ             #4E8C93
Su derin           #12242F
Köpük              #EAF3F5
Su speküler        #FFF6E2
```

### Gün batımı
```
Zirve #26365C · Ufuk #E9A165 · Haze #C48B6E · Güneş #FF9A4A (1.25)
Ambient gök #5A6C92 · ekvator #8A7A80 · yer #3B3440
Sis #D9A277 (0.016) · Su sığ #6B7C7A · derin #17202E · köpük #F3DCC6
```

### Gece
```
Zirve #0A1424 · Ufuk #24344C · Haze #1A2434 · Ay #A8BEDC (0.30)
Ambient gök #1E2C44 · ekvator #1A2230 · yer #0E1420
Sis #16212F (0.024) · Su sığ #23404C · derin #060C14 · köpük #93A8B8
```

### Fırtına / sis
```
Zirve #6B7780 · Ufuk #A8B2B6 · Haze #98A2A6 · Güneş #D8DDDC (0.65)
Ambient gök #6E7A82 · ekvator #6A7276 · yer #3C4246
Sis #A2ACB2 (0.038) · Su sığ #47585C · derin #10181E · köpük #D6DEE0
```

---

## 4. URP proje ayarları (bunlar yanlışsa hiçbir shader kurtarmaz)

**URP Asset:**
- `Depth Texture` **açık** (su köpüğü ve derinlik rengi bunu kullanır)
- `Opaque Texture` açık (kırılma istersen)
- `HDR` açık, `MSAA` 4x
- Shadow distance **80-120m** (daha fazlası gölgeleri inceltir), Cascades 2, Soft Shadows açık
- Shadow Resolution 2048

**Lighting penceresi:**
- Environment Lighting Source: **Gradient** (Skybox değil — gradient ambient kontrolü sana verir)
- Environment Reflections: Skybox, Intensity 0.6-0.8
- Fog: açık, Exponential Squared

**Renderer:**
- SSAO renderer feature ekle: Intensity 0.5, Radius 0.35, Falloff 100 — kayaların oturmasını sağlar

**Kamera:**
- Post Processing açık, Anti-aliasing FXAA veya SMAA
- FOV 40-50 (60+ perspektifi bozar ve dioramayı öldürür)
- Far clip 800-1200

---

## 5. Sık yapılan hatalar (senin sahnende görülenler dahil)

- ❌ Sis rengini gökyüzünden bağımsız seçmek → ufuk kaybolur *(sende bu var)*
- ❌ Suda yüksek frekanslı normal map → titreşim ve "ölü su" *(sende bu var)*
- ❌ Directional light intensity'yi 1'in altında bırakıp ambient'i yükseltmek → kontrastsızlık *(sende bu var)*
- ❌ ACES tonemapping → çamurlu renkler *(muhtemelen sende bu var)*
- ❌ Ağaçlara ve kayalara smoothness vermek → plastik görünüm
- ❌ Su düzlemini yeterince bölmemek (Gerstner dalga vertex'te çalışır, düşük poly düzlemde dalga oluşmaz — **min 100x100 segment**)
- ❌ Gölge mesafesini 500m yapmak → gölgeler pikselleşir, bantlı gölgeleme çirkinleşir

---

## 6. Claude'a verilecek prompt (kopyala-yapıştır)

> Aşağıdaki bloğu olduğu gibi Claude'a ver. Kod yazarken bu kısıtlara uyması için yazıldı.

```
Unity 2022.3 URP projesinde DREDGE (Black Salt Games) oyununun görsel dilini
hedefliyorum. Sen ekranı göremiyorsun, bu yüzden estetik kararları benim
verdiğim sayısal kısıtlara göre uygula ve tüm görsel parametreleri Inspector'dan
canlı ayarlanabilir bırak ([Range] attribute, [ExecuteAlways], OnValidate).

HEDEF STİL — bağlayıcı kurallar:
1. Aerial perspective ana derinlik aracıdır. Fog rengi HER ZAMAN skybox'ın ufuk
   rengiyle senkron olmalı — ikisini tek bir kaynaktan (preset) besle. Fog modu
   Exponential Squared, density 0.008-0.040.
2. Gölgeleme bantlı (2-3 basamak), gradyan değil. Gölge rengi siyah değil, ana
   ışığın tamamlayıcısı. Işık/gölge eşiği 0.42, yumuşaklık 0.08.
3. Arazi/kaya/ağaç üzerinde speküler YOK. Tek parlak yüzey su ve ışık kaynakları.
4. Su: geniş ve yavaş Gerstner dalgaları (vertex displacement), yüksek frekanslı
   normal map YOK. Derinliğe göre sığ→derin renk lerp'i, kıyıda köpük bandı,
   fresnel ile ufukta gökyüzü rengine dönüş, tek ve geniş stilize güneş yolu.
5. Palet dar: 1 soğuk taban + 1 sıcak taban + kadrajın %5'inden az aksan rengi.
6. Post-processing zinciri: Tonemapping NEUTRAL (ACES kullanma), Color
   Adjustments (post exposure 0.0-0.4, contrast +8..+18, saturation -5..+10),
   Split Toning (gölge mavi/mor, highlight sıcak), Vignette 0.20-0.30,
   Bloom threshold >=1.1 intensity <=0.35.
7. Değer hiyerarşisi: ön plan koyu siluet, arka plan neredeyse gökyüzü rengi.

MİMARİ KISITLAR:
- Tüm atmosfer değerleri (güneş açısı/rengi/şiddeti, ambient 3 renk, fog, su
  renkleri, sky renkleri, post-processing) tek bir ScriptableObject preset'te
  toplansın; bir MonoBehaviour bunları sahneye uygulasın ve iki preset arasında
  0-1 blend edebilsin.
- Shader property'leri SRP Batcher uyumlu tek CBUFFER(UnityPerMaterial) içinde
  olsun.
- Shader'lar URP 12+ (Unity 2022.3) HLSL include yollarını kullansın.
- Editörde Play'e basmadan sonuç görülebilmeli.

YASAKLAR:
- Realistik PBR su, screen-space reflection, volumetrik bulut, ACES tonemapping,
  yüksek frekanslı detay normal map, 3'ten fazla ışık kaynağı.

Değişiklik yaparken bana hangi sayısal değeri neden değiştirdiğini tek cümleyle
söyle, "daha iyi görünsün diye" deme.
```

---

## 7. Ayar yaparken izlenecek sıra (önemli)

Sırayı bozma, yoksa birbirini maskeler:

1. **Post-processing'i kapat.** Ham görüntüyle başla.
2. Gökyüzü gradyanını ayarla (zirve/ufuk/yer).
3. Fog rengini ufuk rengine eşitle, density'yi ufuk çizgisi *belli belirsiz* görünene kadar aç.
4. Güneş açısını ayarla — **elevation 12-25°** dramatik, 45°+ düz görünür.
5. Ambient 3 rengi ayarla: gölgelerin çok koyu olmasını istemiyorsan `ambientGround`'ı yükselt, ama `ambientSky`'ı **asla** güneş şiddetinin üstüne çıkarma.
6. Suyu ayarla (önce dalga, sonra renk, en son köpük ve parıltı).
7. **En son** post-processing'i aç ve sadece ince ayar yap.

Bu paketteki `StylizedAtmosphere` bileşeni 2-6 arasını tek panelde topluyor.
