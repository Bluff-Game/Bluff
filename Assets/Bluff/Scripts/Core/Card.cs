using System;

namespace Bluff.Core
{
    /// <summary>Satu kartu fisik. Identitasnya hanya <see cref="Id"/> (unik di satu dek).</summary>
    public readonly struct Card : IEquatable<Card>
    {
        public readonly int Id;
        public readonly Element Element;

        public Card(int id, Element element)
        {
            Id = id;
            Element = element;
        }

        public bool IsWild => Element == Element.Topeng;

        /// <summary>True kalau kartu ini sah untuk klaim <paramref name="claim"/> (elemennya sama, atau Topeng).</summary>
        public bool Matches(Element claim) => Element == claim || IsWild;

        public bool Equals(Card other) => Id == other.Id;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString() => Element + "#" + Id;

        public static bool operator ==(Card a, Card b) => a.Id == b.Id;

        public static bool operator !=(Card a, Card b) => a.Id != b.Id;
    }
}
