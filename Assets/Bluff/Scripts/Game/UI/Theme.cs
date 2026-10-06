using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>Palet warna dan helper teks berwarna. Semua UI mengambil warna dari sini.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("141916");
        public static readonly Color BackgroundTop = Hex("222B24");
        public static readonly Color Felt = Hex("263E32");
        public static readonly Color FeltLight = Hex("405E4D");
        public static readonly Color FeltRim = Hex("16291F");
        public static readonly Color Panel = Hex("1D2420");
        public static readonly Color PanelLight = Hex("29312B");
        public static readonly Color Gold = Hex("B69A62");
        public static readonly Color GoldDark = Hex("79653F");
        public static readonly Color Red = Hex("C96B60");
        public static readonly Color RedDark = Hex("6D332F");
        public static readonly Color Green = Hex("92AE91");
        public static readonly Color Orange = Hex("BE9068");
        public static readonly Color Text = Hex("F0E9D8");
        public static readonly Color TextDark = Hex("151B17");
        public static readonly Color Muted = Hex("ABA99C");
        public static readonly Color Disabled = Hex("646B62");
        public static readonly Color Border = Hex("52594D");
        public static readonly Color CardFace = Hex("EEE7D5");
        public static readonly Color CardBack = Hex("202C26");
        public static readonly Color CardBackLine = Hex("647162");
        public static readonly Color MaskCard = Hex("252B25");

        public static readonly Color[] PlayerColors =
        {
            Hex("B69A62"),
            Hex("789184"),
            Hex("A78072"),
            Hex("7D8F9A"),
            Hex("8E9674"),
            Hex("B39C7F"),
        };

        public static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Matahari: return Hex("F26B3A");
                case Element.Bulan: return Hex("8C6CF2");
                case Element.Bintang: return Hex("E9A800");
                case Element.Petir: return Hex("EC4899");
                case Element.Air: return Hex("3B82F6");
                case Element.Daun: return Hex("22A559");
                default: return Gold;
            }
        }

        public static Color PlayerColor(int playerId) => PlayerColors[playerId % PlayerColors.Length];

        public static Color WithAlpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        /// <summary>Teks rich-text berwarna untuk komponen Text legacy.</summary>
        public static string Colored(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        public static string ElementName(Element element) => element.ToString();

        public static string ColoredElement(Element element) => ElementName(element);
    }
}
