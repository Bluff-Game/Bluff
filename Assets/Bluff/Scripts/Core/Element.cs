using System;
using System.Collections.Generic;

namespace Bluff.Core
{
    /// <summary>
    /// Jenis kartu. Enam elemen pertama bisa diklaim; <see cref="Topeng"/> adalah kartu wild
    /// yang selalu dihitung sebagai elemen apa pun yang diklaim.
    /// </summary>
    public enum Element
    {
        Matahari = 0,
        Bulan = 1,
        Bintang = 2,
        Petir = 3,
        Air = 4,
        Daun = 5,
        Topeng = 6,
    }

    public static class Elements
    {
        public const int ClaimableCount = 6;

        /// <summary>Elemen yang boleh diklaim (semua kecuali Topeng), urut sesuai nilai enum.</summary>
        public static readonly IReadOnlyList<Element> Claimable = Array.AsReadOnly(new[]
        {
            Element.Matahari, Element.Bulan, Element.Bintang, Element.Petir, Element.Air, Element.Daun,
        });

        public static bool IsWild(this Element element) => element == Element.Topeng;

        public static bool IsClaimable(this Element element) => element >= Element.Matahari && element <= Element.Daun;
    }
}
