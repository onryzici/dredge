using System.Collections.Generic;
using UnityEngine;

namespace Dredge.Game
{
    public enum Habitat { Coastal, Deep, Night }

    /// <summary>Balık türü tanımı — envanterde kapladığı yer, değeri, mini-oyun zorluğu.</summary>
    public class FishSpecies
    {
        public string name;
        public Habitat habitat;
        public int width, height;     // envanter ızgarasında hücre
        public int value;             // para
        public float difficulty;      // 0–1: ibre hızı ve dilim darlığı
        public int hitsNeeded;        // kaç başarılı vuruş
        public Color color;

        public static readonly List<FishSpecies> All = new List<FishSpecies>
        {
            new FishSpecies { name = "Ringa",        habitat = Habitat.Coastal, width = 1, height = 1, value = 8,  difficulty = 0.15f, hitsNeeded = 1, color = new Color(0.72f, 0.80f, 0.86f) },
            new FishSpecies { name = "Uskumru",      habitat = Habitat.Coastal, width = 2, height = 1, value = 12, difficulty = 0.30f, hitsNeeded = 2, color = new Color(0.45f, 0.62f, 0.72f) },
            new FishSpecies { name = "Levrek",       habitat = Habitat.Coastal, width = 2, height = 1, value = 16, difficulty = 0.40f, hitsNeeded = 2, color = new Color(0.78f, 0.78f, 0.70f) },
            new FishSpecies { name = "Kefal",        habitat = Habitat.Coastal, width = 1, height = 2, value = 14, difficulty = 0.35f, hitsNeeded = 2, color = new Color(0.60f, 0.66f, 0.55f) },
            new FishSpecies { name = "Morina",       habitat = Habitat.Deep,    width = 3, height = 1, value = 30, difficulty = 0.55f, hitsNeeded = 3, color = new Color(0.55f, 0.50f, 0.42f) },
            new FishSpecies { name = "Kılıçbalığı",  habitat = Habitat.Deep,    width = 4, height = 1, value = 65, difficulty = 0.80f, hitsNeeded = 4, color = new Color(0.35f, 0.45f, 0.62f) },
            new FishSpecies { name = "Ahtapot",      habitat = Habitat.Deep,    width = 2, height = 2, value = 40, difficulty = 0.60f, hitsNeeded = 3, color = new Color(0.62f, 0.38f, 0.42f) },
            new FishSpecies { name = "Kör Yılanbalığı", habitat = Habitat.Night, width = 3, height = 1, value = 55, difficulty = 0.75f, hitsNeeded = 3, color = new Color(0.30f, 0.32f, 0.36f) },
            new FishSpecies { name = "Kanlı Ringa",  habitat = Habitat.Night,   width = 1, height = 1, value = 28, difficulty = 0.45f, hitsNeeded = 2, color = new Color(0.55f, 0.12f, 0.14f) },
        };

        public static FishSpecies Pick(Habitat habitat, System.Random rng)
        {
            var pool = All.FindAll(f => f.habitat == habitat);
            if (pool.Count == 0) pool = All;
            return pool[rng.Next(pool.Count)];
        }
    }

    /// <summary>Yakalanmış tek balık (envanter öğesi).</summary>
    [System.Serializable]
    public class FishItem
    {
        public string speciesName;
        public int width, height;
        public int value;
        public int gridX = -1, gridY = -1;   // envanterdeki yeri (-1: yerleşmedi)
        public bool aberration;              // gece türü: satışta prim, akıl kaybı

        public FishItem(FishSpecies s)
        {
            speciesName = s.name; width = s.width; height = s.height; value = s.value;
            aberration = s.habitat == Habitat.Night;
        }

        public Color Color
        {
            get { var s = FishSpecies.All.Find(f => f.name == speciesName); return s != null ? s.color : Color.gray; }
        }
    }
}
