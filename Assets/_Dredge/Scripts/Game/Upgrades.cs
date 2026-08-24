namespace Dredge.Game
{
    /// <summary>Satın alınan tekne yükseltmeleri (BoatController, PanicSystem, Inventory bunları okur).</summary>
    [System.Serializable]
    public class Upgrades
    {
        public bool engine;    // hız +35%
        public bool cargo;     // 8×5 ambar
        public bool lantern;   // gece paniği yavaşlar, fener daha parlak
        public bool hull;      // gövde 160
    }
}
