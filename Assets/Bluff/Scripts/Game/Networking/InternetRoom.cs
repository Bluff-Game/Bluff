using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bluff.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Bluff.Game
{
    public sealed class InternetRoom : MonoBehaviour
    {
        const string Channel = "bluff.room.v1";
        const double ReadyTimeout = 45;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        readonly List<RoomSeat> seats = new List<RoomSeat>();
        readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        readonly Queue<Inbound> incoming = new Queue<Inbound>();
        readonly Queue<RoomPacket> presentation = new Queue<RoomPacket>();
        readonly Queue<RoomPacket> intents = new Queue<RoomPacket>();
        NetworkManager network;
        UnityTransport transport;
        CancellationTokenSource cancellation;
        Task serviceTask;
        Task connectionTask;
        bool ownsAuthentication;
        bool stopping;
        bool quitting;
        bool sceneAttached;
        bool inputOpen;
        bool bootstrapped;
        bool returnRequested;
        int revision;
        int localSequence;
        int receivedRevision;
        int lastEvent;
        double barrierDeadline;
        double sceneDeadline;
        InternetMatch match;

        sealed class Peer
        {
            public int Seat;
            public bool SceneReady;
            public int Presented;
            public double BudgetAt;
            public int Budget;
            public IntentGate Gate = new IntentGate();
        }

        struct Inbound
        {
            public ulong Sender;
            public RoomPacket Packet;
        }

        public static InternetRoom Current { get; private set; }
        public static InternetRoom Ensure()
        {
            if (Current == null)
                new GameObject("Internet Room").AddComponent<InternetRoom>();
            return Current;
        }

        public bool IsBusy { get; private set; }
        public bool IsHost => network != null && network.IsHost;
        public bool IsConnected => network != null && network.IsListening && network.IsConnectedClient && LocalSeat >= 0;
        public bool InMatch { get; private set; }
        public string RoomCode { get; private set; } = "";
        public string Status { get; private set; } = "";
        public string Error { get; private set; } = "";
        public int LocalSeat { get; private set; } = -1;
        public IReadOnlyList<RoomSeat> Seats => seats.AsReadOnly();
        public event Action Changed;
        internal GameEngine HostEngine => match?.Engine;
        internal MatchStats HostStats => match?.Stats;
        internal bool InputOpen => inputOpen;
        internal int Revision => revision;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            DontDestroyOnLoad(gameObject);
        }

        public Task CreateRoomAsync(string name) => BeginConnection(true, "", name);
        public Task JoinRoomAsync(string code, string name) => BeginConnection(false, code, name);

        Task BeginConnection(bool host, string code, string name)
        {
            if (connectionTask != null && !connectionTask.IsCompleted) return connectionTask;
            connectionTask = ConnectAsync(host, code, name);
            return connectionTask;
        }

        async Task ConnectAsync(bool host, string code, string name)
        {
            if (IsBusy || IsConnected || stopping)
                return;
            Error = "";
            if (!Guid.TryParse(Application.cloudProjectId, out var projectId) || projectId == Guid.Empty)
            {
                SetError("ROOM INTERNET belum dikonfigurasi. Pemilik proyek perlu menautkan Unity Project ID di Project Settings > Services, lalu mengaktifkan Authentication anonim dan Relay di Unity Dashboard.");
                return;
            }
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!host && !ValidCode(code))
            {
                SetError("Kode room tidak valid. Masukkan kode Relay dari host.");
                return;
            }
            if (serviceTask != null && !serviceTask.IsCompleted)
            {
                SetError("Permintaan layanan sebelumnya masih dibatalkan. Tunggu sebentar lalu coba lagi.");
                return;
            }
            IsBusy = true;
            Status = host ? "Membuat room…" : "Menghubungkan ke room…";
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var token = cancellation.Token;
            Changed?.Invoke();
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await AwaitService(UnityServices.InitializeAsync(), token);
                token.ThrowIfCancellationRequested();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    ownsAuthentication = true;
                    await AwaitService(AuthenticationService.Instance.SignInAnonymouslyAsync(), token);
                }
                token.ThrowIfCancellationRequested();
                BuildNetwork(RoomProtocol.CleanName(name));
                string connection = Application.platform == RuntimePlatform.WebGLPlayer ? "wss" : "dtls";
                transport.UseWebSockets = connection == "wss";
                if (host)
                {
                    var allocationTask = RelayService.Instance.CreateAllocationAsync(3);
                    await AwaitService(allocationTask, token);
                    var allocation = await allocationTask;
                    token.ThrowIfCancellationRequested();
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connection));
                    var codeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    await AwaitService(codeTask, token);
                    string joinCode = await codeTask;
                    token.ThrowIfCancellationRequested();
                    InitializeSeats(name);
                    LocalSeat = 0;
                    peers[NetworkManager.ServerClientId] = new Peer { Seat = 0 };
                    if (!network.StartHost()) throw new InvalidOperationException();
                    RegisterMessages();
                    RoomCode = joinCode;
                    BroadcastLobby();
                }
                else
                {
                    var allocationTask = RelayService.Instance.JoinAllocationAsync(code);
                    await AwaitService(allocationTask, token);
                    var allocation = await allocationTask;
                    token.ThrowIfCancellationRequested();
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connection));
                    if (!network.StartClient()) throw new InvalidOperationException();
                    RegisterMessages();
                    while (!IsConnected)
                    {
                        token.ThrowIfCancellationRequested();
                        if (network == null || !network.IsListening) throw new InvalidOperationException();
                        await Task.Delay(50, token);
                    }
                }
                token.ThrowIfCancellationRequested();
                Status = "Menunggu pemain siap.";
            }
            catch (OperationCanceledException)
            {
                if (!stopping) SetError("Koneksi dibatalkan atau melewati batas waktu. Periksa internet dan coba lagi.");
                CleanupNetwork();
                await ReleaseAuthentication();
            }
            catch (Exception)
            {
                if (!stopping && string.IsNullOrEmpty(Error))
                    SetError("Tidak dapat terhubung ke Relay. Periksa internet, kode room, serta konfigurasi Authentication anonim dan Relay proyek Unity.");
                CleanupNetwork();
                await ReleaseAuthentication();
            }
            finally
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }

        async Task AwaitService(Task task, CancellationToken token)
        {
            serviceTask = task;
            if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, token)) != task)
            {
                _ = ObserveService(task);
                token.ThrowIfCancellationRequested();
            }
            await task;
            token.ThrowIfCancellationRequested();
        }

        async Task ObserveService(Task task)
        {
            try { await task; }
            catch (Exception) { }
            if (!IsConnected && !IsBusy) await ReleaseAuthentication();
        }

        void BuildNetwork(string name)
        {
            if (NetworkManager.Singleton != null) throw new InvalidOperationException();
            var go = new GameObject("Room Transport");
            DontDestroyOnLoad(go);
            transport = go.AddComponent<UnityTransport>();
            transport.MaxPayloadSize = RoomProtocol.MaxPayload + 1024;
            transport.MaxSendQueueSize = 256 * 1024;
            transport.DisconnectTimeoutMS = 10000;
            network = go.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = true,
                ForceSamePrefabs = false,
                ProtocolVersion = RoomProtocol.Version,
                ConnectionData = Utf8.GetBytes(RoomProtocol.Version + ":" + name),
                ClientConnectionBufferTimeout = 15
            };
            network.ConnectionApprovalCallback += Approve;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += TransportFailed;
        }

        void RegisterMessages() => network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);

        void InitializeSeats(string name)
        {
            seats.Clear();
            for (int i = 0; i < 4; i++)
                seats.Add(new RoomSeat { SeatId = i, Name = i == 0 ? RoomProtocol.CleanName(name) : "Bot " + i, Connected = i == 0, Ready = i == 0, IsBot = i != 0 });
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }
            response.Approved = false;
            response.Reason = "Room penuh, sudah dimulai, atau versi game berbeda.";
            if (InMatch || stopping || peers.Count >= 4 || request.Payload == null || request.Payload.Length > 100)
                return;
            string text;
            try { text = Utf8.GetString(request.Payload); }
            catch (ArgumentException) { return; }
            string prefix = RoomProtocol.Version + ":";
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) return;
            int seat = -1;
            for (int i = 1; i < 4; i++)
            {
                bool taken = false;
                foreach (var peer in peers.Values) if (peer.Seat == i) taken = true;
                if (!taken) { seat = i; break; }
            }
            if (seat < 0) return;
            peers[request.ClientNetworkId] = new Peer { Seat = seat };
            seats[seat] = new RoomSeat { SeatId = seat, Name = RoomProtocol.CleanName(text.Substring(prefix.Length)), IsBot = false };
            response.Approved = true;
        }

        void Connected(ulong id)
        {
            if (IsHost && peers.TryGetValue(id, out var peer))
            {
                seats[peer.Seat].Connected = true;
                BroadcastLobby();
            }
        }

        void Disconnected(ulong id)
        {
            if (stopping || quitting) return;
            if (IsHost)
            {
                if (id == NetworkManager.ServerClientId) return;
                if (!peers.TryGetValue(id, out var peer)) return;
                peers.Remove(id);
                if (InMatch)
                {
                    Abort("Pemain terputus. Pertandingan dihentikan; buat room baru untuk bermain lagi.");
                    return;
                }
                seats[peer.Seat] = new RoomSeat { SeatId = peer.Seat, Name = "Bot " + peer.Seat, IsBot = true };
                BroadcastLobby();
            }
            else
            {
                Abort("Koneksi ke host terputus atau room tidak tersedia. Gabung kembali dengan kode room yang aktif.");
            }
        }

        void TransportFailed() => Abort("Koneksi Relay gagal. Periksa internet lalu buat atau gabung room lagi.");

        void Receive(ulong sender, FastBufferReader reader)
        {
            if (stopping) return;
            bool host = IsHost;
            if (host && !peers.ContainsKey(sender)) return;
            if (!host && sender != NetworkManager.ServerClientId) return;
            if (host)
            {
                var peer = peers[sender];
                double now = Time.realtimeSinceStartupAsDouble;
                if (now - peer.BudgetAt >= 1) { peer.BudgetAt = now; peer.Budget = 0; }
                if (++peer.Budget > 40) { ProtocolFailure(sender); return; }
            }
            int length = reader.Length - reader.Position;
            if (length < 10 || length > (host ? 256 : RoomProtocol.MaxPayload) || incoming.Count >= 128)
            {
                ProtocolFailure(sender);
                return;
            }
            var bytes = new byte[length];
            reader.ReadBytesSafe(ref bytes, length);
            if (!RoomProtocol.TryDecode(bytes, out var packet)) { ProtocolFailure(sender); return; }
            incoming.Enqueue(new Inbound { Sender = sender, Packet = packet });
        }

        void ProtocolFailure(ulong sender)
        {
            if (IsHost && sender != NetworkManager.ServerClientId)
                network.DisconnectClient(sender);
            else
                Abort("Data room tidak valid. Pastikan semua pemain memakai versi game yang sama.");
        }

        void Update()
        {
            int budget = 64;
            while (!stopping && incoming.Count > 0 && budget-- > 0)
            {
                var item = incoming.Dequeue();
                if (IsHost) ProcessHost(item.Sender, item.Packet);
                else ProcessClient(item.Packet);
            }
            if (!InMatch || stopping) return;
            if (IsHost && returnRequested && AllPresented)
            {
                Broadcast(new RoomPacket { Type = RoomMessage.ReturnRoom, Revision = revision });
                ResetToRoom();
                BroadcastLobby();
                return;
            }
            if (IsHost && !AllSceneReady && Time.realtimeSinceStartupAsDouble > sceneDeadline)
                Abort("Pemain tidak selesai memuat meja dalam 45 detik. Coba buat room lagi.");
            else if (IsHost && barrierDeadline > 0 && !AllPresented && Time.realtimeSinceStartupAsDouble > barrierDeadline)
                Abort("Pemain tidak merespons presentasi meja dalam 45 detik. Pertandingan dihentikan.");
        }

        void ProcessHost(ulong sender, RoomPacket p)
        {
            if (!peers.TryGetValue(sender, out var peer)) return;
            switch (p.Type)
            {
                case RoomMessage.Ready:
                    if (!InMatch) { seats[peer.Seat].Ready = p.Flag; BroadcastLobby(); }
                    break;
                case RoomMessage.SceneReady:
                    if (InMatch && p.Revision == revision && barrierDeadline == 0) peer.SceneReady = true;
                    break;
                case RoomMessage.Presented:
                    if (InMatch && peer.SceneReady && p.Revision == revision) peer.Presented = revision;
                    break;
                case RoomMessage.ReturnRoom:
                    if (InMatch && p.Revision == revision && match != null && match.Engine.Phase == GamePhase.GameOver)
                        returnRequested = true;
                    break;
                case RoomMessage.Intent:
                    if (!InMatch || !inputOpen || !peer.Gate.Accept(p.Sequence, p.Revision, revision, Time.realtimeSinceStartupAsDouble))
                    {
                        Reject(peer.Seat, "Aksi sudah kedaluwarsa atau belum waktunya. Coba lagi saat giliran aktif.");
                        break;
                    }
                    p.Seat = peer.Seat;
                    if (intents.Count < 32) intents.Enqueue(p);
                    break;
                default:
                    ProtocolFailure(sender);
                    break;
            }
        }

        void ProcessClient(RoomPacket p)
        {
            switch (p.Type)
            {
                case RoomMessage.Lobby:
                    if (InMatch) return;
                    seats.Clear();
                    seats.AddRange(p.Seats);
                    LocalSeat = p.Seat;
                    receivedRevision = revision = p.Revision;
                    RoomCode = p.Text;
                    Changed?.Invoke();
                    break;
                case RoomMessage.Start:
                    if (!IsConnected || InMatch || p.Revision != revision) return;
                    BeginMatch();
                    break;
                case RoomMessage.Bootstrap:
                case RoomMessage.Batch:
                    if (!InMatch || p.Snapshot.ViewerId != LocalSeat || p.Revision != receivedRevision + 1)
                    { Abort("Urutan data room tidak sesuai. Pertandingan dihentikan."); return; }
                    if ((p.Type == RoomMessage.Bootstrap) != !bootstrapped || (!bootstrapped && (p.Snapshot.Phase != GamePhase.NotStarted || p.Events.Count != 0)))
                    { Abort("Data awal room tidak valid."); return; }
                    foreach (var e in p.Events)
                    {
                        if (e.Sequence != lastEvent + 1) { Abort("Urutan event room tidak sesuai."); return; }
                        lastEvent = e.Sequence;
                    }
                    bootstrapped = true;
                    receivedRevision = revision = p.Revision;
                    inputOpen = false;
                    presentation.Enqueue(p);
                    break;
                case RoomMessage.Open:
                    if (InMatch && p.Revision == revision)
                    {
                        inputOpen = p.Flag;
                        presentation.Enqueue(p);
                    }
                    break;
                case RoomMessage.Rejected:
                    if (InMatch) presentation.Enqueue(p);
                    break;
                case RoomMessage.ReturnRoom:
                    if (InMatch) ResetToRoom();
                    break;
                case RoomMessage.End:
                    Abort(string.IsNullOrEmpty(p.Text) ? "Host menutup room." : p.Text);
                    break;
                default:
                    Abort("Pesan host tidak valid.");
                    break;
            }
        }

        public void SetReady(bool ready)
        {
            if (!IsConnected || InMatch || IsBusy) return;
            SendHost(new RoomPacket { Type = RoomMessage.Ready, Flag = ready });
        }

        public void StartMatch()
        {
            if (!IsHost || !IsConnected || IsBusy || InMatch) return;
            foreach (var seat in seats)
                if (!seat.IsBot && (!seat.Connected || !seat.Ready))
                { SetError("Semua pemain harus siap sebelum host memulai."); return; }
            Error = "";
            Broadcast(new RoomPacket { Type = RoomMessage.Start, Revision = revision });
            BeginMatch();
            match = new InternetMatch(this);
            StartCoroutine(match.Run());
        }

        void BeginMatch()
        {
            InMatch = true;
            sceneAttached = false;
            inputOpen = false;
            bootstrapped = false;
            returnRequested = false;
            lastEvent = 0;
            receivedRevision = revision;
            presentation.Clear();
            intents.Clear();
            foreach (var peer in peers.Values) { peer.SceneReady = false; peer.Presented = 0; }
            sceneDeadline = Time.realtimeSinceStartupAsDouble + ReadyTimeout;
            Status = "Memuat meja…";
            Changed?.Invoke();
            SceneFlow.GoToGame();
        }

        internal void ReturnToRoom()
        {
            if (InMatch) SendHost(new RoomPacket { Type = RoomMessage.ReturnRoom, Revision = revision });
        }

        void ResetToRoom()
        {
            InMatch = false;
            inputOpen = false;
            sceneAttached = false;
            barrierDeadline = 0;
            StopAllCoroutines();
            match = null;
            presentation.Clear();
            intents.Clear();
            foreach (var seat in seats) seat.Ready = seat.SeatId == 0 || seat.IsBot;
            foreach (var peer in peers.Values)
            {
                peer.SceneReady = false;
                peer.Presented = 0;
            }
            Status = "Siapkan pemain untuk pertandingan berikutnya.";
            Changed?.Invoke();
            SceneFlow.GoToMenu();
        }

        internal void AttachScene()
        {
            if (!InMatch || sceneAttached) return;
            sceneAttached = true;
            SendHost(new RoomPacket { Type = RoomMessage.SceneReady, Revision = revision });
        }

        internal void DetachScene()
        {
            if (InMatch && !stopping && !quitting)
                Abort("Meja ditutup. Pertandingan dihentikan untuk semua pemain.");
            sceneAttached = false;
        }

        internal bool TakePresentation(out RoomPacket packet)
        {
            packet = presentation.Count > 0 ? presentation.Dequeue() : null;
            return packet != null;
        }

        internal void Presented(int batch) => SendHost(new RoomPacket { Type = RoomMessage.Presented, Revision = batch });

        internal void Submit(RoomPacket packet)
        {
            if (!InMatch || !inputOpen) return;
            packet.Type = RoomMessage.Intent;
            packet.Revision = revision;
            packet.Sequence = ++localSequence;
            SendHost(packet);
        }

        void SendHost(RoomPacket packet)
        {
            if (IsHost)
                incoming.Enqueue(new Inbound { Sender = NetworkManager.ServerClientId, Packet = packet });
            else if (network != null && network.IsConnectedClient)
                Send(NetworkManager.ServerClientId, packet);
        }

        internal bool AllSceneReady
        {
            get { foreach (var peer in peers.Values) if (!peer.SceneReady) return false; return peers.Count > 0; }
        }

        internal bool AllPresented
        {
            get { foreach (var peer in peers.Values) if (peer.Presented != revision) return false; return peers.Count > 0; }
        }

        internal IEnumerator Publish(GameEngine engine, IEnumerable<GameEvent> events, bool bootstrap = false)
        {
            inputOpen = false;
            intents.Clear();
            revision++;
            barrierDeadline = Time.realtimeSinceStartupAsDouble + ReadyTimeout;
            foreach (var pair in peers)
            {
                var packet = RoomProtocol.Batch(engine, events, pair.Value.Seat, revision);
                if (bootstrap) packet.Type = RoomMessage.Bootstrap;
                Deliver(pair.Key, packet);
            }
            while (InMatch && !AllPresented) yield return null;
            barrierDeadline = 0;
        }

        internal void Open(float seconds = 0)
        {
            if (!InMatch) return;
            inputOpen = true;
            var p = new RoomPacket { Type = RoomMessage.Open, Revision = revision, Seconds = seconds, Flag = true };
            foreach (var pair in peers) Deliver(pair.Key, p);
            Status = "Pertandingan berlangsung.";
        }

        internal bool TakeIntent(out RoomPacket packet)
        {
            while (intents.Count > 0)
            {
                packet = intents.Dequeue();
                if (inputOpen && packet.Revision == revision) return true;
            }
            packet = null;
            return false;
        }

        internal void Reject(int seat, string reason)
        {
            foreach (var pair in peers)
                if (pair.Value.Seat == seat)
                    Deliver(pair.Key, new RoomPacket { Type = RoomMessage.Rejected, Revision = revision, Text = reason });
        }

        void Deliver(ulong id, RoomPacket p)
        {
            if (id == NetworkManager.ServerClientId)
                presentation.Enqueue(p);
            else
                Send(id, p);
        }

        void BroadcastLobby()
        {
            if (!IsHost || InMatch) return;
            foreach (var pair in peers)
            {
                if (pair.Key == NetworkManager.ServerClientId || !seats[pair.Value.Seat].Connected) continue;
                Send(pair.Key, new RoomPacket { Type = RoomMessage.Lobby, Revision = revision, Seat = pair.Value.Seat, Text = RoomCode, Seats = seats.ToArray() });
            }
            Changed?.Invoke();
        }

        void Broadcast(RoomPacket packet)
        {
            foreach (var pair in peers)
                if (pair.Key != NetworkManager.ServerClientId) Send(pair.Key, packet);
        }

        void Send(ulong id, RoomPacket p)
        {
            if (network == null || !network.IsListening) return;
            byte[] bytes = RoomProtocol.Encode(p);
            using (var writer = new FastBufferWriter(bytes.Length, Allocator.Temp))
            {
                writer.WriteBytesSafe(bytes);
                network.CustomMessagingManager.SendNamedMessage(Channel, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        internal void Abort(string reason)
        {
            if (stopping || quitting) return;
            Error = reason;
            Status = reason;
            _ = StopAsync(true);
        }

        public Task LeaveAsync() => StopAsync(false);

        async Task StopAsync(bool failed)
        {
            if (stopping) return;
            stopping = true;
            bool wasMatch = InMatch;
            cancellation?.Cancel();
            inputOpen = false;
            if (IsHost)
                Broadcast(new RoomPacket { Type = RoomMessage.End, Text = failed ? Error : "Host meninggalkan room. Pertandingan dihentikan." });
            InMatch = false;
            StopAllCoroutines();
            await Task.Yield();
            if (connectionTask != null && !connectionTask.IsCompleted) await connectionTask;
            CleanupNetwork();
            await ReleaseAuthentication();
            await Task.Yield();
            if (!failed) { Error = ""; Status = ""; }
            stopping = false;
            Changed?.Invoke();
            if (wasMatch && !quitting)
            {
                SceneFlow.GoToMenu();
                if (failed) StartCoroutine(ShowFailure(Error));
            }
        }

        IEnumerator ShowFailure(string reason)
        {
            for (int i = 0; i < 120 && !InMatch; i++)
            {
                yield return null;
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                var canvas = menu != null ? menu.GetComponentInChildren<Canvas>() : null;
                if (canvas == null) continue;
                var modal = UIKit.CreateModal(canvas.transform, "Room terputus", new Vector2(900f, 430f), () => { });
                UIKit.CreateScrollText(modal.Content, reason, 26);
                yield break;
            }
        }

        void CleanupNetwork()
        {
            if (network != null)
            {
                network.ConnectionApprovalCallback -= Approve;
                network.OnClientConnectedCallback -= Connected;
                network.OnClientDisconnectCallback -= Disconnected;
                network.OnTransportFailure -= TransportFailed;
                network.CustomMessagingManager?.UnregisterNamedMessageHandler(Channel);
                network.Shutdown();
                Destroy(network.gameObject);
            }
            network = null;
            transport = null;
            match = null;
            revision = receivedRevision = localSequence = lastEvent = 0;
            barrierDeadline = 0;
            bootstrapped = false;
            LocalSeat = -1;
            RoomCode = "";
            InMatch = false;
            sceneAttached = false;
            peers.Clear();
            seats.Clear();
            incoming.Clear();
            presentation.Clear();
            intents.Clear();
        }

        Task ReleaseAuthentication()
        {
            if (ownsAuthentication && (serviceTask == null || serviceTask.IsCompleted) && UnityServices.State == ServicesInitializationState.Initialized)
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut();
                ownsAuthentication = false;
            }
            return Task.CompletedTask;
        }

        void SetError(string value)
        {
            Error = Status = value;
            Changed?.Invoke();
        }

        static bool ValidCode(string code)
        {
            if (code.Length < 4 || code.Length > 16) return false;
            foreach (char c in code) if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }

        void OnApplicationQuit()
        {
            quitting = true;
            cancellation?.Cancel();
        }

        void OnDestroy()
        {
            if (Current != this) return;
            quitting = true;
            cancellation?.Cancel();
            cancellation?.Dispose();
            CleanupNetwork();
            _ = ReleaseAuthentication();
            Current = null;
        }
    }
}
