using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// How a room's transport reaches the other players. Shipped builds use <see cref="RelayEndpoint"/>;
    /// development tooling may swap in another endpoint through <see cref="InternetRoom.Endpoint"/>.
    /// </summary>
    internal interface IRoomEndpoint
    {
        /// <summary>True when Unity Services (project link + Authentication) must be ready before connecting.</summary>
        bool UsesServices { get; }

        bool ValidCode(string code);

        /// <summary>Prepares the transport for hosting and returns the code other players join with.</summary>
        Task<string> HostAsync(UnityTransport transport, int maxClients, CancellationToken token);

        Task JoinAsync(UnityTransport transport, string code, CancellationToken token);
    }

    internal sealed class RelayEndpoint : IRoomEndpoint
    {
        public bool UsesServices => true;

        static string Connection => Application.platform == RuntimePlatform.WebGLPlayer ? "wss" : "dtls";

        public bool ValidCode(string code)
        {
            if (code.Length < 4 || code.Length > 16) return false;
            foreach (char c in code) if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }

        public async Task<string> HostAsync(UnityTransport transport, int maxClients, CancellationToken token)
        {
            transport.UseWebSockets = Connection == "wss";
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxClients);
            token.ThrowIfCancellationRequested();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, Connection));
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            token.ThrowIfCancellationRequested();
            return code;
        }

        public async Task JoinAsync(UnityTransport transport, string code, CancellationToken token)
        {
            transport.UseWebSockets = Connection == "wss";
            var allocation = await RelayService.Instance.JoinAllocationAsync(code);
            token.ThrowIfCancellationRequested();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, Connection));
        }
    }
}
