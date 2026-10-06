using System.Collections.Generic;

namespace Bluff.Core
{
    /// <summary>Satu taruhan: kartu tertutup yang ditaruh beserta klaim elemennya.</summary>
    public sealed class Play
    {
        public Play(int playerId, Element claim, IReadOnlyList<Card> cards, bool shielded = false)
        {
            PlayerId = playerId;
            Claim = claim;
            Cards = cards;
            Shielded = shielded;
        }

        public int PlayerId { get; }

        /// <summary>Dilindungi Tameng: tidak bisa ditembak.</summary>
        public bool Shielded { get; }
        public Element Claim { get; }
        public IReadOnlyList<Card> Cards { get; }
        public int Count => Cards.Count;

        public bool IsHonest
        {
            get
            {
                for (int i = 0; i < Cards.Count; i++)
                {
                    if (!Cards[i].Matches(Claim))
                        return false;
                }
                return true;
            }
        }
    }
}
