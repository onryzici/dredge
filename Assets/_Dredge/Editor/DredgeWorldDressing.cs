using Dredge.Game;
using DredgeLook;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dredge.EditorTools
{
    /// <summary>
    /// Dünyayı "giydirir" ve oyun sistemlerini kurar: Kenney Pirate Kit ile liman
    /// (iskele, depo, sandıklar, bayrak), deniz feneri, batık gemi, hayalet gemi,
    /// şamandıralar, martılar; ardından GameSession + hikâye + görevler.
    /// </summary>
    public static class DredgeWorldDressing
    {
        const string Kit = "Assets/ThirdParty/Kenney/PirateKit/";
        const float Waterline = 1.42f;

        // ------------------------------------------------------------ yardımcı

        /// <summary>Kenney FBX'ini yerleştirir; en büyük yatay boyutu targetSize metre olacak şekilde ölçekler.</summary>
        static GameObject Place(string model, Transform parent, Vector3 pos, float yaw, float targetSize, string name = null, bool bottomAlign = false)
            => PlaceFrom(Kit, model, parent, pos, yaw, targetSize, name, bottomAlign);

        public const string Nature = "Assets/ThirdParty/Kenney/NatureKit/";
        public const string WaterKit = "Assets/ThirdParty/Kenney/Watercraft/";
        public const string Chars = "Assets/ThirdParty/Kenney/Characters/";

        /// <summary>Herhangi bir Kenney klasöründen model yerleştirir; bottomAlign ile alt kenarı pos.y'ye oturtur.</summary>
        public static GameObject PlaceFrom(string dir, string model, Transform parent, Vector3 pos, float yaw, float targetSize, string name = null, bool bottomAlign = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + model + ".fbx");
            if (prefab == null) { Debug.LogWarning("[Dredge] Kenney modeli yok: " + model); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name ?? model;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;

            var b = new Bounds(pos, Vector3.zero); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            float size = Mathf.Max(b.size.x, b.size.z, 0.01f);
            float k = targetSize / size;
            go.transform.localScale = Vector3.one * k;
            if (bottomAlign)
            {
                // Ölçek sonrası alt kenar: bounds.min.y pivot'a göre k ile ölçeklenir
                float bottom = (b.min.y - pos.y) * k;
                go.transform.position = new Vector3(pos.x, pos.y - bottom, pos.z);
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        static Light AddLight(Transform parent, string name, Vector3 localPos, LightType type, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = type; l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // --------------------------------------------------------------- liman

        public static Harbor BuildHarbor(Transform worldRoot)
        {
            var islandPos = new Vector3(62f, 0f, 80f);
            var toCenter = (Vector3.zero - islandPos).normalized;
            var pierPos = islandPos + toCenter * (33f + 1f);
            var pier = new GameObject("Liman").transform;
            pier.SetParent(worldRoot, false);
            pier.position = pierPos;
            pier.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            float yaw = pier.eulerAngles.y;

            // İskele: kıyıdan denize doğru 2 modül + kısa uç
            Place("structure-platform-dock", pier, pier.TransformPoint(new Vector3(0f, 0.2f, 4f)), yaw, 8f, "Iskele A");
            Place("structure-platform-dock", pier, pier.TransformPoint(new Vector3(0f, 0.2f, 12f)), yaw, 8f, "Iskele B");
            Place("structure-platform-dock-small", pier, pier.TransformPoint(new Vector3(0f, 0.2f, 18.5f)), yaw, 5f, "Iskele Ucu");
            // Depo + çatı (Liman Reisi'nin kulübesi), sandıklar, fıçılar, bayrak
            Place("structure", pier, pier.TransformPoint(new Vector3(-3.5f, 0.6f, -1f)), yaw + 90f, 6.5f, "Depo");
            Place("structure-roof", pier, pier.TransformPoint(new Vector3(-3.5f, 4.2f, -1f)), yaw + 90f, 7.2f, "Depo Cati");
            Place("crate", pier, pier.TransformPoint(new Vector3(2.6f, 0.8f, 6f)), yaw + 20f, 1.4f, "Sandik");
            Place("crate-bottles", pier, pier.TransformPoint(new Vector3(2.6f, 0.8f, 7.6f)), yaw - 10f, 1.3f, "Sandik 2");
            Place("barrel", pier, pier.TransformPoint(new Vector3(-2.7f, 0.8f, 9f)), yaw, 1.1f, "Fici");
            Place("barrel", pier, pier.TransformPoint(new Vector3(-2.7f, 0.8f, 10.3f)), yaw + 40f, 1.1f, "Fici 2");
            Place("chest", pier, pier.TransformPoint(new Vector3(2.4f, 0.8f, 15f)), yaw - 90f, 1.4f, "Koleksiyoncu Sandigi");
            Place("flag-high", pier, pier.TransformPoint(new Vector3(-2.8f, 0.6f, 17.5f)), yaw, 1.2f, "Bayrak");
            Place("boat-row-small", pier, pier.TransformPoint(new Vector3(4.5f, 0.0f, 2f)), yaw + 15f, 3.2f, "Kayik");

            // NPC işaretleri (ışıklı direkler): Reis depoda, Koleksiyoncu iskele ucunda
            var reisLamp = AddLight(pier, "Liman Feneri", new Vector3(-3.5f, 5.2f, -1f), LightType.Point, new Color(1f, 0.78f, 0.5f), 7f, 28f);
            reisLamp.gameObject.AddComponent<LanternFlicker>();
            AddLight(pier, "Uc Feneri", new Vector3(0f, 3.2f, 18.5f), LightType.Point, new Color(0.6f, 1f, 0.75f), 4f, 16f);

            var harbor = pier.gameObject.AddComponent<Harbor>();
            harbor.interactRadius = 24f;
            var respawn = new GameObject("Respawn").transform;
            respawn.SetParent(pier, false);
            respawn.localPosition = new Vector3(9f, 0f, 16f);
            respawn.localRotation = Quaternion.identity;
            harbor.respawnPoint = respawn;

            // Liman girişini temizle
            foreach (var rock in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (rock == null) continue;
                var t = rock.transform;
                while (t != null && t.name != "Rock") t = t.parent;   // Kenney kayalarında collider alt objede
                if (t != null && Vector3.Distance(t.position, pier.position) < 28f) Object.DestroyImmediate(t.gameObject);
            }
            Physics.SyncTransforms();

            // Şamandıralar (Kenney Watercraft): liman yaklaşma hattı
            for (int i = 0; i < 3; i++)
            {
                var p = pier.TransformPoint(new Vector3(i % 2 == 0 ? -9f : 9f, 0f, 24f + i * 10f));
                var buoy = PlaceFrom(WaterKit, i % 2 == 0 ? "buoy" : "buoy-flag", worldRoot, p, i * 40f, 1.6f, "Samandira", bottomAlign: false);
                if (buoy == null) continue;
                foreach (var c in buoy.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var lamp = AddLight(buoy.transform, "Isik", new Vector3(0f, 1.8f, 0f), LightType.Point, Color.red, 1.2f, 7f);
                var b = buoy.AddComponent<Buoy>(); b.lamp = lamp; b.color = i % 2 == 0 ? new Color(1f, 0.35f, 0.25f) : new Color(0.4f, 1f, 0.6f); b.blinkPeriod = 2f + i * 0.7f;
            }

            // Limanda bağlı tekneler ve yük
            var moored = PlaceFrom(WaterKit, "ship-small", worldRoot, pier.TransformPoint(new Vector3(-7.5f, 0f, 9f)), yaw + 90f, 9f, "Bagli Tekne");
            if (moored != null) { foreach (var c in moored.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c); moored.AddComponent<Buoy>(); }
            PlaceFrom(WaterKit, "cargo-pile-a", pier, pier.TransformPoint(new Vector3(-2.4f, 0.8f, 13f)), yaw + 15f, 1.6f, "Yuk");
            PlaceFrom(Nature, "sign", pier, pier.TransformPoint(new Vector3(2.4f, 0.8f, 3f)), yaw + 180f, 0.9f, "Tabela", bottomAlign: true);
            PlaceFrom(Nature, "campfire_logs", pier, pier.TransformPoint(new Vector3(-5.5f, 0.6f, 4f)), 0f, 1.2f, "Ates", bottomAlign: true);

            // NPC'ler (Kenney Mini Characters): Liman Reisi depo önünde, Koleksiyoncu iskele ucunda
            var reis = PlaceFrom(Chars, "character-male-c", pier, pier.TransformPoint(new Vector3(-1.8f, 0.8f, 1.5f)), yaw + 150f, 0.75f, "Liman Reisi", bottomAlign: true);
            var col = PlaceFrom(Chars, "character-female-b", pier, pier.TransformPoint(new Vector3(0.9f, 0.8f, 17.2f)), yaw + 200f, 0.72f, "Koleksiyoncu", bottomAlign: true);
            foreach (var npc in new[] { reis, col })
                if (npc != null) foreach (var c in npc.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return harbor;
        }

        // --------------------------------------------------------- deniz feneri

        public static void BuildLighthouse(Transform worldRoot)
        {
            // Kaya Burnu'nun (−36, 54, r 24) denize bakan tepesi
            var island = new Vector3(-36f, 0f, 54f);
            var toSea = (Vector3.zero - island).normalized;
            var basePos = island + toSea * 14f;
            if (Physics.Raycast(basePos + Vector3.up * 80f, Vector3.down, out var hit, 120f)) basePos = hit.point;
            // Kule tabanı zemine oturur (bottomAlign); kaya içine 1 m gömülü ki havada durmasın
            var tower = Place("tower-complete-small", worldRoot, basePos - Vector3.up * 1.0f, 0f, 9f, "Deniz Feneri", bottomAlign: true);
            if (tower == null) return;
            var beam = AddLight(tower.transform, "Isin", Vector3.zero, LightType.Spot, new Color(1f, 0.92f, 0.75f), 40f, 220f);
            var topY = 0f; foreach (var r in tower.GetComponentsInChildren<Renderer>()) topY = Mathf.Max(topY, r.bounds.max.y);
            beam.transform.position = new Vector3(basePos.x, topY - 1.2f, basePos.z);
            beam.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            beam.spotAngle = 22f; beam.innerSpotAngle = 8f;
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamba"; lamp.transform.SetParent(tower.transform, true);
            lamp.transform.position = beam.transform.position; lamp.transform.localScale = Vector3.one * 1.3f / tower.transform.localScale.x;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            var lm = new Material(Shader.Find("Dredge/StylizedLit")) { name = "LighthouseLamp" };
            lm.SetColor("_BaseColor", new Color(0.8f, 0.72f, 0.55f));
            lamp.GetComponent<MeshRenderer>().sharedMaterial = lm;
            var lh = tower.AddComponent<Lighthouse>();
            lh.beam = beam; lh.lamp = lamp.GetComponent<Renderer>();

        }

        // ------------------------------------------------------ batık & hayalet

        public static Transform BuildWreck(Transform worldRoot)
        {
            // Batı Adası (−78, 108, r 30) açığı
            var island = new Vector3(-78f, 0f, 108f);
            var dir = (new Vector3(-40f, 0f, 40f) - island).normalized;
            var pos = island + dir * 44f;
            var wreck = Place("ship-wreck", worldRoot, new Vector3(pos.x, -0.6f, pos.z), 35f, 16f, "Batik");
            if (wreck != null) wreck.transform.rotation = Quaternion.Euler(-4f, 35f, 9f);
            var lamp = AddLight(wreck != null ? wreck.transform : worldRoot, "Batik Isigi", new Vector3(0f, 3f, 0f), LightType.Point, new Color(0.5f, 0.9f, 0.7f), 3f, 14f);
            lamp.gameObject.AddComponent<LanternFlicker>();
            return wreck != null ? wreck.transform : null;
        }

        public static GhostShip BuildGhostShip(Transform worldRoot)
        {
            var ship = Place("ship-ghost", worldRoot, new Vector3(0f, 0f, -600f), 0f, 22f, "Hayalet Gemi");
            if (ship == null) return null;
            foreach (var c in ship.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var gs = ship.AddComponent<GhostShip>();
            gs.lantern = AddLight(ship.transform, "Yesil Fener", new Vector3(0f, 6f, 0f), LightType.Point, new Color(0.35f, 1f, 0.6f), 12f, 40f);
            return gs;
        }

        // ------------------------------------------------------------ oyun kökü

        public static void BuildGame(Transform worldRoot, Transform boat)
        {
            var settings = DredgeLookIntegration.LoadOrCreateSettings();

            var harbor = BuildHarbor(worldRoot);
            BuildLighthouse(worldRoot);
            var wreck = BuildWreck(worldRoot);
            var ghost = BuildGhostShip(worldRoot);


            var damage = boat.gameObject.AddComponent<BoatDamage>();

            var game = new GameObject("=== GAME ===");
            var session = game.AddComponent<GameSession>();
            var clock = game.AddComponent<GameClock>();
            clock.settings = settings;
            clock.atmosphere = Object.FindFirstObjectByType<StylizedAtmosphere>();
            clock.hour = settings.startHour;
            var inventory = game.AddComponent<Inventory>();
            var panic = game.AddComponent<PanicSystem>();
            var spawner = game.AddComponent<FishingSpotSpawner>();
            var minigame = game.AddComponent<FishingMinigame>();
            var story = game.AddComponent<StoryManager>();
            story.ghostShip = ghost;
            story.wreckSite = wreck;
            var hud = game.AddComponent<GameHUD>();
            hud.spawner = spawner;
            hud.minigame = minigame;

            session.boat = boat.GetComponent<BoatController>();
            session.clock = clock;
            session.inventory = inventory;
            session.panic = panic;
            session.damage = damage;
            session.homeHarbor = harbor;
            session.story = story;

            var respawn = harbor.respawnPoint;
            boat.position = new Vector3(respawn.position.x, boat.position.y, respawn.position.z);
            boat.rotation = Quaternion.Euler(0f, respawn.eulerAngles.y, 0f);
        }
    }
}
