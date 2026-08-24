# Dredge Prototype

Dredge tarzı bir balıkçı teknesi / deniz prototipi. Unity **6000.5.6f1**, URP.

## Açılış

1. Projeyi Unity 6000.5.6f1 ile aç (ilk açılış `Library/` üreteceği için biraz sürer).
2. Sahne: `Assets/_Dredge/Scenes/Sea.unity`
3. Sahneyi baştan üretmek istersen: menüden **`Dredge ▸ 1) Deniz Sahnesini Kur`**
   (sahneyi üzerine yazar).

## Kontroller

| Tuş | İşlev |
|---|---|
| `W` / `S` | Gaz / geri |
| `A` / `D` | Dümen |
| Fare | Kamerayı çevir |
| Fare tekeri | Yakınlaş / uzaklaş |
| `Esc` | İmleci bırak (geri kilitlemek için sol tık) |

## Neler var

- **Stilize deniz** — altı Gerstner dalgası vertex shader'da, derinliğe göre renk
  (sahne derinlik dokusundan), kıyı köpüğü, gökyüzü yansıması, güneş yolu.
  Aynı dalga matematiği CPU'da da çözülü, tekne gördüğü dalganın üstünde yüzüyor.
- **Prosedürel adalar ve çamlar** — düz gölgelemeli, teraslı kayalıklar.
- **Alacakaranlık gökyüzü** — gradyan + iki katmanlı düşük poligon bulut, kendi shader'ı.
- **Tekne** — kinematik yüzdürme, dört köşeden dalga örneklemesi, kayalık çarpışması.
- **İz köpüğü** — mesafeye bağlı parçacıklar, kıvrımlı fırça darbesi atlası.
- **Deniz sesi** — 20 sn kusursuz döngü, prosedürel üretildi.

Ayrıntılı teknik notlar: [`Assets/_Dredge/README.md`](Assets/_Dredge/README.md)

## Notlar

- `Assets/Low-Poly 3D Boat Model` üçüncü taraf bir asset; projenin geri kalanı
  (shader'lar, script'ler, üretilen mesh/doku/ses) bu depoya özgü.
- `Assets/_Dredge/Generated` ve `Assets/_Dredge/Materials` içindekiler sahne kurucusu
  tarafından üretiliyor; elle düzenlemek yerine kurucuyu tekrar çalıştır.
