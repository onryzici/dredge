# Dredge tarzı deniz sahnesi

`Dredge ▸ 1) Deniz Sahnesini Kur` → `Assets/_Dredge/Scenes/Sea.unity` üretilir ve açılır.
Sahne **elle düzenlenmiyor**, kod üretiyor; yeniden kurmak üzerine yazar.

## Kontroller

| Tuş | İşlev |
|---|---|
| `W` / `S` | Gaz / geri |
| `A` / `D` | Dümen |
| Fare | Kamerayı çevir |
| Fare tekeri | Yakınlaş / uzaklaş (14–48 m) |
| `Esc` | İmleci serbest bırak (geri kilitlemek için sol tık) |

## Kamera

Dredge'de kamera **tekneyle birlikte dönmez**: yatay açı dünya uzayında sabit kalır,
yalnızca fareyle değişir, tekne kameranın altında döner. Oyuncu bir yöne bakarken
başka bir yöne seyredebilir; oyunun okyanusta yön bulma hissi buradan geliyor.
`BoatFollowCamera` bunu birebir uyguluyor — `autoAlign` alanı 0, yani hiç
otomatik hizalama yok. (Black Salt Games'in v1.2.0 sürüm notlarında bu davranış
açıkça geçiyor.)

Varsayılan mesafe 28 m, eğim 26°. Kamera kayalığa girecek olursa küre süpürmesiyle
içeri çekiliyor.

## Deniz

`StylizedWater.shader` — altı Gerstner dalgası vertex shader'da toplanıyor.
Dalga boyları bilerek birbirinin katı **değil** ve yönler geniş bir yelpazeye
yayılmış; uyumlu dalga boyları gözle görülür şekilde tekrar eden bir desen üretiyor.
Üstüne yavaş sürüklenen değer gürültüsü hem yüksekliğe hem normale bindiriliyor —
aynı gürültü `OceanSurface` içinde C# olarak da yazılı, yoksa tekne görünen
yüzeyden 40 cm sapardı.

Su, gökyüzünü düz bir renkle değil **skybox'ı gerçekten örnekleyerek** yansıtıyor
(`unity_SpecCube0`), bu yüzden bulutlar ve batan güneş suyun üstünde görünüyor.
Ayrı bir "güneş yolu" terimi de suyun üstündeki parlak şeridi veriyor. Dalga
parametreleri `OceanSurface` tarafından **global shader değişkeni** olarak veriliyor;
aynı sınıf aynı matematiği CPU'da da çözüyor. Bu yüzden tekne, gördüğünüz dalganın
tam olarak üstünde yüzer — ayrı bir sinüs yaklaşımı yok.

Renk sahne derinlik dokusundan geliyor: yüzeyle arkasındaki geometri arasındaki
mesafe sığ turkuazdan dip laciverte geçişi ve kıyı köpüğünü sürüyor. Bu yüzden
**URP asset'inde Depth Texture açık olmalı** — kurucu bunu otomatik açıyor.

Izgara 520 m / 300 hücre ve tekneyi hücre boyutuna yuvarlanarak takip ediyor;
dalgalar dünya konumundan hesaplandığı için kayma görünmüyor, denizin kenarına
varılamıyor.

## Ufuk

Ayrıntılı deniz ızgarası 520 m. Ötesi boş kalınca denizin kenarı **gerçek ufkun
altında** bitiyor ve bakınca her şey aşağı kaymış gibi duruyordu (12 m kamera
yüksekliğinde 260 m'lik kenar, göz hizasının 2.6° altına düşüyor).

`Ufuk Duzlemi` bu boşluğu dolduruyor: 8 km'lik düz bir levha, dalga çukurlarının
2 m altında. Kamera uzak kırpması 3000 m'ye çıkarıldı ki levha göz hizasına
kadar uzasın.

`Horizon.shader`'ın **DepthOnly geçişi yok** — bu kasıtlı. Olsaydı URP'nin
derinlik ön-geçişine girer, su shader'ı "altımda 2 metrede zemin var" diye okur
ve bütün denizi sığ renkle boyardı. Bu haliyle su, ızgaranın ötesinde sonsuz
derinlik görüyor.

## Geometri

Pakette ada/ağaç olmadığı için hepsi `MeshFactory` içinde üretiliyor, düz
gölgelemeli (her üçgen kendi normali) — referanstaki kırıklı kaya yüzeylerinin
sebebi bu. Adalar kutupsal ızgaradan (18 halka × 28 dilim); kıyı çizgisi açısal gürültüyle
bozuluyor, yükseklik profili kenarda su altına iniyor. Yükseklik %70 oranında
basamaklara çekiliyor — kayalıkların katmanlı sahanlık görüntüsü buradan geliyor. Çamlar gövde + üç koni, iki alt-mesh.

Ağaçlar adaların üstüne ışın atılarak yerleştiriliyor: su kenarına (y < 2.2) ve
dik kayaya (eğim > 44°) ağaç dikilmiyor.

## İz köpüğü ve ses

Teknenin ardındaki köpük iki parçacık sistemi: kıçta geniş iz, baş tarafta serpinti.
Emisyon **mesafeye** bağlı (`rateOverDistance`), yani köpük yalnızca tekne hareket
ederken çıkar — duruyorken tek parçacık doğmaz.

Malzeme URP'nin `Particles/Unlit`'i değil, `WakeFoam.shader`. Sebebi: URP parçacık
malzemesini koddan saydama çevirmek güvenilir değil — `_Surface`/`_Blend` alanlarını
yazmak yetmiyor, malzeme doğrulaması editörde çalışmadığı için karışım durumu opak
kalabiliyor ve parçacıklar beyaz **kare** olarak çıkıyor. Kendi shader'ımızda
karışım sabit yazılı, kaçacak yer yok. Yumuşak sönümleme de shader'ın içinde.

Doku yumuşak leke değil, **dört ayrı kıvrımlı fırça darbesinden oluşan 2×2 atlas**
(`FoamSwirl.png`); her parçacık rastgele birini seçiyor, ömrü boyunca ağır ağır
dönüyor ve yanlara açılarak V izi bırakıyor.

Emitörler gövdenin dışında (kıçın 1 m arkası, başın 0.6 m önü).

Doku ve deniz sesi (`SeaAmbience.wav`, 20 sn kusursuz döngü) prosedürel üretildi.

## Tekne

`BoatController` kinematik: gövdenin dört köşesindeki su yüksekliğinden konum ve
eğim çıkarıyor. Rigidbody + kaldırma kuvveti yerine bu seçildi çünkü sakin denizde
görsel olarak ayırt edilemiyor ama patlamıyor ve deterministik. Su hattı pivotun
**1.42 m** üstünde (paketin demo sahnesinden ölçüldü). Kayalıklara girmemek için
gövde küresiyle bir adım ileri bakılıyor.

## Sonraki adımlar

1. Balık tutma döngüsü: olta atma, mini oyun, tür/derinlik tabloları.
2. Liman ve satış: iskeleye yanaşma, envanter ızgarası (Dredge'in kutu-yerleştirme sistemi).
3. Gündüz/gece döngüsü — güneşi döndürüp `GradientSky` renklerini ve sis yoğunluğunu lerp'lemek yeterli.
4. Tekne yükseltmeleri: hız, ambar, ışık menzili.
5. Ses: dalga, motor, martı.

## Not

Eski demirci sahnesi hâlâ `Assets/_Game` ve `Assets/Daniel Mistage` altında duruyor.
Yeni sahnenin onlarla hiçbir bağı yok; istersen ikisini de silebilirsin.
