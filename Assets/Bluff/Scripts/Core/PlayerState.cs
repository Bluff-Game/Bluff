using System.Collections.Generic;

namespace Bluff.Core
{
    public sealed class PlayerState
    {
        internal readonly List<Card> hand = new List<Card>();

        public PlayerState(int id, string name, bool isBot)
        {
            Id = id;
            Name = name;
            IsBot = isBot;
        }

        public int Id { get; }
        public string Name { get; }
        public bool IsBot { get; }
        public IReadOnlyList<Card> Hand => hand;
        public int HandCount => hand.Count;
        public int Bullets { get; internal set; }

        /// <summary>Kartu Kuasa milik pemain ini (rahasia sampai dipakai).</summary>
        public PowerType Power { get; internal set; }

        public bool PowerUsed { get; internal set; }

        public bool HasPower => Power != PowerType.None && !PowerUsed;
    }
}
