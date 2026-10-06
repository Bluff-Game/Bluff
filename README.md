<p align="center">
  <img src="media/bluf-logo.svg" alt="BLUF" width="400">
</p>

<h1 align="center">BLUF: Topeng & Elemen</h1>

<p align="center">Game kartu tipu-tipuan 3D untuk empat pemain, dibuat dengan Unity.</p>

Duduk di meja bersama pemain bertopeng, taruh kartu tertutup, lalu klaim elemennya. Percayai lawan atau gunakan peluru untuk menantang kebohongannya. Tujuannya sederhana: habiskan kartu di tangan dan lolos dari tuduhan terakhir.

## Fitur

- Permainan orang pertama dengan ruangan, karakter bertopeng, dan kartu 3D.
- Mode offline melawan tiga komputer, dengan tingkat kesulitan Mudah, Normal, dan Sulit.
- Enam elemen, kartu Topeng, serta enam jenis Kartu Kuasa.
- Tutorial awal interaktif dan tombol **Panduan** untuk belajar ulang.
- Pilihan kartu, klaim, dan tindakan yang menyesuaikan giliran pemain.
- Avatar karakter, gerakan tangan, dan animasi tembakan.
- Kontrol kamera untuk melihat sekeliling meja.
- Implementasi room internet dengan kode undangan, kesiapan pemain, dan bot untuk kursi kosong.

> Proyek masih dalam pengembangan. Room internet memerlukan konfigurasi Unity Gaming Services; koneksi Relay lintas jaringan belum diverifikasi. WebGL belum menjadi target rilis yang terverifikasi.

## Cara bermain

Aturan bawaan untuk empat pemain:

1. Setiap pemain mendapat **13 kartu**, **2 peluru**, dan **1 Kartu Kuasa** ketika kuasa diaktifkan.
2. Pemimpin ronde memilih **1–3 kartu** dan mengklaim salah satu elemen. Kartu yang ditaruh boleh sesuai klaim atau berupa kebohongan.
3. Pemain berikutnya mengikuti elemen ronde atau memilih **Lewati**. Pemimpin ronde tidak boleh melewati giliran.
4. Setelah kartu ditaruh, pemain lain dapat memilih **BLUFF!** untuk menantang atau **Loloskan** untuk membiarkannya.
5. Jika taruhan bohong, penaruh mengambil tumpukan dan peluru penantang kembali. Jika taruhan jujur, penantang kehilangan peluru dan mengambil kartu yang dituduh.
6. Jika semua pemain lain berturut-turut melewati giliran, tumpukan hangus dan pemain terakhir yang menaruh kartu memimpin ronde baru.
7. Pemain menang ketika kartu terakhirnya lolos atau terbukti jujur saat ditantang.

**Topeng** cocok dengan elemen apa pun. **Kartu Kuasa** hanya dapat digunakan sekali, sesuai syarat masing-masing. Pengaturan dapat mengubah pengembalian peluru, hukuman salah tembak, dan penggunaan kuasa; tutorial mengikuti aturan yang aktif.

## Membuka proyek

### Kebutuhan

- Unity Hub.
- **Unity 6.3 LTS — 6000.3.25f1**.
- Koneksi internet untuk mengunduh paket pada pembukaan pertama.

### Langkah

1. Tambahkan folder repositori ini melalui **Unity Hub → Add project from disk**.
2. Tunggu Unity menyelesaikan import aset dan pemulihan paket.
3. Buka `Assets/Bluff/Scenes/MainMenu.unity`.
4. Tekan **Play**. Gunakan rasio **16:9** sebagai tampilan utama.
5. Jika scene atau Build Settings belum tersedia, jalankan **Bluff → Setup Scenes + Build Settings**.

Scene `MainMenu`, `Loading`, dan `Game` sudah disertakan. Editor helper hanya menyiapkan scene atau registrasi build yang belum tersedia.

## Room internet

Mode internet menggunakan **Netcode for GameObjects**, **Unity Transport**, **Relay**, dan **Anonymous Authentication**. Host menjalankan aturan permainan; setiap pemain menerima informasi kartu yang memang boleh dilihatnya.

### Konfigurasi pengembang

