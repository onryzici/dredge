using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dredge.Game
{
    public enum QuestState { Hidden, Active, Done }

    [System.Serializable]
    public class Quest
    {
        public string id, title, description, reward;
        public QuestState state;
        public System.Func<GameSession, bool> check;    // tamamlama koşulu
        public System.Action<GameSession> onComplete;
    }

    /// <summary>
    /// Hikâye: açılış anlatımı, Kuzey Marlin limanındaki iki kişi (Liman Reisi, Koleksiyoncu),
    /// görev zinciri, günlük (J) ve bir gece olayı (hayalet gemi). Diyaloglar klavyeyle
    /// seçilir (1/2/3), ESC kapatır.
    /// </summary>
    public class StoryManager : MonoBehaviour
    {
        public GhostShip ghostShip;
        public Transform wreckSite;

        // durum
        public bool introDone;
        public bool journalOpen;
        public readonly List<Quest> quests = new List<Quest>();
        public readonly List<string> journal = new List<string>();
        public bool hasRustyKey, metCollector, wreckVisited, ghostSeen;

        // intro
        int introPage;
        float introTimer;
        static readonly string[] IntroPages =
        {
            "Kuzey Marlin.\n\nHaritanın kıyısında, sisin bitmediği bir liman. Bir tekne, biraz borç ve gitmen söylenmiş bir isim: <b>Liman Reisi</b>.",
            "Buradaki balıklar tuhaf. Ağlar bazen boş, bazen fazlasıyla dolu gelir. Gece açıkta kalanlar sabah farklı konuşur.\n\nGün ışığında av. Karanlıkta liman.",
            "Kontroller — W/S gaz, A/D dümen, fare kamera.\n[E] balık tut / konuş / sat   ·   [TAB] ambar   ·   [J] günlük\n\nHer şeye rağmen: denize açıl."
        };

        // diyalog
        bool dialogOpen;
        string dialogSpeaker;
        string dialogText;
        readonly List<(string label, System.Action act)> choices = new List<(string, System.Action)>();
        int selected;

        GameSession S => GameSession.Instance;

        void Start()
        {
            BuildQuests();
            journal.Add("Kuzey Marlin'e vardım. Liman Reisi'yle konuşmalıyım.");
            S?.SetMode(GameMode.Docked);   // intro sırasında sürüş kilitli
            if (S != null) S.clock.running = false;
        }

        void BuildQuests()
        {
            quests.Add(new Quest
            {
                id = "first_catch", title = "İlk Ağ",
                description = "Liman Reisi'ne 3 kıyı balığı getir (Ringa, Uskumru, Levrek ya da Kefal).",
                reward = "60₺ ve Reis'in güveni",
                state = QuestState.Hidden,
                check = s => CountCoastal(s) >= 3,
                onComplete = s => { s.money += 60; journal.Add("Reis balıkları saydı, homurdandı, parayı verdi. 'Batıdaki batığı gördün mü?' dedi."); }
            });
            quests.Add(new Quest
            {
                id = "wreck", title = "Batık",
                description = "Batı Adası açıklarındaki batık gemiyi bul ve yanına yanaş.",
                reward = "Paslı bir anahtar",
                state = QuestState.Hidden,
                check = s => wreckVisited,
                onComplete = s => { hasRustyKey = true; journal.Add("Batığın güvertesinde paslı bir anahtar buldum. Kilit yok; ama anahtar var."); }
            });
            quests.Add(new Quest
            {
                id = "collector", title = "Tuhaf Bir Talep",
                description = "Koleksiyoncu'ya gece yakalanmış bir 'aberasyon' (Kanlı Ringa ya da Kör Yılanbalığı) getir.",
                reward = "Fener yükseltmesi (gece paniği yavaşlar)",
                state = QuestState.Hidden,
                check = s => CountAberrations(s) >= 1,
                onComplete = s =>
                {
                    RemoveOneAberration(s);
                    s.upgrades.lantern = true;
                    journal.Add("Koleksiyoncu balığa uzun uzun baktı. 'Bunlar artıyor,' dedi. 'Fenerini yak. Bakma onlara.'");
                }
            });
            quests.Add(new Quest
            {
                id = "ghost", title = "Sisin İçinde",
                description = "Gece açıkta, sisin içinde bir şey gördün. Bunu Liman Reisi'ne anlat.",
                reward = "?",
                state = QuestState.Hidden,
                check = s => ghostSeen,
                onComplete = s => { journal.Add("Reis dinledi, sonra sustu. 'Herkes görür,' dedi. 'Bir kere.'"); }
            });
        }

        static int CountCoastal(GameSession s)
        {
            int n = 0;
            foreach (var f in s.inventory.items)
                if (f.speciesName == "Ringa" || f.speciesName == "Uskumru" || f.speciesName == "Levrek" || f.speciesName == "Kefal") n++;
            return n;
        }
        static int CountAberrations(GameSession s) { int n = 0; foreach (var f in s.inventory.items) if (f.aberration) n++; return n; }
        static void RemoveOneAberration(GameSession s)
        {
            var it = s.inventory.items.Find(f => f.aberration);
            if (it == null) return;
            var keep = new List<FishItem>(s.inventory.items); keep.Remove(it);
            s.inventory.Clear();
            foreach (var f in keep) { f.gridX = -1; f.gridY = -1; s.inventory.TryAdd(f); }
        }
        static void RemoveCoastal(GameSession s, int count)
        {
            var keep = new List<FishItem>(s.inventory.items);
            int removed = 0;
            keep.RemoveAll(f => removed < count && (f.speciesName == "Ringa" || f.speciesName == "Uskumru" || f.speciesName == "Levrek" || f.speciesName == "Kefal") && ++removed > 0);
            s.inventory.Clear();
            foreach (var f in keep) { f.gridX = -1; f.gridY = -1; s.inventory.TryAdd(f); }
        }

        public Quest GetQuest(string id) => quests.Find(q => q.id == id);
        public bool AnyUIOpen => !introDone || dialogOpen || journalOpen;

        // ------------------------------------------------------------ update

        void Update()
        {
            var s = S; var kb = Keyboard.current;
            if (s == null || kb == null) return;

            if (!introDone)
            {
                introTimer += Time.deltaTime;
                if (introTimer > 0.4f && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    introPage++; introTimer = 0f;
                    if (introPage >= IntroPages.Length)
                    {
                        introDone = true;
                        s.SetMode(GameMode.Sailing);
                        s.clock.running = true;
                        s.Notify("Liman Reisi iskelede bekliyor — yanına git ve [E]");
                    }
                }
                return;
            }

            if (dialogOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame) CloseDialog();
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) selected = (selected - 1 + choices.Count) % Mathf.Max(1, choices.Count);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) selected = (selected + 1) % Mathf.Max(1, choices.Count);
                for (int i = 0; i < choices.Count && i < 4; i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { selected = i; Pick(); }
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Pick();
                return;
            }

            if (kb.jKey.wasPressedThisFrame)
            {
                journalOpen = !journalOpen;
                s.SetMode(journalOpen ? GameMode.Inventory : GameMode.Sailing);
            }
            if (journalOpen) return;

            // Görev tamamlama kontrolü (limanda konuşarak teslim edilir; burada sadece "hazır" işareti)
            // Batık ziyareti
            if (wreckSite != null && !wreckVisited && Vector3.Distance(s.boat.transform.position, wreckSite.position) < 22f)
            {
                var q = GetQuest("wreck");
                if (q.state == QuestState.Active)
                {
                    wreckVisited = true;
                    s.panic?.OnAberration();
                    s.Notify("Batığın güvertesinde bir şey parlıyor... (günlüğe yazıldı)", 5f);
                    Complete(q);
                }
            }
            // Hayalet gemi görüldü mü
            if (ghostShip != null && ghostShip.Visible && !ghostSeen && Vector3.Distance(s.boat.transform.position, ghostShip.transform.position) < 120f)
            {
                ghostSeen = true;
                var q = GetQuest("ghost"); q.state = QuestState.Active;
                journal.Add("Gece, sisin içinde yeşil fenerli bir gemi gördüm. Yelkenleri vardı ama rüzgâr yoktu.");
                s.Notify("Sisin içinde bir şey var...", 5f);
                s.panic.Panic01 = Mathf.Min(1f, s.panic.Panic01 + 0.3f);
            }
        }

        void Complete(Quest q)
        {
            q.state = QuestState.Done;
            q.onComplete?.Invoke(S);
            S.Notify($"Görev tamamlandı: {q.title}  ·  {q.reward}", 5f);
        }

        // ------------------------------------------------------------ NPC diyalogları

        public void TalkToHarbormaster()
        {
            var s = S;
            var q1 = GetQuest("first_catch"); var q2 = GetQuest("wreck"); var q4 = GetQuest("ghost");
            choices.Clear(); selected = 0;
            dialogSpeaker = "LİMAN REİSİ";

            if (q1.state == QuestState.Hidden)
            {
                dialogText = "Sen misin yeni gelen? Tekne çürük, borç taze. Bana üç kıyı balığı getir; para öderim, sonra konuşuruz.\n\nHalkaları gör, yavaşla, [E]. İbre yeşildeyken bas.";
                choices.Add(("Tamam, üç balık.", () => { q1.state = QuestState.Active; journal.Add("Reis: 3 kıyı balığı istiyor."); CloseDialog(); }));
            }
            else if (q1.state == QuestState.Active)
            {
                if (q1.check(s))
                {
                    dialogText = "Bak sen. Üç balık. Fena değil, çürük tekne için.";
                    choices.Add(("Balıkları ver.", () => { RemoveCoastal(s, 3); Complete(q1); q2.state = QuestState.Active; dialogText = "Batı Adası açıklarında bir batık var. Kimse yanaşmaz. Sen yanaş. Ne bulursan bana getir."; choices.Clear(); choices.Add(("Neden ben?", () => { dialogText = "Çünkü yenisin. Henüz korkmuyorsun."; choices.Clear(); choices.Add(("...", CloseDialog)); })); choices.Add(("Giderim.", CloseDialog)); }));
                }
                else dialogText = $"Üç dedim. Sende {CountCoastal(s)} var. Halkalar kıyıda, git.";
                choices.Add(("Tamam.", CloseDialog));
            }
            else if (q2.state == QuestState.Active)
            {
                dialogText = "Batık batıda. Gündüz git. Gece gitme.";
                choices.Add(("Anladım.", CloseDialog));
            }
            else if (q4.state == QuestState.Active)
            {
                dialogText = "Yeşil fener. Biliyorum. Herkes bir kere görür. İkinci kez görenleri kimse görmez.\n\nGeceleri limanda kal.";
                choices.Add(("...", () => { Complete(q4); CloseDialog(); }));
            }
            else
            {
                dialogText = hasRustyKey ? "Anahtar sende kalsın. Kilidi bulan sen olacaksın.\n\nBalık var mı? Sat, git, dinlen."
                                         : "Balık var mı? Sat, git, dinlen. Gece açıkta kalma.";
                choices.Add(("Yükseltmeler", OpenUpgrades));
                choices.Add(("Hoşça kal.", CloseDialog));
            }
            if (!choices.Exists(c => c.label == "Yükseltmeler") && q1.state != QuestState.Hidden) choices.Insert(choices.Count - 1, ("Yükseltmeler", OpenUpgrades));
            OpenDialog();
        }

        public void TalkToCollector()
        {
            var s = S; var q3 = GetQuest("collector");
            choices.Clear(); selected = 0;
            dialogSpeaker = "KOLEKSİYONCU";
            if (!metCollector)
            {
                metCollector = true;
                dialogText = "Merhaba. Sen... denizden geldin, değil mi? Elbette. Herkes gelir.\n\nBalıkların arasında bazen tuhaf olanlar çıkar. Gözleri fazla. Ya da hiç yok. Onları bana getir. İyi öderim.";
                choices.Add(("Tuhaf olanlar mı?", () => { dialogText = "Gece daha çok çıkarlar. Kırmızı halkaların altında. Onlara uzun bakma.\n\nİlkini getirene fenerini değiştiririm. Işık... yardımcı olur."; choices.Clear(); choices.Add(("Kabul.", () => { q3.state = QuestState.Active; journal.Add("Koleksiyoncu: gece 'aberasyon' istiyor. Kırmızı halkalar."); CloseDialog(); })); }));
            }
            else if (q3.state == QuestState.Active)
            {
                if (q3.check(s))
                {
                    dialogText = "Evet. Evet, bu. Bak nasıl da... neyse.";
                    choices.Add(("Balığı ver.", () => { Complete(q3); dialogText = "Fenerin artık daha güçlü. Gece sisi bundan hoşlanmaz. Sen de fazla hoşlanma."; choices.Clear(); choices.Add(("...", CloseDialog)); }));
                }
                else dialogText = "Henüz yok mu? Gece. Kırmızı halkalar. Sabırlıyım.";
                choices.Add(("Sonra.", CloseDialog));
            }
            else
            {
                int ab = CountAberrations(s);
                dialogText = ab > 0 ? $"{ab} tane var sende. Tanesine 2 kat öderim." : "Yeni bir şey görürsen... bana getir.";
                if (ab > 0) choices.Add(("Hepsini sat (2×).", () => { int sum = 0; foreach (var f in s.inventory.items) if (f.aberration) sum += f.value * 2; var keep = s.inventory.items.FindAll(f => !f.aberration); s.inventory.Clear(); foreach (var f in keep) { f.gridX = -1; f.gridY = -1; s.inventory.TryAdd(f); } s.money += sum; s.Notify($"Koleksiyoncu ödedi: +{sum}₺"); CloseDialog(); }));
                choices.Add(("Hoşça kal.", CloseDialog));
            }
            OpenDialog();
        }

        /// <summary>Limanda [E]: tek menü — kişiler ve işlemler.</summary>
        public void OpenHarborMenu()
        {
            var s = S;
            dialogSpeaker = "KUZEY MARLİN LİMANI";
            dialogText = $"Ambar: {s.inventory.Count} balık ({s.inventory.TotalValue()}₺)   ·   Gövde {Mathf.RoundToInt(s.damage.hull)}/{Mathf.RoundToInt(s.damage.maxHull)}   ·   Saat {s.clock.TimeString}";
            choices.Clear(); selected = 0;
            choices.Add(("Liman Reisi ile konuş", TalkToHarbormaster));
            choices.Add(("Koleksiyoncu ile konuş", TalkToCollector));
            choices.Add(($"Balıkları sat (+{s.inventory.TotalValue()}₺)", () => { s.homeHarbor.SellAll(s); OpenHarborMenu(); }));
            choices.Add(("Onar / Dinlen / Yükselt", () =>
            {
                dialogSpeaker = "TERSANE";
                dialogText = "Ne yapalım?";
                choices.Clear(); selected = 0;
                int missing = Mathf.RoundToInt(s.damage.maxHull - s.damage.hull);
                choices.Add(($"Gövdeyi onar ({missing * s.homeHarbor.repairCostPerPoint}₺)", () => { s.homeHarbor.Repair(s); OpenHarborMenu(); }));
                choices.Add(("Sabaha kadar dinlen", () => { s.homeHarbor.Rest(s); CloseDialog(); }));
                choices.Add(("Yükseltmeler", OpenUpgrades));
                choices.Add(("Geri", OpenHarborMenu));
            }));
            choices.Add(("Denize dön", CloseDialog));
            OpenDialog();
        }

        void OpenUpgrades()
        {
            var s = S; var up = s.upgrades;
            dialogSpeaker = "YÜKSELTMELER";
            dialogText = $"Paran: {s.money}₺";
            choices.Clear(); selected = 0;
            void Item(string name, int cost, bool owned, System.Action buy, string desc)
            {
                if (owned) choices.Add(($"{name} — alındı", () => { }));
                else choices.Add(($"{name} — {cost}₺  ({desc})", () =>
                {
                    if (s.money < cost) { s.Notify("Paran yetmiyor."); return; }
                    s.money -= cost; buy(); s.Notify($"{name} alındı."); OpenUpgrades();
                }));
            }
            Item("Motor", 150, up.engine, () => up.engine = true, "hız +35%");
            Item("Büyük ambar", 200, up.cargo, () => { up.cargo = true; s.inventory.Expand(8, 5); }, "8×5 ızgara");
            Item("Fener", 120, up.lantern, () => up.lantern = true, "gece paniği yavaş");
            Item("Takviye gövde", 180, up.hull, () => { up.hull = true; s.damage.maxHull = 160f; s.damage.hull = 160f; }, "gövde 160");
            choices.Add(("Geri", CloseDialog));
            OpenDialog();
        }

        void OpenDialog() { dialogOpen = true; S.SetMode(GameMode.Docked); }
        void CloseDialog() { dialogOpen = false; S.SetMode(GameMode.Sailing); }
        void Pick() { if (selected >= 0 && selected < choices.Count) choices[selected].act?.Invoke(); }

        // ------------------------------------------------------------ çizim

        void OnGUI()
        {
            UISkin.Ensure();
            float u = UISkin.U, W = Screen.width, H = Screen.height;

            if (!introDone)
            {
                UISkin.FullscreenTint(new Color(0.01f, 0.02f, 0.04f, 0.94f));
                var r = new Rect(W / 2 - 440 * u, H / 2 - 170 * u, 880 * u, 340 * u);
                UISkin.Panel(r, 1f, true);
                GUI.Label(new Rect(r.x + 40 * u, r.y + 24 * u, r.width - 80 * u, 44 * u), introPage == 0 ? "KUZEY MARLİN" : introPage == 1 ? "SİS" : "DENİZE", UISkin.Title);
                UISkin.DividerLine(new Rect(r.x + r.width / 2 - 120 * u, r.y + 70 * u, 240 * u, 24 * u));
                GUI.Label(new Rect(r.x + 60 * u, r.y + 100 * u, r.width - 120 * u, 190 * u), IntroPages[Mathf.Min(introPage, IntroPages.Length - 1)], UISkin.Center);
                GUI.Label(new Rect(r.x, r.yMax - 40 * u, r.width, 28 * u), $"[SPACE] devam   ·   {introPage + 1}/{IntroPages.Length}", new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            if (dialogOpen)
            {
                var r = new Rect(W / 2 - 460 * u, H - 330 * u, 920 * u, 300 * u);
                UISkin.Panel(r, 1f, true);
                GUI.Label(new Rect(r.x + 36 * u, r.y + 18 * u, 500 * u, 30 * u), dialogSpeaker, UISkin.Header);
                GUI.Label(new Rect(r.x + 36 * u, r.y + 52 * u, r.width - 72 * u, 110 * u), dialogText, UISkin.Body);
                float y = r.y + 168 * u;
                for (int i = 0; i < choices.Count; i++)
                {
                    UISkin.Choice(new Rect(r.x + 30 * u, y, r.width - 60 * u, 28 * u), choices[i].label, i == selected, (i + 1).ToString());
                    y += 30 * u;
                }
                GUI.Label(new Rect(r.x, r.yMax - 26 * u, r.width, 20 * u), "↑↓ / 1-4 seç   ·   ENTER onayla   ·   ESC kapat", new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter });
            }

            if (journalOpen)
            {
                var r = new Rect(W / 2 - 480 * u, H / 2 - 300 * u, 960 * u, 600 * u);
                UISkin.Panel(r, 1f, true);
                GUI.Label(new Rect(r.x, r.y + 18 * u, r.width, 40 * u), "GÜNLÜK", UISkin.Title);
                // sol: görevler
                float lx = r.x + 40 * u, ly = r.y + 80 * u;
                GUI.Label(new Rect(lx, ly, 400 * u, 28 * u), "GÖREVLER", UISkin.Header); ly += 34 * u;
                foreach (var q in quests)
                {
                    if (q.state == QuestState.Hidden) continue;
                    string mark = q.state == QuestState.Done ? "<color=#73ff99>✓</color>" : "<color=#ffcc73>•</color>";
                    GUI.Label(new Rect(lx, ly, 420 * u, 26 * u), $"{mark} <b>{q.title}</b>", UISkin.Body); ly += 26 * u;
                    GUI.Label(new Rect(lx + 18 * u, ly, 400 * u, 44 * u), q.description, UISkin.Small); ly += 46 * u;
                    GUI.Label(new Rect(lx + 18 * u, ly, 400 * u, 22 * u), "Ödül: " + q.reward, UISkin.Small); ly += 30 * u;
                }
                // sağ: notlar
                float rx = r.x + 500 * u, ry = r.y + 80 * u;
                GUI.Label(new Rect(rx, ry, 400 * u, 28 * u), "NOTLAR", UISkin.Header); ry += 34 * u;
                for (int i = journal.Count - 1; i >= 0 && ry < r.yMax - 60 * u; i--)
                {
                    GUI.Label(new Rect(rx, ry, 420 * u, 60 * u), "— " + journal[i], UISkin.Small); ry += 62 * u;
                }
                GUI.Label(new Rect(r.x, r.yMax - 36 * u, r.width, 24 * u), "[J] kapat", new GUIStyle(UISkin.Small) { alignment = TextAnchor.MiddleCenter });
            }
        }
    }
}
