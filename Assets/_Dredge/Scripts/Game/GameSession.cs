using UnityEngine;

namespace Dredge.Game
{
    public enum GameMode { Sailing, Fishing, Docked, Inventory }

    /// <summary>
    /// Oyun durumunun tek sahibi: para, mod, sistem referansları. Sahnede bir tane olur.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public int money = 0;
        public GameMode mode = GameMode.Sailing;

        [Header("Referanslar (kurucu doldurur, boşsa sahnede aranır)")]
        public BoatController boat;
        public GameClock clock;
        public Inventory inventory;
        public PanicSystem panic;
        public BoatDamage damage;
        public Harbor homeHarbor;
        public StoryManager story;
        public Upgrades upgrades = new Upgrades();

        [Header("Bildirim")]
        public string notice;
        public float noticeUntil;

        void Awake()
        {
            Instance = this;
            if (boat == null) boat = FindAnyObjectByType<BoatController>();
            if (clock == null) clock = GetComponent<GameClock>();
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (panic == null) panic = GetComponent<PanicSystem>();
            if (damage == null) damage = FindAnyObjectByType<BoatDamage>();
            if (homeHarbor == null) homeHarbor = FindAnyObjectByType<Harbor>();
            if (story == null) story = GetComponent<StoryManager>();
        }

        public void SetMode(GameMode m)
        {
            mode = m;
            if (boat != null) boat.InputLocked = m != GameMode.Sailing;
        }

        public void Notify(string text, float seconds = 3f)
        {
            notice = text;
            noticeUntil = Time.time + seconds;
        }

        public bool HasNotice => Time.time < noticeUntil && !string.IsNullOrEmpty(notice);
    }
}
