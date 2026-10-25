using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Bluff.Core;

namespace Bluff.Game
{
    public sealed class RoomSeat
    {
        public int SeatId;
        public string Name;
        public bool Connected;
        public bool Ready;
        public bool IsBot;
        /// <summary>A bot is playing for this human seat (disconnected or idle).</summary>
        public bool Autopilot;
        public int Outfit;
        public int[] Effects = new int[7];
    }

    internal enum RoomMessage : byte
    {
        Lobby, Start, Bootstrap, Batch, Open, Intent, Ready, SceneReady, Presented, Rejected, End, ReturnRoom, Emote, Status, Resume
    }

    internal enum RoomAction : byte
    {
        Play, Pass, Power, Shoot, LetPass
    }

    internal sealed class RoomPacket
    {
        public RoomMessage Type;
        public int Revision;
        public int Sequence;
        public int Seat;
        public bool Flag;
        public float Seconds;
        public string Text = "";
        public RoomSeat[] Seats;
        public GameSnapshot Snapshot;
        public List<GameEvent> Events = new List<GameEvent>();
        public RoomAction Action;
        public Element Claim;
        public PowerType Power;
        public int Target = -1;
        public int Emote;
        public List<int> Cards = new List<int>();
    }

    internal sealed class IntentGate
    {
        int lastSequence;
        double window;
        int count;

        public bool Accept(int sequence, int revision, int currentRevision, double now)
        {
            if (now - window >= 1)
            {
                window = now;
                count = 0;
            }
            if (++count > 16 || sequence <= lastSequence || sequence <= 0)
                return false;
            lastSequence = sequence;
            return revision == currentRevision;
        }
    }

    internal static class RoomProtocol
    {
        public const int MaxPayload = 65536;
        public const int Version = 5;
        public const float MaxOpenSeconds = 120f;
        const int MaxCodeBytes = 32;
        const int Magic = 0x424C5546;
        const int MaxCards = 52;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string CleanName(string value)
        {
            var result = new StringBuilder();
            foreach (char c in value ?? "")
            {
                var category = char.GetUnicodeCategory(c);
                if (c == '<' || c == '>' || char.IsControl(c) || char.IsSurrogate(c) || category == UnicodeCategory.Format)
                    continue;
                result.Append(c);
                if (result.Length >= 24)
                    break;
            }
            string name = result.ToString().Trim();
            return name.Length == 0 ? "Player" : name;
        }

        public static RoomPacket Batch(GameEngine engine, IEnumerable<GameEvent> events, int viewer, int revision)
        {
            var packet = new RoomPacket { Type = RoomMessage.Batch, Revision = revision, Snapshot = GameSnapshot.Create(engine, viewer) };
            foreach (var e in events)
                packet.Events.Add(e.RedactedFor(viewer));
            return packet;
        }

