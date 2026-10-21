using Bluff.Core;

namespace Bluff.Game
{
    /// <summary>Nama &amp; penjelasan Kartu Kuasa untuk UI.</summary>
    public static class PowerText
    {
        public static string Name(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return "Eagle Eye";
                case PowerType.Tameng: return "Shield";
                case PowerType.Bakar: return "Burn";
                case PowerType.Badai: return "Storm";
                case PowerType.Cermin: return "Mirror";
                case PowerType.Hadiah: return "Gift";
                default: return "-";
            }
        }

        /// <summary>Penjelasan singkat untuk widget kartu kuasa.</summary>
        public static string Short(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return "Secretly peek at an opponent's latest play.";
                case PowerType.Tameng: return "Protect your next play from challenges.";
                case PowerType.Bakar: return "Burn 1-2 cards on your turn.";
                case PowerType.Badai: return "Clear the pile and lead a new round.";
                case PowerType.Cermin: return "Reflect a caught bluff back at the challenger.";
                case PowerType.Hadiah: return "Give 1-2 cards to an opponent on your turn.";
                default: return "";
            }
        }

        /// <summary>Penjelasan lengkap untuk halaman aturan.</summary>
        public static string Long(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return "During the challenge window, secretly inspect the latest play before deciding whether to challenge.";
                case PowerType.Tameng: return "Play with Shield to prevent challenges. Cannot protect your final play.";
                case PowerType.Bakar: return "On your turn, burn 1-2 cards from your hand. Keep at least 1 card.";
                case PowerType.Badai: return "When following a round, burn the center pile and lead a new round with any element.";
                case PowerType.Cermin: return "Automatic: the first time your bluff is caught, the shot reflects. The challenger takes the pile and you lead a new round. Does not protect your final play.";
                case PowerType.Hadiah: return "On your turn, give 1-2 selected cards to one opponent. Keep at least 1 card.";
                default: return "";
            }
        }
    }
}
