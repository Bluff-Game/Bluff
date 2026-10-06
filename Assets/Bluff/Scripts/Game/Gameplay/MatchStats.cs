using Bluff.Core;

namespace Bluff.Game
{
    /// <summary>Statistik per pemain untuk layar hasil (dihitung dari event lengkap, jadi termasuk bohong yang lolos).</summary>
    public sealed class MatchStats
    {
        public int[] Plays = new int[0];
        public int[] Lies = new int[0];
        public int[] LiesCaught = new int[0];
        public int[] Shots = new int[0];
        public int[] Hits = new int[0];
        public int[] CardsTaken = new int[0];

        public void Observe(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.GameStarted:
                    int n = e.HandCounts.Length;
                    Plays = new int[n];
                    Lies = new int[n];
                    LiesCaught = new int[n];
                    Shots = new int[n];
                    Hits = new int[n];
                    CardsTaken = new int[n];
                    break;
                case GameEventType.CardsPlayed:
                    Plays[e.PlayerId]++;
                    if (e.Cards != null && !e.Cards.TrueForAll(c => c.Matches(e.Claim)))
                        Lies[e.PlayerId]++;
                    break;
                case GameEventType.ShotFired:
                    Shots[e.PlayerId]++;
                    break;
                case GameEventType.CardsRevealed:
                    if (!e.Honest)
                    {
                        LiesCaught[e.PlayerId]++;
                        Hits[e.OtherPlayerId]++;
                    }
                    break;
                case GameEventType.PileTaken:
                    CardsTaken[e.PlayerId] += e.Count;
                    break;
            }
        }
    }
}