        public static byte[] Encode(RoomPacket p)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream, Utf8))
            {
                w.Write(Magic);
                w.Write((byte)Version);
                w.Write((byte)p.Type);
                w.Write(p.Revision);
                switch (p.Type)
                {
                    case RoomMessage.Emote:
                        Require(p.Seat >= 0 && p.Seat < 4 && p.Target >= 0 && p.Target < 4 && p.Target != p.Seat && p.Emote >= 0 && p.Emote < 2);
                        w.Write((byte)p.Seat); w.Write((byte)p.Target); w.Write((byte)p.Emote); break;
                    case RoomMessage.Lobby:
                        w.Write(p.Seat);
                        Text(w, p.Text, MaxCodeBytes);
                        Require(p.Seats != null && p.Seats.Length == 4);
                        foreach (var seat in p.Seats)
                        {
                            Require(seat != null && Cosmetics.ValidOutfitCode(seat.Outfit) && seat.Effects != null && seat.Effects.Length == 7);
                            w.Write(seat.SeatId);
                            Text(w, CleanName(seat.Name), 96);
                            w.Write(seat.Connected);
                            w.Write(seat.Ready);
                            w.Write(seat.IsBot);
                            w.Write((ushort)seat.Outfit);
                            for (int i = 1; i <= 6; i++)
                            {
                                Require(Cosmetics.ValidEffect(seat.Effects[i]));
                                w.Write((byte)seat.Effects[i]);
                            }
                        }
                        break;
                    case RoomMessage.Bootstrap:
                    case RoomMessage.Batch:
                        Snapshot(w, p.Snapshot);
                        Require(p.Events != null && p.Events.Count <= 64);
                        w.Write(p.Events.Count);
                        foreach (var e in p.Events) Event(w, e);
                        break;
                    case RoomMessage.Open:
                        w.Write(p.Seconds);
                        w.Write(p.Flag);
                        break;
                    case RoomMessage.Intent:
                        w.Write(p.Sequence);
                        w.Write((byte)p.Action);
                        w.Write((byte)p.Claim);
                        w.Write((byte)p.Power);
                        w.Write(p.Target);
                        w.Write(p.Flag);
                        Require(p.Cards != null && p.Cards.Count <= 3);
                        w.Write(p.Cards.Count);
                        foreach (int id in p.Cards) w.Write(id);
                        break;
                    case RoomMessage.Ready:
                        w.Write(p.Flag);
                        break;
                    case RoomMessage.Status:
                        Require(p.Seats != null && p.Seats.Length == 4);
                        foreach (var seat in p.Seats)
                        {
                            Require(seat != null);
                            w.Write((byte)((seat.Connected ? 1 : 0) | (seat.Autopilot ? 2 : 0)));
                        }
                        break;
                    case RoomMessage.Rejected:
                    case RoomMessage.End:
                        Text(w, p.Text, 512);
                        break;
                }
                Require(stream.Length <= MaxPayload);
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] bytes, out RoomPacket packet)
        {
            packet = null;
            if (bytes == null || bytes.Length < 10 || bytes.Length > MaxPayload)
                return false;
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var r = new BinaryReader(stream, Utf8))
                {
                    Require(r.ReadInt32() == Magic && r.ReadByte() == Version);
                    var p = new RoomPacket { Type = (RoomMessage)Number(r.ReadByte(), 0, (int)RoomMessage.Resume), Revision = Number(r.ReadInt32(), 0, int.MaxValue) };
                    switch (p.Type)
                    {
                        case RoomMessage.Emote:
                            p.Seat=Number(r.ReadByte(),0,3); p.Target=Number(r.ReadByte(),0,3); p.Emote=Number(r.ReadByte(),0,1); Require(p.Seat!=p.Target); break;
                        case RoomMessage.Lobby:
                            p.Seat = Number(r.ReadInt32(), 0, 3);
                            p.Text = Text(r, MaxCodeBytes);
                            p.Seats = new RoomSeat[4];
                            for (int i = 0; i < 4; i++)
                            {
                                Require(r.ReadInt32() == i);
                                p.Seats[i] = new RoomSeat { SeatId = i, Name = CleanName(Text(r, 96)), Connected = Bool(r), Ready = Bool(r), IsBot = Bool(r) };
                                p.Seats[i].Outfit = r.ReadUInt16();
                                Require(Cosmetics.ValidOutfitCode(p.Seats[i].Outfit));
                                for (int effect = 1; effect <= 6; effect++)
                                    p.Seats[i].Effects[effect] = Number(r.ReadByte(), 0, Cosmetics.EffectNames.Length - 1);
                            }
                            break;
                        case RoomMessage.Bootstrap:
                        case RoomMessage.Batch:
                            p.Snapshot = Snapshot(r);
                            int count = Number(r.ReadInt32(), 0, 64);
                            for (int i = 0; i < count; i++)
                            {
                                var e = Event(r);
                                Require(e.Cards == null || e.CanSeeCards(p.Snapshot.ViewerId));
                                Require(e.Type != GameEventType.PowerDealt || e.PlayerId == p.Snapshot.ViewerId || e.Power == PowerType.None);
                                Require(e.Type != GameEventType.PowerPeek || e.PlayerId == p.Snapshot.ViewerId || !e.Honest);
                                p.Events.Add(e);
                            }
                            break;
                        case RoomMessage.Open:
                            p.Seconds = r.ReadSingle();
                            Require(!float.IsNaN(p.Seconds) && !float.IsInfinity(p.Seconds) && p.Seconds >= 0 && p.Seconds <= MaxOpenSeconds);
                            p.Flag = Bool(r);
                            break;
                        case RoomMessage.Intent:
                            p.Sequence = Number(r.ReadInt32(), 1, int.MaxValue);
                            p.Action = (RoomAction)Number(r.ReadByte(), 0, (int)RoomAction.LetPass);
                            p.Claim = (Element)Number(r.ReadByte(), 0, 5);
                            p.Power = (PowerType)Number(r.ReadByte(), 0, 6);
                            p.Target = Number(r.ReadInt32(), -1, 3);
                            p.Flag = Bool(r);
                            int ids = Number(r.ReadInt32(), 0, 3);
                            for (int i = 0; i < ids; i++)
                            {
                                int id = Number(r.ReadInt32(), 0, MaxCards - 1);
                                Require(!p.Cards.Contains(id));
                                p.Cards.Add(id);
                            }
                            Require(p.Action != RoomAction.Play || ids > 0);
                            Require(p.Action != RoomAction.Power || (p.Power != PowerType.None && ids <= Powers.MaxPowerCards));
                            Require(p.Action == RoomAction.Play || p.Action == RoomAction.Power || ids == 0);
                            break;
                        case RoomMessage.Ready:
                            p.Flag = Bool(r);
                            break;
                        case RoomMessage.Status:
                            p.Seats = new RoomSeat[4];
                            for (int i = 0; i < 4; i++)
                            {
                                int flags = Number(r.ReadByte(), 0, 3);
                                p.Seats[i] = new RoomSeat { SeatId = i, Connected = (flags & 1) != 0, Autopilot = (flags & 2) != 0 };
                            }
                            break;
                        case RoomMessage.Rejected:
                        case RoomMessage.End:
                            p.Text = Text(r, 512);
                            Require(p.Text.IndexOf('<') < 0 && p.Text.IndexOf('>') < 0);
                            break;
                    }
                    Require(stream.Position == stream.Length);
                    packet = p;
                    return true;
                }
            }
            catch (InvalidDataException) { return false; }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
            catch (OverflowException) { return false; }
        }

        static void Snapshot(BinaryWriter w, GameSnapshot s)
        {
            w.Write(s.ViewerId);
            var c = s.Config;
            w.Write(c.RefundBulletOnHit);
            w.Write(c.WrongShotTakesWholePile);
            w.Write(c.EnablePowers);
            Cards(w, s.MyHand);
            for (int i = 0; i < 4; i++)
            {
                Text(w, CleanName(s.Names[i]), 96);
                w.Write(s.HandCounts[i]);
                w.Write(s.Bullets[i]);
                w.Write(s.PowerSpent[i]);
                w.Write((byte)s.KnownPowers[i]);
            }
            w.Write((byte)s.Phase);
            w.Write(s.CurrentPlayer);
            w.Write(s.RoundLeader);
            w.Write(s.HasRoundElement);
            w.Write((byte)s.RoundElement);
            w.Write(s.LastPlayerId);
            w.Write(s.LastPlayCount);
            w.Write((byte)s.LastPlayClaim);
            w.Write(s.LastPlayShielded);
            w.Write(s.PileCount);
            w.Write(s.BurnedCount);
            w.Write(s.ConsecutivePasses);
            w.Write(s.Winner);
            w.Write((byte)s.MyPower);
            w.Write(s.MyPowerUsed);
            Require(s.RoundClaims.Count <= 128);
            w.Write(s.RoundClaims.Count);
            foreach (var claim in s.RoundClaims)
            {
                w.Write(claim.PlayerId);
                w.Write(claim.Count);
            }
        }

        static GameSnapshot Snapshot(BinaryReader r)
        {
            var s = new GameSnapshot
            {
                ViewerId = Number(r.ReadInt32(), 0, 3), PlayerCount = 4,
                Config = new GameConfig { RefundBulletOnHit = Bool(r), WrongShotTakesWholePile = Bool(r), EnablePowers = Bool(r) },
                MyHand = Cards(r), Names = new string[4], HandCounts = new int[4], Bullets = new int[4], PowerSpent = new bool[4], KnownPowers = new PowerType[4]
            };
            Require(s.MyHand != null);
            for (int i = 0; i < 4; i++)
            {
                s.Names[i] = CleanName(Text(r, 96));
                s.HandCounts[i] = Number(r.ReadInt32(), 0, MaxCards);
                s.Bullets[i] = Number(r.ReadInt32(), 0, 2);
                s.PowerSpent[i] = Bool(r);
                s.KnownPowers[i] = (PowerType)Number(r.ReadByte(), 0, 6);
                Require(i == s.ViewerId || s.PowerSpent[i] || s.KnownPowers[i] == PowerType.None);
            }
            Require(s.MyHand.Count == s.HandCounts[s.ViewerId]);
            s.Phase = (GamePhase)Number(r.ReadByte(), 0, 3);
            s.CurrentPlayer = Number(r.ReadInt32(), -1, 3);
            s.RoundLeader = Number(r.ReadInt32(), -1, 3);
            s.HasRoundElement = Bool(r);
            s.RoundElement = (Element)Number(r.ReadByte(), 0, 5);
            s.LastPlayerId = Number(r.ReadInt32(), -1, 3);
            s.LastPlayCount = Number(r.ReadInt32(), 0, 3);
            s.LastPlayClaim = (Element)Number(r.ReadByte(), 0, 5);
            s.LastPlayShielded = Bool(r);
            s.PileCount = Number(r.ReadInt32(), 0, MaxCards);
            s.BurnedCount = Number(r.ReadInt32(), 0, MaxCards);
            s.ConsecutivePasses = Number(r.ReadInt32(), 0, 3);
            s.Winner = Number(r.ReadInt32(), -1, 3);
            s.MyPower = (PowerType)Number(r.ReadByte(), 0, 6);
            s.MyPowerUsed = Bool(r);
            int claims = Number(r.ReadInt32(), 0, 128);
            for (int i = 0; i < claims; i++)
                s.RoundClaims.Add(new ClaimInfo(Number(r.ReadInt32(), 0, 3), Number(r.ReadInt32(), 1, 3)));
            return s;
        }

        static void Event(BinaryWriter w, GameEvent e)
        {
            w.Write(e.Sequence);
            w.Write((byte)e.Type);
            w.Write(e.PlayerId);
            w.Write(e.OtherPlayerId);
            w.Write(e.Count);
            w.Write((byte)e.Claim);
            w.Write(e.Honest);
            w.Write(e.Partial);
            w.Write(e.Shielded);
            w.Write((byte)e.Power);
            w.Write((byte)e.Reason);
            Cards(w, e.Cards);
            for (int i = 0; i < 4; i++)
            {
                w.Write(e.HandCounts[i]);
                w.Write(e.Bullets[i]);
                w.Write(e.PowerSpent[i]);
            }
            w.Write(e.PileCount);
            w.Write(e.BurnedCount);
            w.Write(e.CurrentPlayer);
        }

        static GameEvent Event(BinaryReader r)
        {
            var e = new GameEvent
            {
                Sequence = Number(r.ReadInt32(), 1, int.MaxValue), Type = (GameEventType)Number(r.ReadByte(), 0, 16),
                PlayerId = Number(r.ReadInt32(), -1, 3), OtherPlayerId = Number(r.ReadInt32(), -1, 3), Count = Number(r.ReadInt32(), 0, MaxCards),
                Claim = (Element)Number(r.ReadByte(), 0, 5), Honest = Bool(r), Partial = Bool(r), Shielded = Bool(r),
                Power = (PowerType)Number(r.ReadByte(), 0, 6), Reason = (RoundStartReason)Number(r.ReadByte(), 0, 2), Cards = Cards(r),
                HandCounts = new int[4], Bullets = new int[4], PowerSpent = new bool[4]
            };
            for (int i = 0; i < 4; i++)
            {
                e.HandCounts[i] = Number(r.ReadInt32(), 0, MaxCards);
                e.Bullets[i] = Number(r.ReadInt32(), 0, 2);
                e.PowerSpent[i] = Bool(r);
            }
            e.PileCount = Number(r.ReadInt32(), 0, MaxCards);
            e.BurnedCount = Number(r.ReadInt32(), 0, MaxCards);
            e.CurrentPlayer = Number(r.ReadInt32(), -1, 3);
            return e;
        }

        static void Cards(BinaryWriter w, List<Card> cards)
        {
            Require(cards == null || cards.Count <= MaxCards);
            w.Write(cards == null ? -1 : cards.Count);
            if (cards == null) return;
            foreach (var card in cards)
            {
                w.Write(card.Id);
                w.Write((byte)card.Element);
            }
        }

        static List<Card> Cards(BinaryReader r)
        {
            int count = Number(r.ReadInt32(), -1, MaxCards);
            if (count < 0) return null;
            var cards = new List<Card>(count);
            var ids = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int id = Number(r.ReadInt32(), 0, MaxCards - 1);
                Require(ids.Add(id));
                cards.Add(new Card(id, (Element)Number(r.ReadByte(), 0, 6)));
            }
            return cards;
        }

        static void Text(BinaryWriter w, string text, int max)
        {
            byte[] bytes = Utf8.GetBytes(text ?? "");
            Require(bytes.Length <= max);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        static string Text(BinaryReader r, int max)
        {
            int length = Number(r.ReadInt32(), 0, max);
            var bytes = r.ReadBytes(length);
            Require(bytes.Length == length);
            return Utf8.GetString(bytes);
        }

        static bool Bool(BinaryReader r) => Number(r.ReadByte(), 0, 1) == 1;

        static int Number(int value, int min, int max)
        {
            Require(value >= min && value <= max);
            return value;
        }

        static void Require(bool condition)
        {
            if (!condition) throw new InvalidDataException("Invalid room message.");
        }
    }
}
