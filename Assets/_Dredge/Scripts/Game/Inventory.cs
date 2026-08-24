using System.Collections.Generic;
using UnityEngine;

namespace Dredge.Game
{
    /// <summary>
    /// Ambar ızgarası (DREDGE'deki gibi): her balık width×height hücre kaplar,
    /// sığmıyorsa yakalanamaz. Otomatik yerleştirme (ilk boş yer, gerekirse döndür).
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public int columns = 6;
        public int rows = 4;
        public List<FishItem> items = new List<FishItem>();

        bool[,] occ;

        void Awake() { Rebuild(); }

        void Rebuild()
        {
            occ = new bool[columns, rows];
            foreach (var it in items)
                if (it.gridX >= 0) Mark(it, true);
        }

        void Mark(FishItem it, bool v)
        {
            for (int x = 0; x < it.width; x++)
            for (int y = 0; y < it.height; y++)
            {
                int gx = it.gridX + x, gy = it.gridY + y;
                if (gx < columns && gy < rows) occ[gx, gy] = v;
            }
        }

        bool Fits(int x, int y, int w, int h)
        {
            if (x + w > columns || y + h > rows) return false;
            for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
                if (occ[x + i, y + j]) return false;
            return true;
        }

        /// <summary>Balığı yerleştirmeyi dener; sığmazsa false.</summary>
        public bool TryAdd(FishItem it)
        {
            if (occ == null) Rebuild();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    if (Fits(x, y, it.width, it.height))
                    {
                        it.gridX = x; it.gridY = y;
                        Mark(it, true);
                        items.Add(it);
                        return true;
                    }
                }
                // Döndürüp bir daha dene
                (it.width, it.height) = (it.height, it.width);
            }
            (it.width, it.height) = (it.height, it.width);
            return false;
        }

        public bool WouldFit(int w, int h)
        {
            if (occ == null) Rebuild();
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                if (Fits(x, y, w, h) || Fits(x, y, h, w)) return true;
            return false;
        }

        public int TotalValue()
        {
            int sum = 0;
            foreach (var it in items) sum += it.value;
            return sum;
        }

        public int Count => items.Count;

        public void Clear()
        {
            items.Clear();
            Rebuild();
        }

        public bool IsOccupied(int x, int y) => occ != null && occ[x, y];

        /// <summary>Ambarı büyüt (yükseltme); mevcut balıklar yeniden yerleşir.</summary>
        public void Expand(int cols, int rows)
        {
            var keep = new List<FishItem>(items);
            columns = Mathf.Max(columns, cols); this.rows = Mathf.Max(this.rows, rows);
            items.Clear(); Rebuild();
            foreach (var f in keep) { f.gridX = -1; f.gridY = -1; TryAdd(f); }
        }
    }
}