1. Hubungkan proyek ke Unity Cloud melalui **Edit → Project Settings → Services**, menggunakan project dan organization milik Anda.
2. Aktifkan layanan Authentication dan Relay yang diperlukan. Pastikan pengaturan layanan konsisten untuk seluruh build pemain.
3. Periksa kuota dan biaya layanan sebelum membagikan build publik.
4. Jalankan pengujian host dan client pada perangkat serta jaringan berbeda.

Tidak perlu memasukkan password Unity, service-account key, atau token privat ke source code. Jika project belum terhubung, aplikasi menampilkan pesan konfigurasi dan tidak membuat room palsu.

### Alur pemain

- Host memilih **Main online → Buat room**, lalu membagikan kode room.
- Pemain lain memilih **Gabung**, memasukkan kode, lalu menyatakan **Siap**.
- Host memulai pertandingan setelah pemain siap. Kursi kosong diisi bot hingga total empat pemain.
- Menu jeda online tidak menghentikan permainan bersama.
- Koneksi terputus mengakhiri pertandingan dengan pemberitahuan; host migration dan reconnect di tengah pertandingan belum tersedia.

## Kontrol

| Aksi | Kontrol |
| --- | --- |
| Pilih atau batalkan pilihan kartu | Klik kiri kartu |
| Periksa kartu | Arahkan pointer ke kartu |
| Taruh kartu | Tombol **Taruh kartu** atau **Enter** |
| Lewati giliran | Tombol **Lewati** atau **P** |
| Tantang taruhan | Tombol **BLUFF!**, **B**, atau **Spasi** |
| Gunakan kuasa | Buka informasi kuasa, lalu **Gunakan** |
| Lihat sekeliling | Tahan klik kanan dan gerakkan mouse |
| Kembalikan arah pandang | **R** |
| Pelajari aturan | **Panduan** |
| Buka menu jeda | **Esc** |

## Struktur proyek

```text
Assets/Bluff/
├── Editor/                 Penyiapan scene dan build settings
├── Resources/
│   ├── BluffShaders/       Shader dunia dan efek
│   ├── Brand/              Logo PNG dan SVG
│   └── Fonts/              Font beserta lisensinya
├── Scenes/                 MainMenu, Loading, dan Game
└── Scripts/
    ├── Core/               Aturan permainan dan AI, C# tanpa Unity
    └── Game/
        ├── App/            Pengaturan, input, dan perpindahan layar
        ├── Gameplay/       Controller, presentasi, dan HUD
        ├── Loading/        Alur loading
        ├── Menu/           Menu utama dan room
        ├── Networking/     Relay, protokol, dan otoritas host
        ├── UI/             Komponen UI, avatar, dan tutorial
        └── World/          Karakter, kartu, kamera, dan animasi
Packages/                   Dependensi Unity dan lockfile
ProjectSettings/            Konfigurasi proyek
media/                      Aset untuk README
```

Folder `Library`, `Temp`, `Logs`, `obj`, `UserSettings`, dan file solusi IDE tidak disertakan dalam Git karena dibuat ulang secara lokal.

## Verifikasi dan batasan

- Kompilasi proyek dan pengujian fungsional telah dijalankan dalam salinan Unity terisolasi.
- Aturan permainan telah diuji melalui tes Core dan simulasi bot di workspace pengembangan. Runner tersebut tidak disertakan dalam repositori Unity ini.
- Protokol online dan koneksi lokal NGO telah diuji; hal ini **bukan** bukti bahwa konfigurasi Relay internet sudah aktif.
- Tampilan GPU, seluruh ukuran layar, dan kualitas animasi masih memerlukan pemeriksaan visual. Penyempurnaan keterbacaan kartu dan tata letak masih berlangsung.

## Logo dan tipografi

- Logo transparan PNG: [`Assets/Bluff/Resources/Brand/BlufWordmark.png`](Assets/Bluff/Resources/Brand/BlufWordmark.png).
- Logo vektor SVG: [`Assets/Bluff/Resources/Brand/BlufWordmark.svg`](Assets/Bluff/Resources/Brand/BlufWordmark.svg).
- Tipografi UI: **Barlow**.
- Tipografi judul: **Cormorant Garamond**.

Kedua font disertakan dengan lisensi SIL Open Font License di [`Assets/Bluff/Resources/Fonts`](Assets/Bluff/Resources/Fonts). Lisensi font tidak menetapkan lisensi untuk keseluruhan proyek.
