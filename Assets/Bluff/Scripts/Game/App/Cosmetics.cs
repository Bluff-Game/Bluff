using System;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    public enum WardrobeSlot { Jacket, Trousers, Shoes, Headwear, Eyewear }

    public static class Cosmetics
    {
        public static readonly string[] CostumeNames = { "The Host", "Midnight Magician", "Crimson Noble", "Ivory Royal", "Emerald Regent", "Brass Captain", "Blue Gambit", "Velvet High Roller", "After Hours" };
        public static readonly string[] CostumeDescriptions = {
            "The original card-room ensemble.", "Charcoal tailoring with a silver mask and beret.",
            "Wine-red tailoring with a gold mask and top hat.", "Ivory tailoring, jade mask, and a brass crown.",
            "Emerald tails, pinstripes, and a monocle.", "Officer's coat, field trousers, and tall boots.",
            "Cobalt sash with ivory tailoring and a fedora.", "Velvet smoking jacket, satin trousers, and tassel loafers.",
            "Relaxed midnight tailoring, canvas shoes, and smoked glasses."
        };
        static readonly int[][] Bundles = {
            new[]{0,0,0,0,0}, new[]{1,1,1,1,1}, new[]{2,2,2,2,2}, new[]{3,3,3,3,3},
            new[]{4,4,4,0,7}, new[]{5,7,4,5,4}, new[]{6,6,6,7,4}, new[]{7,7,7,7,7}, new[]{1,5,5,6,5}
        };
        public static readonly string[] EffectNames = { "Classic", "Brass Ritual", "Jade Current" };
        public static readonly int[] EffectPrices = { 0, 80, 120 };
        public static readonly string[] SlotNames = { "Jackets", "Trousers", "Shoes", "Headwear", "Eyewear" };
        public static readonly string[][] ItemNames = {
            new[] { "Host's jacket", "Midnight jacket", "Crimson jacket", "Ivory jacket", "Emerald tailcoat", "Officer's coat", "Cobalt duelist", "Velvet smoking jacket" },
            new[] { "Slate trousers", "Midnight trousers", "Wine trousers", "Sage trousers", "Pinstripe trousers", "Camel trousers", "Ivory pleats", "Satin-stripe trousers" },
            new[] { "Black Oxfords", "Silver-buckle shoes", "Oxblood Oxfords", "Ivory spats", "Riding boots", "Canvas high-tops", "Two-tone brogues", "Tassel loafers" },
            new[] { "Host's top hat", "Midnight beret", "Crimson top hat", "Brass crown", "No headwear", "Tweed cap", "Crimson bandana", "Ivory fedora" },
            new[] { "Brass mask", "Silver mask", "Noble's mask", "Jade mask", "Round spectacles", "Smoked glasses", "No eyewear", "Brass monocle" }
        };
        static readonly int[][] ItemPrices = {
            new[] { 0, 70, 95, 120, 135, 145, 130, 155 }, new[] { 0, 40, 50, 65, 75, 60, 80, 85 }, new[] { 0, 35, 45, 65, 85, 70, 80, 90 },
            new[] { 0, 45, 65, 95, 0, 55, 40, 90 }, new[] { 0, 35, 50, 75, 60, 75, 0, 80 }
        };
        public const int StartingCoins = 300;
        public const int MatchCoins = 40;
        public const int WinBonus = 40;
        const string SaveKey = "bluff.cosmetics.v1";

        [Serializable]
        sealed class SaveData
        {
            public int Coins = StartingCoins;
            public int Costume;
            public int Schema;
            public int[] Outfit;
            public List<int> OwnedItems;
            public List<int> OwnedCostumes = new List<int> { 0 };
            public List<int> OwnedEffects = new List<int>();
            public int[] Effects = new int[7];
            public List<string> RewardedMatches = new List<string>();
        }

        static SaveData data;
        public static event Action Changed;
        static SaveData Data
        {
            get
            {
                if (data != null) return data;
                try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey, "")); }
                catch (ArgumentException) { }
                data = data ?? new SaveData();
                data.Coins = Mathf.Clamp(data.Coins, 0, 1000000);
                data.OwnedCostumes = data.OwnedCostumes ?? new List<int>();
                data.OwnedEffects = data.OwnedEffects ?? new List<int>();
                data.RewardedMatches = data.RewardedMatches ?? new List<string>();
                if (data.Effects == null || data.Effects.Length != 7) data.Effects = new int[7];
                if (!data.OwnedCostumes.Contains(0)) data.OwnedCostumes.Add(0);
                if (!ValidCostume(data.Costume) || !data.OwnedCostumes.Contains(data.Costume)) data.Costume = 0;
                if (data.Schema < 2)
                {
                    data.Outfit = BundleOutfit(data.Costume);
                    data.OwnedItems = new List<int>();
                    foreach (int bundle in data.OwnedCostumes)
                        if (ValidCostume(bundle))
                            for (int slot = 0; slot < 5; slot++) data.OwnedItems.Add(slot * 10 + Bundles[bundle][slot]);
                    data.Schema = 2;
                }
                data.OwnedItems = data.OwnedItems ?? new List<int>();
                if (!ValidOutfit(data.Outfit)) data.Outfit = new int[5];
                for (int slot = 0; slot < 5; slot++)
                    if (!OwnsItem((WardrobeSlot)slot, data.Outfit[slot])) data.Outfit[slot] = 0;
                foreach (var power in Powers.All)
                    if (!OwnsEffect(power, data.Effects[(int)power])) data.Effects[(int)power] = 0;
                Persist();
                return data;
            }
        }

        static void Persist()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        static void Commit() { Persist(); Changed?.Invoke(); }
        public static int Coins => Data.Coins;
        public static int Costume => Data.Costume;
        public static int[] Outfit => (int[])Data.Outfit.Clone();
        public static int OutfitCode => Pack(Outfit);
        public static bool ValidItem(WardrobeSlot slot, int item) => (int)slot >= 0 && (int)slot < 5 && item >= 0 && item < ItemNames[(int)slot].Length;
        public static bool ValidOutfit(int[] outfit)
        {
            if (outfit == null || outfit.Length != 5) return false;
            for (int i = 0; i < 5; i++) if (!ValidItem((WardrobeSlot)i, outfit[i])) return false;
            return true;
        }
        public static int[] BundleOutfit(int id) => (int[])Bundles[ValidCostume(id) ? id : 0].Clone();
        public static int Pack(int[] outfit)
        {
            int code = 0;
            for (int i = 0; i < 5; i++) code |= outfit[i] << (i * 3);
            return code;
        }
        public static int[] Unpack(int code)
        {
            var result = new int[5];
            for (int i = 0; i < 5; i++) result[i] = (code >> (i * 3)) & 7;
            return result;
        }
        public static bool ValidOutfitCode(int code) => code >= 0 && code < 32768 && ValidOutfit(Unpack(code));
        public static int ItemPrice(WardrobeSlot slot, int id) => ValidItem(slot, id) ? ItemPrices[(int)slot][id] : 0;
        public static bool OwnsItem(WardrobeSlot slot, int id) => ValidItem(slot, id) && (ItemPrice(slot, id) == 0 || Data.OwnedItems.Contains((int)slot * 10 + id));
        public static bool BuyItem(WardrobeSlot slot, int id)
        {
            if (!ValidItem(slot, id) || OwnsItem(slot, id) || Coins < ItemPrice(slot, id)) return false;
            Data.Coins -= ItemPrice(slot, id);
            Data.OwnedItems.Add((int)slot * 10 + id);
            Commit(); return true;
        }
        public static bool EquipItem(WardrobeSlot slot, int id)
        {
            if (!OwnsItem(slot, id)) return false;
            Data.Outfit[(int)slot] = id;
            Commit(); return true;
        }
        public static int BundlePrice(int id)
        {
            int price = 0;
            if (!ValidCostume(id)) return 0;
            for (int slot = 0; slot < 5; slot++) if (!OwnsItem((WardrobeSlot)slot, Bundles[id][slot])) price += ItemPrice((WardrobeSlot)slot, Bundles[id][slot]);
            return Mathf.CeilToInt(price * .85f);
        }
        public static bool ValidCostume(int id) => id >= 0 && id < CostumeNames.Length;
        public static bool ValidEffect(int id) => id >= 0 && id < EffectNames.Length;
        static bool ValidPower(PowerType power) => power >= PowerType.MataElang && power <= PowerType.Hadiah;
        public static bool OwnsCostume(int id) => ValidCostume(id) && BundlePrice(id) == 0;
        public static bool OwnsEffect(PowerType power, int id) => ValidPower(power) && ValidEffect(id) && (id == 0 || Data.OwnedEffects.Contains((int)power * 10 + id));
        public static int Effect(PowerType power) => ValidPower(power) ? Data.Effects[(int)power] : 0;

        public static bool BuyCostume(int id)
        {
            if (!ValidCostume(id) || OwnsCostume(id) || Coins < BundlePrice(id)) return false;
            Data.Coins -= BundlePrice(id);
            for (int slot = 0; slot < 5; slot++)
                if (!OwnsItem((WardrobeSlot)slot, Bundles[id][slot])) Data.OwnedItems.Add(slot * 10 + Bundles[id][slot]);
            if (!Data.OwnedCostumes.Contains(id)) Data.OwnedCostumes.Add(id);
            Commit();
            return true;
        }

        public static bool BuyEffect(PowerType power, int id)
        {
            if (!ValidPower(power) || !ValidEffect(id) || OwnsEffect(power, id) || Coins < EffectPrices[id]) return false;
            Data.Coins -= EffectPrices[id];
            Data.OwnedEffects.Add((int)power * 10 + id);
            Commit();
            return true;
        }

        public static bool EquipCostume(int id)
        {
            if (!OwnsCostume(id)) return false;
            Data.Costume = id;
            Data.Outfit = BundleOutfit(id);
            Commit();
            return true;
        }

        public static bool EquipEffect(PowerType power, int id)
        {
            if (!OwnsEffect(power, id)) return false;
            Data.Effects[(int)power] = id;
            Commit();
            return true;
        }

        public static int RewardMatch(string matchId, bool won)
        {
            if (string.IsNullOrEmpty(matchId) || Data.RewardedMatches.Contains(matchId)) return 0;
            int amount = Mathf.Min(1000000 - Coins, MatchCoins + (won ? WinBonus : 0));
            Data.Coins += amount;
            Data.RewardedMatches.Add(matchId);
            if (Data.RewardedMatches.Count > 128) Data.RewardedMatches.RemoveAt(0);
            Commit();
            return amount;
        }

        public static HumanoidStyle CostumeStyle(int id)
        {
            var style = HumanoidStyle.ForPlayer(0);
            switch (id)
            {
                case 1:
                    style.Shirt = Theme.Hex("303C48"); style.Pants = Theme.Hex("1B222B");
                    style.Mask = Theme.Hex("BFC8C4"); style.Hat = Theme.Hex("222A35"); style.HatStyle = HatStyle.Beret; break;
                case 2:
                    style.Shirt = Theme.Hex("982E3D"); style.Pants = Theme.Hex("301D27");
                    style.Mask = Theme.Hex("D7B46C"); style.Hat = Theme.Hex("661B2F"); style.HatStyle = HatStyle.TopHat; break;
                case 3:
                    style.Shirt = Theme.Hex("E6D9AD"); style.Pants = Theme.Hex("57634D");
                    style.Mask = Theme.Hex("547E68"); style.Hat = Theme.Gold; style.HatStyle = HatStyle.Crown; break;
            }
            return style;
        }

        public static HumanoidStyle OutfitStyle(int code)
        {
            var outfit = ValidOutfitCode(code) ? Unpack(code) : new int[5];
            var style = CostumeStyle(outfit[0]);
            style.JacketStyle=outfit[0]; style.TrouserStyle=outfit[1];
            if(outfit[0]>=4) style.Shirt=Theme.Hex(new[]{"246449","B0843C","264C89","682C40"}[outfit[0]-4]);
            style.Pants = CostumeStyle(outfit[1]).Pants;
            if(outfit[1]>=4) style.Pants=Theme.Hex(new[]{"303340","AA8257","D4CBB1","252A30"}[outfit[1]-4]);
            style.Shoes = Theme.Hex(new[] { "252020", "343C45", "642A2C", "D3C6A1", "483122", "C4B898", "E2D6B9", "542B25" }[outfit[2]]);
            style.ShoeStyle = outfit[2];
            var hat = CostumeStyle(outfit[3]);
            style.Hat = hat.Hat;
            style.HatStyle = outfit[3] == 4 ? HatStyle.None : hat.HatStyle;
            if(outfit[3]==5) { style.HatStyle=HatStyle.Cap; style.Hat=Theme.Hex("8A714D"); }
            if(outfit[3]==6) { style.HatStyle=HatStyle.Bandana; style.Hat=Theme.Hex("A02B3C"); }
            if(outfit[3]==7) { style.HatStyle=HatStyle.Fedora; style.Hat=Theme.Hex("DFD1B0"); }
            style.Mask = CostumeStyle(Mathf.Min(3, outfit[4])).Mask;
            style.Eyewear = outfit[4] < 4 ? 0 : outfit[4] - 3;
            return style;
        }

        public static HumanoidStyle LocalStyle => OutfitStyle(OutfitCode);

        public static int OutfitForSeat(int seat, int human)
        {
            var room = InternetRoom.Current;
            if (room != null && room.InMatch && seat >= 0 && seat < room.Seats.Count && !room.Seats[seat].IsBot)
                return room.Seats[seat].Outfit;
            return seat == human || human < 0 && seat == 0 ? OutfitCode : -1;
        }

        public static int EffectForSeat(int seat, int human, PowerType power)
        {
            if (!ValidPower(power)) return 0;
            var room = InternetRoom.Current;
            if (room != null && room.InMatch && seat >= 0 && seat < room.Seats.Count && !room.Seats[seat].IsBot)
            {
                var effects = room.Seats[seat].Effects;
                return effects != null && effects.Length > (int)power && ValidEffect(effects[(int)power]) ? effects[(int)power] : 0;
            }
            return seat == human ? Effect(power) : 0;
        }
    }
}
