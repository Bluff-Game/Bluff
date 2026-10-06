using System;
using System.Collections.Generic;

namespace Bluff.Core
{
    public static class Deck
    {
        /// <summary>Dek lengkap berurutan: tiap elemen <see cref="GameConfig.CopiesPerElement"/> kali, lalu Topeng.</summary>
        public static List<Card> Create(GameConfig config)
        {
            var cards = new List<Card>(config.TotalCards);
            int id = 0;
            foreach (var element in Elements.Claimable)
            {
                for (int i = 0; i < config.CopiesPerElement; i++)
                    cards.Add(new Card(id++, element));
            }
            for (int i = 0; i < config.WildCount; i++)
                cards.Add(new Card(id++, Element.Topeng));
            return cards;
        }

        /// <summary>Fisher-Yates.</summary>
        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
