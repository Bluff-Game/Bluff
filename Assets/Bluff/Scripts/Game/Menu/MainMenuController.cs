using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bluff.Core;
using Bluff.Core.AI;
using UnityEngine;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>
    /// Layar Home: ruang kartu 3D sebagai latar (kamera berputar pelan, tokoh bertopeng "bermain" sendiri),
    /// judul &amp; tombol di kiri, dan dialog (main vs komputer, online, cara bermain, pengaturan).
    /// </summary>
    public sealed class MainMenuController : BluffScreen
    {
        const float ButtonWidth = 440f;
        const float ButtonHeight = 68f;
        const float ColumnX = 350f;

        UIKit.Modal openModal;
        InternetRoom room;
        RectTransform onlineSetup;
        RectTransform onlineLobby;
        InputField onlineName;
        InputField onlineCode;
        Text onlineStatus;
        Text roomCode;
        Text readyLabel;
        Button createRoom;
        Button joinRoom;
        Button copyCode;
        Button startMatch;
        Button ready;
        Button leaveRoom;
        Button onlineClose;
        readonly Text[] seatNames = new Text[4];
        readonly Text[] seatStates = new Text[4];
        bool onlineModal;
        bool requestPending;
        bool startPending;
        bool? requestedReady;
        bool destroyed;
        volatile bool roomChanged;
        string requestStatus;
        string onlineError;
        string onlineNotice;
        RectTransform titleGroup;
        TableScene table;
        Camera cam;
        float orbit = 0.6f;
        readonly List<Card3D> pile = new List<Card3D>();

        static readonly string[] DifficultyDescriptions =
        {
            "Komputer ceroboh: sering bohong tanpa hitung-hitungan dan suka menembak asal.",
            "Komputer wajar: bohong saat tumpukan kecil dan menembak kalau cukup curiga.",
            "Komputer licik: menghitung kartu, mengingat siapa yang pernah bohong, dan jarang membuang peluru.",
        };

        protected override void Awake()
        {
            base.Awake();
            Build3D();
            BuildShade();
            BuildTitle();
            BuildButtons();

            var footer = UIKit.CreateText(Root, "Footer", "v0.2", 20, Theme.Muted, TextAnchor.MiddleLeft);
            footer.rectTransform.Place(UIKit.BottomLeft, new Vector2(ColumnX, 42f), new Vector2(ButtonWidth, 28f));
        }

        void Start()
        {
            StartCoroutine(Director());
            AttachRoom(InternetRoom.Current);
            if (room != null && !room.InMatch && (room.IsConnected || room.IsBusy))
                ShowOnlineInfo();
        }

        async void OnDestroy()
        {
            destroyed = true;
            if (room == null)
                return;
            room.Changed -= OnRoomChanged;
            if (!requestPending && !room.InMatch)
                await CleanupRoomAsync(room);
        }

        static async Task CleanupRoomAsync(InternetRoom service)
        {
            if (service == null || service.InMatch)
                return;
            try
            {
                await service.LeaveAsync();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        void Update()
        {
            float t = Time.unscaledTime;
            if (cam != null)
            {
                orbit += Time.deltaTime * 0.018f;
                var pos = new Vector3(Mathf.Sin(orbit) * 3.1f, 1.95f + Mathf.Sin(t * 0.3f) * 0.08f, -Mathf.Cos(orbit) * 3.1f);
                cam.transform.position = pos;
                var center = new Vector3(0f, 0.85f, 0f);
                var right = Vector3.Cross(Vector3.up, center - pos).normalized;
                // Geser titik pandang ke kiri supaya meja tampil di sisi kanan layar (sisi kiri untuk menu).
                cam.transform.rotation = Quaternion.LookRotation(center - right * 1.05f - pos);
            }

            if (roomChanged)
            {
                roomChanged = false;
                if (!onlineModal && room != null && !room.InMatch && (room.IsConnected || room.IsBusy))
                    ShowOnlineInfo();
                RefreshOnlineRoom();
            }

            if (!GameTutorial.IsShowing && InputCompat.EscapePressed() && openModal != null)
                CloseModal();
        }

        // ------------------------------------------------------------------ latar 3D

        void Build3D()
        {
            table = TableScene.Create(transform, 4, -1, new List<string> { "A", "B", "C", "D" });
            cam = table.Camera;
            cam.fieldOfView = 42f;
            for (int id = 0; id < 4; id++)
            {
                table.SetFanCount(id, 6 + id);
                table.SetBullets(id, 2);
                table.SetPowerCard(id, PowerType.None, false, false);
            }
        }

        /// <summary>Tokoh-tokoh di meja melakukan aksi acak supaya latar terasa hidup.</summary>
        IEnumerator Director()
        {
            var rng = new System.Random();
            yield return new WaitForSeconds(1f);
            while (true)
            {
                yield return new WaitForSeconds(2.2f + (float)rng.NextDouble() * 2f);
                int a = rng.Next(4);
                var actor = table.Character(a);
                if (actor == null)
                    continue;
                int action = rng.Next(5);
                if (action <= 1)
                {
                    StartCoroutine(actor.Reach(table.PileAnchor.position, 0.6f));
                    var card = Card3D.Create(transform, "MenuCard");
                    card.SetHiddenCard();
                    card.transform.position = actor.CardHold.position;
                    var offset = Random.insideUnitCircle * 0.07f;
                    var slot = table.PileAnchor.position + new Vector3(offset.x, pile.Count * 0.0017f, offset.y);
                    StartCoroutine(card.FlyTo(slot, Quaternion.LookRotation(Vector3.up, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward), 0.5f, 0.14f, 20f));
                    pile.Add(card);
                    if (pile.Count > 10)
                    {
                        yield return new WaitForSeconds(0.8f);
                        Fx.Fire(table.PileAnchor.position, 1f, 1.2f);
                        foreach (var c in pile)
                            Destroy(c.gameObject, 0.6f);
                        pile.Clear();
                    }
                }
                else if (action == 2)
                {
                    int b = (a + 1 + rng.Next(3)) % 4;
                    var target = table.Character(b);
                    actor.Aim(target.Head.position);
                    yield return new WaitForSeconds(0.5f);
                    Fx.MuzzleFlash(actor.Muzzle.position, (target.Head.position - actor.Muzzle.position).normalized);
                    Sfx.Play(SfxType.Shoot, 0.25f);
                    StartCoroutine(target.Flinch());
                    StartCoroutine(actor.Recoil());
                    yield return new WaitForSeconds(0.6f);
                    StartCoroutine(rng.Next(2) == 0 ? target.Caught(1.5f) : target.Proud(1.2f));
                    actor.LowerGun();
                }
                else if (action == 3)
                {
                    StartCoroutine(actor.Wave(1f));
                }
                else
                {
                    actor.Celebrate();
                    yield return new WaitForSeconds(1.4f);
                    actor.HoldCards();
                }
            }
        }

        // ------------------------------------------------------------------ layout

        void BuildShade()
        {
            // Gradasi gelap di sisi kiri supaya menu terbaca di atas adegan 3D.
            var shade = UIKit.CreateImage(Root, "Shade", SpriteFactory.HorizontalFade, Theme.Background.WithAlpha(0.92f));
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(0f, 1f);
            shade.rectTransform.pivot = new Vector2(0f, 0.5f);
            shade.rectTransform.sizeDelta = new Vector2(1150f, 0f);
            shade.rectTransform.anchoredPosition = Vector2.zero;
        }

        void BuildTitle()
        {
            titleGroup = UIKit.CreateRect("Title", Root);
            titleGroup.Place(UIKit.Left, new Vector2(ColumnX, 230f), new Vector2(ButtonWidth, 250f));

            var title = UIKit.CreateLogo(titleGroup, "Logo");
            title.rectTransform.Place(UIKit.Center, new Vector2(0f, 28f), new Vector2(472f, 148f));

            var tagline = UIKit.CreateText(titleGroup, "Tagline", "Tipu. Tebak. Tembak.", 24, Theme.Muted, TextAnchor.MiddleLeft);
            tagline.rectTransform.Place(UIKit.Center, new Vector2(0f, -74f), new Vector2(ButtonWidth, 36f));
        }

        void BuildButtons()
        {
            var column = UIKit.CreateRect("Buttons", Root);
            column.Place(UIKit.Left, new Vector2(ColumnX, -128f), new Vector2(ButtonWidth, 416f));

            float y = 156f;
            AddMenuButton(column, "Main vs komputer", y, Theme.Gold, Theme.TextDark, ShowPlaySetup);
            y -= 88f;
            AddMenuButton(column, "Main online", y, Theme.Panel.WithAlpha(0.8f), Theme.Text, ShowOnlineInfo);
            y -= 78f;
            AddMenuButton(column, "Belajar bermain", y, Theme.Panel.WithAlpha(0.8f), Theme.Text, ShowTutorial);
            y -= 78f;
            AddMenuButton(column, "Pengaturan", y, Theme.Panel.WithAlpha(0.8f), Theme.Text, ShowSettings);
            y -= 78f;
            AddMenuButton(column, "Keluar", y, Color.clear, Theme.Muted, Quit);
        }

        Button AddMenuButton(RectTransform parent, string label, float y, Color bg, Color fg, System.Action onClick)
        {
            var button = UIKit.CreateButton(parent, label, label, new Vector2(ButtonWidth, ButtonHeight), bg, fg, onClick, 28, 6f);
            button.GetComponent<RectTransform>().Place(UIKit.Center, new Vector2(0f, y), new Vector2(ButtonWidth, ButtonHeight));
            var text = button.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.Stretch(24f, 24f, 0f, 0f);
            return button;
        }

        // ------------------------------------------------------------------ dialog

        void CloseModal()
        {
            if (openModal == null)
                return;
            if (onlineModal)
            {
                if (requestPending || startPending || (room != null && (room.IsBusy || room.InMatch)))
                    return;
                if (room != null && room.IsConnected)
                {
                    onlineNotice = "Pilih Keluar room untuk meninggalkan room sebelum menutup.";
                    RefreshOnlineRoom();
                    return;
                }
            }
            openModal.Close();
            openModal = null;
            onlineModal = false;
        }

        UIKit.Modal OpenModal(string title, Vector2 size)
        {
            CloseModal();
            if (openModal != null || GameTutorial.IsShowing)
                return null;
            openModal = UIKit.CreateModal(Root, title, size, () => openModal = null);
            return openModal;
        }

        void ShowPlaySetup()
        {
            var modal = OpenModal("Main vs Komputer", new Vector2(900f, 660f));
            if (modal == null)
                return;
            var content = modal.Content;

            var nameLabel = UIKit.CreateText(content, "NameLabel", "Nama kamu", 30, Theme.Muted, TextAnchor.MiddleLeft);
            nameLabel.rectTransform.Place(UIKit.Top, new Vector2(0f, -20f), new Vector2(800f, 40f));
            var input = UIKit.CreateInput(content, GameSettings.PlayerName, "Masukkan nama…", 14, new Vector2(800f, 72f));
            input.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(0f, -86f), new Vector2(800f, 72f));

            var description = UIKit.CreateText(content, "Description", DifficultyDescriptions[(int)GameSettings.Difficulty], 26, Theme.Text.WithAlpha(0.8f), TextAnchor.UpperCenter);
            description.rectTransform.Place(UIKit.Top, new Vector2(0f, -268f), new Vector2(780f, 90f));
            var difficultyRow = UIKit.CreateSelector(content, "Lawan komputer", GameSettings.DifficultyNames, (int)GameSettings.Difficulty, i =>
            {
                GameSettings.Difficulty = (BotDifficulty)i;
                description.text = DifficultyDescriptions[i];
            }, 800f);
            difficultyRow.Place(UIKit.Top, new Vector2(0f, -180f), new Vector2(800f, 76f));

            var powerRow = UIKit.CreateSelector(content, "Kartu Kuasa", new[] { "Nonaktif", "Aktif" }, GameSettings.EnablePowers ? 1 : 0,
                i => GameSettings.EnablePowers = i == 1, 800f);
            powerRow.Place(UIKit.Top, new Vector2(0f, -340f), new Vector2(800f, 76f));

            var info = UIKit.CreateText(content, "Info", "4 pemain  ·  13 kartu  ·  2 peluru  ·  1 Kartu Kuasa per pemain", 24, Theme.Muted);
            info.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 122f), new Vector2(800f, 36f));

            var start = UIKit.CreateButton(content, "Start", "Mulai", new Vector2(420f, 86f), Theme.Gold, Theme.TextDark, () =>
            {
                GameSettings.PlayerName = string.IsNullOrWhiteSpace(input.text) ? "Kamu" : input.text.Trim();
                SceneFlow.GoToGame();
            }, 38, 24f);
            start.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 46f), new Vector2(420f, 86f));
        }

        void AttachRoom(InternetRoom service)
        {
            if (ReferenceEquals(room, service))
                return;
            if (room != null)
                room.Changed -= OnRoomChanged;
            room = service;
            if (room != null)
                room.Changed += OnRoomChanged;
        }

        void OnRoomChanged() => roomChanged = true;

        void ShowOnlineInfo()
        {
            if (GameTutorial.IsShowing)
                return;
            if (onlineModal)
            {
                RefreshOnlineRoom();
                return;
            }
            CloseModal();
            if (openModal != null)
                return;
            AttachRoom(InternetRoom.Current);
            onlineError = null;
            onlineNotice = null;
            requestedReady = null;
            startPending = false;
            openModal = UIKit.CreateModal(Root, "Main online", new Vector2(980f, 920f), null);
            onlineModal = true;
            var content = openModal.Content;
            const float width = 884f;
            onlineClose = UIKit.CreateButton(openModal.Panel, "Close", "X", new Vector2(64f, 64f), Theme.PanelLight, Theme.Text, CloseModal, 30, 32f);
            onlineClose.GetComponent<RectTransform>().Place(UIKit.TopRight, new Vector2(-50f, -50f), new Vector2(64f, 64f));

            onlineSetup = UIKit.CreateRect("RoomSetup", content);
            onlineSetup.Stretch();
            var nameLabel = UIKit.CreateText(onlineSetup, "NameLabel", "Nama kamu", 28, Theme.Muted, TextAnchor.MiddleLeft);
            nameLabel.rectTransform.Place(UIKit.Top, new Vector2(0f, -26f), new Vector2(width, 40f));
            onlineName = UIKit.CreateInput(onlineSetup, GameSettings.PlayerName, "Masukkan nama…", 14, new Vector2(width, 72f));
            onlineName.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(0f, -88f), new Vector2(width, 72f));
            var codeLabel = UIKit.CreateText(onlineSetup, "CodeLabel", "Kode room untuk bergabung", 28, Theme.Muted, TextAnchor.MiddleLeft);
            codeLabel.rectTransform.Place(UIKit.Top, new Vector2(0f, -178f), new Vector2(width, 40f));
            onlineCode = UIKit.CreateInput(onlineSetup, "", "Masukkan kode dari teman…", 64, new Vector2(width, 72f));
            onlineCode.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(0f, -240f), new Vector2(width, 72f));
            onlineName.onValueChanged.AddListener(_ => RefreshOnlineRoom());
            onlineCode.onValueChanged.AddListener(_ => RefreshOnlineRoom());
            createRoom = UIKit.CreateButton(onlineSetup, "CreateRoom", "Buat room", new Vector2(422f, 76f), Theme.Gold, Theme.TextDark, () => ConnectRoom(true), 30);
            createRoom.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(-231f, -352f), new Vector2(422f, 76f));
            joinRoom = UIKit.CreateButton(onlineSetup, "JoinRoom", "Gabung", new Vector2(422f, 76f), Theme.PanelLight, Theme.Text, () => ConnectRoom(false), 30);
            joinRoom.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(231f, -352f), new Vector2(422f, 76f));

            onlineLobby = UIKit.CreateRect("RoomLobby", content);
            onlineLobby.Stretch();
            roomCode = UIKit.CreateText(onlineLobby, "RoomCode", "", 34, Theme.Gold, TextAnchor.MiddleLeft);
            roomCode.supportRichText = false;
            roomCode.resizeTextForBestFit = true;
            roomCode.resizeTextMinSize = 20;
            roomCode.resizeTextMaxSize = 34;
            roomCode.rectTransform.Place(UIKit.Top, new Vector2(-116f, -38f), new Vector2(652f, 64f));
            copyCode = UIKit.CreateButton(onlineLobby, "CopyCode", "Salin kode", new Vector2(204f, 64f), Theme.PanelLight, Theme.Text, () =>
            {
                if (room == null || !room.IsConnected || string.IsNullOrEmpty(room.RoomCode))
                    return;
                GUIUtility.systemCopyBuffer = room.RoomCode;
                onlineNotice = "Kode room disalin.";
                RefreshOnlineRoom();
            }, 26);
            copyCode.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(340f, -38f), new Vector2(204f, 64f));
            for (int i = 0; i < seatNames.Length; i++)
            {
                var row = UIKit.CreatePanel(onlineLobby, "Seat " + i, Theme.Background, 6f);
                row.rectTransform.Place(UIKit.Top, new Vector2(0f, -154f - i * 82f), new Vector2(width, 74f));
                seatNames[i] = UIKit.CreateText(row.transform, "Name", "", 26, Theme.Text, TextAnchor.MiddleLeft);
                seatNames[i].supportRichText = false;
                seatNames[i].resizeTextForBestFit = true;
                seatNames[i].resizeTextMinSize = 18;
                seatNames[i].resizeTextMaxSize = 26;
                seatNames[i].rectTransform.Stretch(20f, 384f, 10f, 10f);
                seatStates[i] = UIKit.CreateText(row.transform, "State", "", 23, Theme.Muted, TextAnchor.MiddleRight);
                seatStates[i].rectTransform.Stretch(510f, 20f, 10f, 10f);
            }
            var botNote = UIKit.CreateText(onlineLobby, "BotFill", "Kursi kosong diisi komputer saat permainan dimulai.", 23, Theme.Muted);
            botNote.rectTransform.Place(UIKit.Top, new Vector2(0f, -476f), new Vector2(width, 46f));

            var statusArea = UIKit.CreateRect("RoomStatus", content);
            statusArea.Place(UIKit.Bottom, new Vector2(0f, 178f), new Vector2(width, 128f));
            onlineStatus = UIKit.CreateScrollText(statusArea, "", 23);
            onlineStatus.supportRichText = false;
            leaveRoom = UIKit.CreateButton(content, "LeaveRoom", "Tutup", new Vector2(422f, 76f), Theme.PanelLight, Theme.Text, LeaveRoom, 28);
            leaveRoom.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(-231f, 40f), new Vector2(422f, 76f));
            startMatch = UIKit.CreateButton(content, "StartMatch", "Mulai", new Vector2(422f, 76f), Theme.Gold, Theme.TextDark, StartOnlineMatch, 32);
            startMatch.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(231f, 40f), new Vector2(422f, 76f));
            ready = UIKit.CreateButton(content, "Ready", "Siap", new Vector2(422f, 76f), Theme.Gold, Theme.TextDark, ToggleReady, 32);
            ready.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(231f, 40f), new Vector2(422f, 76f));
            readyLabel = ready.GetComponentInChildren<Text>();
            RefreshOnlineRoom();
        }

        bool TryGetSeat(int id, out RoomSeat seat)
        {
            if (room != null && room.Seats != null)
            {
                foreach (var candidate in room.Seats)
                {
                    if (candidate.SeatId == id)
                    {
                        seat = candidate;
                        return true;
                    }
                }
            }
            seat = default;
            return false;
        }

        bool CanStartMatch()
        {
            if (room == null || !room.IsConnected || !room.IsHost || room.IsBusy || room.InMatch || requestPending || startPending)
                return false;
            if (!TryGetSeat(room.LocalSeat, out var local) || !local.Connected || local.IsBot)
                return false;
            foreach (var seat in room.Seats)
            {
                if (!seat.IsBot && (!seat.Connected || !seat.Ready))
                    return false;
            }
            return true;
        }

        void RefreshOnlineRoom()
        {
            if (destroyed || !onlineModal || openModal == null || openModal.Root == null)
                return;
            bool connected = room != null && room.IsConnected;
            bool inMatch = room != null && room.InMatch;
            bool host = connected && room.IsHost;
            bool serviceBusy = room != null && room.IsBusy;
            string error = !string.IsNullOrWhiteSpace(onlineError) ? onlineError : room != null ? room.Error : null;
            if (!connected || (!serviceBusy && !string.IsNullOrWhiteSpace(error)))
            {
                requestedReady = null;
                startPending = false;
            }
            bool localConnected = connected && TryGetSeat(room.LocalSeat, out var localSeat) && localSeat.Connected && !localSeat.IsBot;
            bool localReady = localConnected && TryGetSeat(room.LocalSeat, out var local) && local.Ready;
            if (requestedReady.HasValue && localConnected && requestedReady.Value == localReady)
                requestedReady = null;
            bool busy = requestPending || serviceBusy || startPending || inMatch;
            onlineSetup.gameObject.SetActive(!connected && !inMatch);
            onlineLobby.gameObject.SetActive(connected || inMatch);
            openModal.Title.text = connected || inMatch ? "Room online" : "Main online";
            onlineName.interactable = !busy && !connected;
            onlineCode.interactable = !busy && !connected;
            createRoom.interactable = !busy && !connected;
            joinRoom.interactable = !busy && !connected && !string.IsNullOrWhiteSpace(onlineCode.text);
            onlineClose.interactable = !busy && !connected;
            leaveRoom.interactable = !busy;
            leaveRoom.GetComponentInChildren<Text>().text = connected ? "Keluar room" : "Tutup";
            copyCode.interactable = connected && !busy && !string.IsNullOrEmpty(room.RoomCode);
            roomCode.text = "Kode room: " + (room != null && !string.IsNullOrEmpty(room.RoomCode) ? room.RoomCode : "Menunggu…");
            startMatch.gameObject.SetActive(host);
            startMatch.interactable = CanStartMatch();
            ready.gameObject.SetActive(connected && !host);
            ready.interactable = localConnected && !busy && !requestedReady.HasValue;
            readyLabel.text = requestedReady.HasValue ? "Menyimpan…" : localReady ? "Batal siap" : "Siap";

            for (int i = 0; i < seatNames.Length; i++)
            {
                bool found = TryGetSeat(i, out var seat);
                string name = "Kosong";
                string state = "Akan diisi komputer";
                Color color = Theme.Muted;
                if (found && seat.IsBot)
                {
                    name = string.IsNullOrWhiteSpace(seat.Name) ? "Komputer" : seat.Name;
                    state = "Komputer · otomatis";
                    color = Theme.Green;
                }
                else if (found && seat.Connected)
                {
                    name = string.IsNullOrWhiteSpace(seat.Name) ? "Pemain" : seat.Name;
                    if (i == room.LocalSeat)
                        name += host ? " (kamu, host)" : " (kamu)";
                    state = seat.Ready ? "Terhubung · siap" : "Terhubung · belum siap";
                    color = seat.Ready ? Theme.Green : Theme.Muted;
                }
                else if (found && !string.IsNullOrWhiteSpace(seat.Name))
                {
                    name = seat.Name;
                    state = "Tidak terhubung · belum siap";
                }
                seatNames[i].text = (i + 1) + ". " + name;
                seatStates[i].text = state;
                seatStates[i].color = color;
            }

            string status = room != null ? room.Status : null;
            if (inMatch)
                status = "Memasuki permainan…";
            else if (requestPending)
                status = requestStatus + (string.IsNullOrWhiteSpace(status) ? "" : "\n" + status);
            else if (startPending)
                status = "Menunggu permainan dimulai…";
            else if (serviceBusy && string.IsNullOrWhiteSpace(status))
                status = "Menghubungkan…";
            if (string.IsNullOrWhiteSpace(status))
                status = connected ? host ? "Tunggu semua teman siap, lalu pilih Mulai." : "Pilih Siap setelah kamu siap bermain." : "Buat room atau masukkan kode dari teman untuk bergabung.";
            if (!string.IsNullOrWhiteSpace(error) && !status.Contains(error))
                status += "\n" + error;
            if (!string.IsNullOrWhiteSpace(onlineNotice))
                status += "\n" + onlineNotice;
            onlineStatus.text = status;
            onlineStatus.color = string.IsNullOrWhiteSpace(error) ? Theme.Muted : Theme.Red;
        }

        async void ConnectRoom(bool create)
        {
            if (!onlineModal || requestPending || (room != null && (room.IsBusy || room.IsConnected || room.InMatch)))
                return;
            string code = onlineCode.text.Trim();
            if (!create && code.Length == 0)
                return;
            string name = string.IsNullOrWhiteSpace(onlineName.text) ? "Kamu" : onlineName.text.Trim();
            requestPending = true;
            requestStatus = create ? "Membuat room…" : "Bergabung ke room…";
            onlineError = null;
            onlineNotice = null;
            RefreshOnlineRoom();
            InternetRoom service = null;
            try
            {
                GameSettings.PlayerName = name;
                service = InternetRoom.Ensure();
                AttachRoom(service);
                if (service == null)
                    throw new System.InvalidOperationException("Layanan room tidak tersedia.");
                if (create)
                    await service.CreateRoomAsync(GameSettings.PlayerName);
                else
                    await service.JoinRoomAsync(code, GameSettings.PlayerName);
            }
            catch (System.Exception exception)
            {
                onlineError = exception.Message;
            }
            finally
            {
                requestPending = false;
                requestStatus = null;
                if (!destroyed)
                    RefreshOnlineRoom();
            }
            if (destroyed)
                await CleanupRoomAsync(service);
        }

        async void LeaveRoom()
        {
            if (!onlineModal || requestPending || startPending || (room != null && (room.IsBusy || room.InMatch)))
                return;
            if (room == null)
            {
                CloseModal();
                return;
            }
            var service = room;
            requestPending = true;
            requestStatus = "Meninggalkan room…";
            onlineError = null;
            onlineNotice = null;
            requestedReady = null;
            RefreshOnlineRoom();
            try
            {
                await service.LeaveAsync();
            }
            catch (System.Exception exception)
            {
                onlineError = exception.Message;
            }
            finally
            {
                requestPending = false;
                requestStatus = null;
                if (!destroyed)
                    RefreshOnlineRoom();
            }
            if (destroyed)
            {
                await CleanupRoomAsync(service);
                return;
            }
            if (!service.IsConnected && !service.IsBusy && !service.InMatch && string.IsNullOrWhiteSpace(onlineError) && string.IsNullOrWhiteSpace(service.Error))
                CloseModal();
        }

        void ToggleReady()
        {
            if (room == null || !room.IsConnected || room.IsHost || room.IsBusy || room.InMatch || requestPending || requestedReady.HasValue)
                return;
            if (!TryGetSeat(room.LocalSeat, out var seat) || !seat.Connected || seat.IsBot)
                return;
            requestedReady = !seat.Ready;
            onlineError = null;
            onlineNotice = null;
            ready.interactable = false;
            try
            {
                room.SetReady(requestedReady.Value);
            }
            catch (System.Exception exception)
            {
                onlineError = exception.Message;
                requestedReady = null;
            }
            RefreshOnlineRoom();
        }

        void StartOnlineMatch()
        {
            if (!CanStartMatch())
                return;
            startPending = true;
            onlineError = null;
            onlineNotice = null;
            startMatch.interactable = false;
            try
            {
                room.StartMatch();
            }
            catch (System.Exception exception)
            {
                onlineError = exception.Message;
                startPending = false;
            }
            RefreshOnlineRoom();
        }

        void ShowTutorial()
        {
            if (GameTutorial.IsShowing)
                return;
            CloseModal();
            if (openModal == null)
                GameTutorial.Show(Root, null);
        }

        void ShowRules()
        {
            var modal = OpenModal("Cara Bermain", new Vector2(1240f, 900f));
            if (modal != null)
                UIKit.CreateScrollText(modal.Content, RulesText.Build(), 28);
        }

        void ShowSettings()
        {
            var modal = OpenModal("Pengaturan", new Vector2(980f, 820f));
            if (modal == null)
                return;
            var list = UIKit.CreateRect("List", modal.Content);
            list.Stretch();
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(0, 0, 6, 0);

            const float width = 860f;
            var windowNames = new string[GameSettings.ShootWindowOptions.Length];
            for (int i = 0; i < windowNames.Length; i++)
                windowNames[i] = GameSettings.ShootWindowOptions[i].ToString("0") + " detik";
            UIKit.CreateSelector(list, "Jendela tembak", windowNames, GameSettings.ShootWindowIndex, i => GameSettings.ShootWindowIndex = i, width);
            UIKit.CreateSelector(list, "Kecepatan permainan", GameSettings.SpeedNames, GameSettings.SpeedIndex, i => GameSettings.SpeedIndex = i, width);

            var volumeNames = new string[GameSettings.VolumeOptions.Length];
            for (int i = 0; i < volumeNames.Length; i++)
                volumeNames[i] = Mathf.RoundToInt(GameSettings.VolumeOptions[i] * 100f) + "%";
            UIKit.CreateSelector(list, "Efek suara", volumeNames, GameSettings.VolumeIndex, i =>
            {
                GameSettings.VolumeIndex = i;
                Sfx.Play(SfxType.Card);
            }, width);

            UIKit.CreateSelector(list, "Kartu Kuasa", new[] { "Nonaktif", "Aktif" }, GameSettings.EnablePowers ? 1 : 0,
                i => GameSettings.EnablePowers = i == 1, width);
            UIKit.CreateSelector(list, "Tembakan jitu: peluru kembali", new[] { "Tidak", "Ya" }, GameSettings.RefundBulletOnHit ? 1 : 0,
                i => GameSettings.RefundBulletOnHit = i == 1, width);
            UIKit.CreateSelector(list, "Salah tembak: ambil", new[] { "Kartu tuduhan", "Semua tumpukan" }, GameSettings.ClassicWrongShotPenalty ? 1 : 0,
                i => GameSettings.ClassicWrongShotPenalty = i == 1, width);

            var note = UIKit.CreateText(list, "Note",
                "Aturan bawaan: Kartu Kuasa aktif, peluru kembali saat tembakan jitu, dan salah tembak hanya mengambil kartu yang dituduh.",
                23, Theme.Muted, TextAnchor.MiddleCenter, FontStyle.Italic);
            note.rectTransform.sizeDelta = new Vector2(width, 80f);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
