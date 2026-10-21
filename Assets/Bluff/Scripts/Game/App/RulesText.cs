using Bluff.Core;

namespace Bluff.Game
{
    public static class RulesText
    {
        public static string Build()
        {
            string refund = GameSettings.RefundBulletOnHit
                ? "The challenger leads the next round and gets their bullet back."
                : "The challenger leads the next round. Their bullet is spent.";
            string wrongShot = GameSettings.ClassicWrongShotPenalty
                ? "the challenger takes the entire pile and loses a bullet. The player who made the claim leads the next round."
                : "the challenger takes only the challenged cards and loses a bullet. The round continues.";
            return H("Objective") + "Be the first to empty your hand and survive the final challenge.\n\n" +
                H("Cards") + "52 cards: 8 each of Sun, Moon, Star, Lightning, Water and Leaf, plus 4 Masks.\n" +
                "<b>Mask</b> is wild and always matches your claim.\n\n" +
                H("Setup") + "Each player starts with 13 cards and <b>2 bullets</b>.\n\n" +
                H("Your turn") + "The round leader plays 1-3 cards face down and claims an element, such as \"2 Moon\". " +
                "That becomes the round element. Play continues clockwise. Follow with 1-3 cards claiming the same element, or Pass. " +
                "The card count is public; you may bluff about the element.\n\n" +
                H("Call BLUFF!") + "After a play, opponents have a brief challenge window. You need a bullet to call BLUFF! The first challenge counts.\n" +
                "<b>Bluff:</b> at least one card does not match the claim and is not a Mask. The bluffer takes the entire pile. " + refund + "\n" +
                "<b>Honest:</b> " + wrongShot + "\n\n" +
                H("Burning the pile") + "When every other player passes in succession, the pile is removed from the match. " +
                "The last player to play cards leads a new round and chooses a new element.\n\n" + PowersSection() +
                H("Winning") + "Your final play must survive its challenge window. If your final bluff is caught, you take the pile and keep playing.\n\n" +
                H("Tips") + "Save Masks for a safe final play. Each element has at most 12 matching cards: 8 of its own and 4 Masks. " +
                "Count the claims on the table, watch opponents' bullets, and weigh the size of the pile before bluffing.\n\n" +
                H("Controls") + "Click cards to select. Enter: play. P: pass. Space / B: BLUFF! Esc: pause.";
        }

        static string PowersSection()
        {
            if (!GameSettings.EnablePowers) return "";
            var text = new System.Text.StringBuilder();
            text.Append(H("Power cards"));
            text.Append("Each player receives one secret power. It can be used <b>once per match</b>. Select its icon to activate it when ready.\n");
            foreach (var power in Powers.All)
                text.Append("<b>").Append(PowerText.Name(power)).Append("</b>: ").Append(PowerText.Long(power)).Append('\n');
            return text.Append('\n').ToString();
        }

        static string H(string title) => "<size=32><b>" + title + "</b></size>\n";
    }
}
