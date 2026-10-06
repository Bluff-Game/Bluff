using Bluff.Core;

namespace Bluff.Game
{
    /// <summary>Teks "Cara Bermain" (rich text). Mengikuti pengaturan aturan yang sedang aktif.</summary>
    public static class RulesText
    {
        public static string Build()
        {
            string mask = "<b>Topeng</b>";
            string lie = "<b>Bohong</b>";
            string honest = "<b>Jujur</b>";
            string refund = GameSettings.RefundBulletOnHit
                ? "Penembak memimpin ronde baru, dan pelurunya <b>kembali</b> (tembakan jitu tidak menghabiskan peluru)."
                : "Penembak memimpin ronde baru. Pelurunya tetap terpakai.";
            string wrongShot = GameSettings.ClassicWrongShotPenalty
                ? "penembak mengambil <b>seluruh</b> tumpukan dan pelurunya hilang. Penaruh memimpin ronde baru."
                : "penembak mengambil <b>kartu yang ia tuduh</b> saja dan pelurunya hilang. Ronde berlanjut seperti biasa.";

            return
                H("Tujuan") +
                "Jadilah pemain pertama yang menghabiskan semua kartu di tangan.\n\n" +

                H("Kartu") +
                "52 kartu: 6 elemen × 8 kartu (" +
                Theme.ElementName(Element.Matahari) + ", " +
                Theme.ElementName(Element.Bulan) + ", " +
                Theme.ElementName(Element.Bintang) + ", " +
                Theme.ElementName(Element.Petir) + ", " +
                Theme.ElementName(Element.Air) + ", " +
                Theme.ElementName(Element.Daun) + ") ditambah 4 kartu " + mask + ".\n" +
                mask + " adalah kartu wild: selalu dihitung sebagai elemen yang kamu klaim, jadi taruhan berisi Topeng tetap jujur.\n\n" +

                H("Persiapan") +
                "Kartu dikocok dan dibagi rata: 13 kartu per pemain. Setiap pemain mendapat <b>2 peluru</b>.\n\n" +

                H("Giliran") +
                "• <b>Pemimpin ronde</b> menaruh 1–3 kartu tertutup lalu mengumumkan klaim, misalnya \"2 × Bulan\". Elemen itu menjadi <b>elemen ronde</b>.\n" +
                "• Pemain berikutnya (searah jarum jam) harus menaruh 1–3 kartu dengan klaim elemen yang sama, jujur atau bohong, atau memilih <b>Pass</b>.\n" +
                "• Jumlah kartu selalu terlihat semua orang. Yang bisa dibohongi hanya elemennya.\n\n" +

                H("Menembak: BLUFF!") +
                "Setiap kali ada yang menaruh kartu, ada <b>jendela tembak</b> beberapa detik. Pemain lain yang masih punya peluru boleh menekan BLUFF!. Penembak tercepat yang dihitung. Kartu yang baru ditaruh lalu dibuka:\n" +
                "• " + lie + " (ada kartu yang bukan elemen klaim dan bukan Topeng): penaruh mengambil <b>semua</b> kartu di tumpukan tengah. " + refund + "\n" +
                "• " + honest + ": " + wrongShot + "\n\n" +

                H("Hangus") +
                "Kalau semua pemain lain Pass berturut-turut, tumpukan tengah <b>hangus</b> (dibuang dari permainan). Pemain terakhir yang menaruh memimpin ronde baru dengan elemen bebas.\n\n" +

                PowersSection() +

                H("Menang") +
                "Habiskan kartumu, dan taruhan terakhirmu harus lolos dari tembakan. Kalau ketahuan bohong di kartu terakhir, kamu mengambil tumpukan dan permainan berlanjut.\n\n" +

                H("Tips") +
                "• Simpan Topeng untuk taruhan terakhir, karena dijamin jujur.\n" +
                "• Hitung kartu: tiap elemen hanya punya 12 kartu sah (8 + 4 Topeng). Kalau klaim di meja sudah terlalu banyak, pasti ada yang bohong.\n" +
                "• Peluru itu langka. Lawan yang pelurunya habis tidak bisa menembakmu.\n" +
                "• Bohong saat tumpukan masih kecil itu murah. Bohong saat tumpukan besar itu taruhan besar.\n\n" +

                H("Kontrol") +
                "Klik kartu di tanganmu untuk memilih • Enter = Taruh • P = Pass • Spasi / B = BLUFF! • Esc = Jeda";
        }

        static string PowersSection()
        {
            if (!GameSettings.EnablePowers)
                return "";
            var sb = new System.Text.StringBuilder();
            sb.Append(H("Kartu Kuasa"));
            sb.Append("Setiap pemain mendapat <b>1 Kartu Kuasa</b> acak (rahasia, berbeda-beda) yang hanya bisa dipakai <b>sekali</b>. Kartumu terlihat di kiri bawah layar.\n");
            foreach (var power in Powers.All)
                sb.Append("• <b>").Append(PowerText.Name(power)).Append("</b>: ").Append(PowerText.Long(power)).Append('\n');
            sb.Append('\n');
            return sb.ToString();
        }

        static string H(string title) => "<size=32><b>" + title + "</b></size>\n";
    }
}
