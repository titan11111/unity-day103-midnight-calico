using System.Collections.Generic;
using UnityEngine;

namespace MidnightCalico
{
    public class ItemInfo
    {
        public ItemId id;
        public string name;
        public string desc;
        public Color color;
        public bool consumable;
        public ItemInfo(ItemId id, string name, Color color, bool consumable, string desc)
        {
            this.id = id; this.name = name; this.color = color; this.consumable = consumable; this.desc = desc;
        }
    }

    public class Inventory
    {
        public static readonly Dictionary<ItemId, ItemInfo> Info = new Dictionary<ItemId, ItemInfo>
        {
            { ItemId.Flashlight, new ItemInfo(ItemId.Flashlight, "懐中電灯", new Color(0.85f, 0.8f, 0.4f), false,
                "電池は十分。暗がりで光ると、ミケの目が光って見える。使うとON/OFF。") },
            { ItemId.Toy, new ItemInfo(ItemId.Toy, "猫じゃらし", new Color(0.95f, 0.75f, 0.3f), false,
                "ミケのお気に入り。近くで振ると夢中になって飛びついてくる。何度でも使えるが、すぐ飽きる。") },
            { ItemId.Food, new ItemInfo(ItemId.Food, "カリカリ", new Color(0.7f, 0.45f, 0.25f), true,
                "ドライフード。床に置いて離れると、ミケが食べに来る。食べている間はチャンス。") },
            { ItemId.Paste, new ItemInfo(ItemId.Paste, "おやつペースト", new Color(0.9f, 0.55f, 0.6f), true,
                "ミケの大好物。近くで封を切ると、隠れていても自分から寄ってくる。") },
            { ItemId.Net, new ItemInfo(ItemId.Net, "洗濯ネット", new Color(0.85f, 0.88f, 0.92f), false,
                "持っているだけで、捕まえる成功率が大きく上がる。動物病院でもおすすめされる方法だ。") },
            { ItemId.Matatabi, new ItemInfo(ItemId.Matatabi, "またたび", new Color(0.55f, 0.7f, 0.35f), true,
                "床にまくと、ミケがごろごろ転がって夢中になる。警戒していても効きやすい。") },
            { ItemId.Bandage, new ItemInfo(ItemId.Bandage, "絆創膏", new Color(0.95f, 0.8f, 0.65f), true,
                "ひっかき傷の手当てに。体力が回復する。") },
        };

        public readonly List<ItemId> order = new List<ItemId>();
        readonly Dictionary<ItemId, int> count = new Dictionary<ItemId, int>();
        public ItemId equipped = ItemId.Flashlight;

        public Inventory() { Add(ItemId.Flashlight, 1); }

        public void Add(ItemId id, int n)
        {
            if (!count.ContainsKey(id)) { count[id] = 0; order.Add(id); }
            count[id] += n;
        }

        public bool Has(ItemId id) { int c; return count.TryGetValue(id, out c) && c > 0; }
        public int Count(ItemId id) { int c; return count.TryGetValue(id, out c) ? c : 0; }

        public void Remove(ItemId id)
        {
            int c;
            if (!count.TryGetValue(id, out c)) return;
            c--;
            if (c <= 0)
            {
                count.Remove(id);
                order.Remove(id);
                if (equipped == id) equipped = ItemId.None;
            }
            else count[id] = c;
        }
    }
}
