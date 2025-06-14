using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Networking = Unity.Networking;
using Unity.Networking.Transport.TLS;
using Unity.Networking.Transport.Relay;
using Unity.Jobs;
using Unity.Networking.Transport.Error;

#if MULTIPLAYER_SERVICES_SDK_INSTALLED
using Unity.Services.Relay.Models;
#endif

namespace Netick.Transport
{
    public enum ClientNetworkProtocol
    {
        None = 0,
        UDP = 1,
        WebSocket = 2,
        RelayUDP = 3,
        RelayWebSocket = 4,

        /// <summary>
        /// Will determine protocol based on the platform selected
        /// </summary>
        Auto = 5,
    }

    [Flags]
    public enum ServerNetworkProtocol
    {
        None = 0,
        UDP = 1 << 0,
        WebSocket = 1 << 2,
        RelayUDP = 1 << 3,
        RelayWebSocket = 1 << 4,
    }

    [CreateAssetMenu(fileName = "UnityTransportProvider", menuName = "Netick/Transport/UnityTransportProvider", order = 1)]
    public class UnityTransportProvider : Unity.NetworkTransportProvider
    {
        [SerializeField] private ClientNetworkProtocol _clientProtocol;
        [SerializeField] private ServerNetworkProtocol _serverProtocol;
        [SerializeField] private NetworkConfigParameter _parameters;

        [Header("Encryption")]
        [SerializeField][Tooltip("Per default the client/server communication will not be encrypted. Select true to enable DTLS for UDP and TLS for Websocket.")] private bool _useEncryption;

        [Space]
        [SerializeField][TextArea(3, 10)] private string _serverCertificate;
        [SerializeField][TextArea(3, 10)] private string _serverPrivateKey;

        [Space]
        [SerializeField][TextArea(3, 10)] private string _serverCommonName;
        [SerializeField][TextArea(3, 10)] private string _clientCaCertificate;

        public ClientNetworkProtocol ClientProtocol => _clientProtocol;
        public ServerNetworkProtocol ServerProtocol => _serverProtocol;
        public string ServerCommonName => _serverCommonName;
        public ref NetworkConfigParameter Parameters => ref _parameters;

        private void Reset()
        {
            NetworkSettings settings = new NetworkSettings();
            NetworkConfigParameter param = settings.GetNetworkConfigParameters();

            _parameters = param;
        }

        public void SetProtocol(ClientNetworkProtocol clientProtocol, ServerNetworkProtocol serverProtocol)
        {
            _clientProtocol = clientProtocol;
            _serverProtocol = serverProtocol;
        }

        public void SetEncryption(bool useEncryption)
        {
            _useEncryption = useEncryption;
        }

        public void SetServerCertificate(string serverCertificate)
        {
            _serverCertificate = serverCertificate;
        }

        public void SetServerPrivateKey(string serverPrivateKey)
        {
            _serverPrivateKey = serverPrivateKey;
        }

        public void SetServerSecrets(string serverCertificate, string serverPrivateKey)
        {
            _serverCertificate = serverCertificate;
            _serverPrivateKey = serverPrivateKey;
        }

        public void SetClientSecrets(string serverCommonName, string caCertificate = null)
        {
            _serverCommonName = serverCommonName;
            _clientCaCertificate = caCertificate;
        }

        public override NetworkTransport MakeTransportInstance()
        {
            NetickUnityTransport transport = new();
            transport.SetProtocol(GetClientNetworkProtocol(), _serverProtocol);
            transport.SetNetworkConfigParameter(_parameters);
            transport.SetEncryption(_useEncryption);
            transport.SetServerSecrets(_serverCertificate, _serverPrivateKey);
            transport.SetClientSecrets(_serverCommonName, _clientCaCertificate);

            return transport;
        }

        private ClientNetworkProtocol GetClientNetworkProtocol()
        {
            if (_clientProtocol != ClientNetworkProtocol.Auto)
                return _clientProtocol;

#if UNITY_WEBGL && !UNITY_EDITOR
            return ClientNetworkProtocol.WebSocket;
#else
            return ClientNetworkProtocol.UDP;
#endif
        }
    }
    public static class NetickUnityTransportExt { public static NetickUnityTransport.NetickUnityTransportEndPoint ToNetickEndPoint(this NetworkEndpoint networkEndpoint) => new NetickUnityTransport.NetickUnityTransportEndPoint(networkEndpoint); }

    public unsafe class NetickUnityTransport : NetworkTransport
    {
        public ClientNetworkProtocol ClientProtocol;
        public ServerNetworkProtocol ServerProtocol;
        public NetworkConfigParameter Parameters;

        public bool UseEncryption;

        public string ServerCertificate;
        public string ServerPrivateKey;

        public string ServerCommonName;
        public string ClientCaCertificate;


        public void SetNetworkConfigParameter(NetworkConfigParameter parameters)
        {
            Parameters = parameters;
        }

        public void SetEncryption(bool useEncryption)
        {
            UseEncryption = useEncryption;
        }

        public void SetServerSecrets(string serverCertificate, string serverPrivateKey)
        {
            ServerCertificate = serverCertificate;
            ServerPrivateKey = serverPrivateKey;
        }

        public void SetClientSecrets(string serverCommonName, string caCertificate = null)
        {
            ServerCommonName = serverCommonName;
            ClientCaCertificate = caCertificate;
        }

        public void SetProtocol(ClientNetworkProtocol clientProtocol, ServerNetworkProtocol serverProtocol)
        {
            ClientProtocol = clientProtocol;
            ServerProtocol = serverProtocol;
        }

        public struct NetickUnityTransportEndPoint : IEndPoint
        {
            public NetworkEndpoint EndPoint;
            string IEndPoint.IPAddress => EndPoint.Address.ToString();
            int IEndPoint.Port => EndPoint.Port;
            public NetickUnityTransportEndPoint(NetworkEndpoint networkEndpoint)
            {
                EndPoint = networkEndpoint;
            }
            public override string ToString()
            {
                return $"{EndPoint.Address}";
            }
        }



        public unsafe class NetickUnityTransportConnection : TransportConnection
        {

            public NetickUnityTransport Transport;
            public Networking.Transport.NetworkConnection Connection;
            public override IEndPoint EndPoint => Transport._driver.GetRemoteEndpoint(Connection).ToNetickEndPoint();
            public override int Mtu => MaxPayloadSize;

            public int MaxPayloadSize;

            public NetickUnityTransportConnection(NetickUnityTransport transport)
            {
                Transport = transport;
            }

            public unsafe override void Send(IntPtr ptr, int length)
            {
                if (!Connection.IsCreated)
                    return;

                int beginSendResult = Transport._driver.BeginSend(NetworkPipeline.Null, Connection, out var networkWriter);

                if (beginSendResult < 0)
                {
                    Debug.LogError($"[{nameof(UnityTransportProvider)}]: Error begin send: {(StatusCode)beginSendResult}");
                }

                networkWriter.WriteBytesUnsafe((byte*)ptr.ToPointer(), length);
                int endSendResult = Transport._driver.EndSend(networkWriter);

                if (endSendResult < 0)
                {
                    Debug.LogError($"[{nameof(UnityTransportProvider)}]: Error begin send: {(StatusCode)beginSendResult}");
                }
            }
        }

        private MultiNetworkDriver _driver;
        private Dictionary<Networking.Transport.NetworkConnection, NetickUnityTransportConnection> _connectedPeers = new();
        private Queue<NetickUnityTransportConnection> _freeConnections = new();
        private Networking.Transport.NetworkConnection _serverConnection;

        private NativeList<Networking.Transport.NetworkConnection> _connections;

        private BitBuffer _bitBuffer;
        private byte* _bytesBuffer;
        private int _bytesBufferSize = 2048;

        private const int MAX_CONNECTION_REQUEST_SIZE = 200;
        private byte[] _connectionRequestBytes = new byte[MAX_CONNECTION_REQUEST_SIZE];
        private NativeArray<byte> _connectionRequestNative;

        private const string CONNECTION_TYPE_UDP = "udp";
        private const string CONNECTION_TYPE_DTLS = "dtls";

        private const string CONNECTION_TYPE_WS = "ws";
        private const string CONNECTION_TYPE_WSS = "wss";

#if MULTIPLAYER_SERVICES_SDK_INSTALLED
        public static Allocation Allocation => _allocation;
        public static JoinAllocation JoinAllocation => _joinAllocation;

        private static Allocation _allocation;
        private static JoinAllocation _joinAllocation;

        public static void SetAllocation(Allocation allocation)
        {
            _allocation = allocation;
        }

        public static void SetJoinAllocation(JoinAllocation joinAllocation)
        {
            _joinAllocation = joinAllocation;
        }
#endif

        private enum RelaySocket
        {
            UDP,
            WebSocket
        }

        public NetickUnityTransport()
        {
            _bytesBuffer = (byte*)UnsafeUtility.Malloc(_bytesBufferSize, 4, Allocator.Persistent);
        }

        ~NetickUnityTransport()
        {
            UnsafeUtility.Free(_bytesBuffer, Allocator.Persistent);
            _connectionRequestNative.Dispose();
        }

        public override void Init()
        {
            _bitBuffer = new BitBuffer(createChunks: false);
            _connections = new NativeList<Networking.Transport.NetworkConnection>(Engine.IsServer ? Engine.Config.MaxPlayers : 0, Allocator.Persistent);
        }

        private NetworkDriver ConstructDriverRelay<N>(N networkInterface, string connectionType) where N : unmanaged, INetworkInterface
        {
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
            RelayServerData relayData;
            if (Engine.IsServer)
            {
                if (Allocation == null)
                {
                    throw new Exception("Relay Allocation Request is null");
                }

                relayData = Allocation.ToRelayServerData(connectionType);
            }
            else
            {
                if (JoinAllocation == null)
                {
                    throw new Exception("Relay Join Allocation is null");
                }

                relayData = JoinAllocation.ToRelayServerData(connectionType);
            }
            NetworkSettings settings = GetDefaultNetworkSettings();
            settings.WithRelayParameters(ref relayData);

            NetworkDriver driver = NetworkDriver.Create(networkInterface, settings);
            return driver;
#else
            throw new Exception("Unity Relay SDK is missing. If it's already installed, ensure 'MULTIPLAYER_SERVICES_SDK_INSTALLED' is added to the scripting define symbols");
#endif
        }

        private string GetRelayConnectionType(RelaySocket socket, bool isSecure)
        {
            if (socket == RelaySocket.UDP)
            {
                if (!isSecure)
                {
                    return CONNECTION_TYPE_UDP;
                }

                return CONNECTION_TYPE_DTLS;
            }


            if (socket == RelaySocket.WebSocket)
            {
                if (!isSecure)
                {
                    return CONNECTION_TYPE_WS;
                }

                return CONNECTION_TYPE_WSS;
            }

            Debug.LogError("Failed to get relay connection type");
            return string.Empty;
        }

        private NetworkDriver ConstructDriverRelayUDP()
        {
            bool isSecure = UseEncryption;

            string connectionType = GetRelayConnectionType(RelaySocket.UDP, isSecure);
#if UNITY_WEBGL && !UNITY_EDITOR
            return ConstructDriverRelay(new IPCNetworkInterface(), connectionType);
            Debug.LogError($"[{nameof(UnityTransportProvider)}]: Relay UDP Driver is not available in webGL!");
#else
            return ConstructDriverRelay(new UDPNetworkInterface(), connectionType);
#endif
        }

        private NetworkDriver ConstructDriverRelayWS()
        {
            bool isSecure = UseEncryption;

            string connectionType = GetRelayConnectionType(RelaySocket.WebSocket, isSecure);

            return ConstructDriverRelay(new WebSocketNetworkInterface(), connectionType);
        }

        private NetworkSettings GetNetworkSettings(bool isServer)
        {
            if (UseEncryption)
                return GetSecureNetworkSettings(isServer);

            return GetDefaultNetworkSettings();
        }

        private NetworkSettings GetDefaultNetworkSettings()
        {
            NetworkSettings settings = new NetworkSettings();

            settings.AddRawParameterStruct(ref Parameters);

            return settings;
        }

        private NetworkSettings GetSecureNetworkSettings(bool isServer)
        {
            NetworkSettings settings = GetDefaultNetworkSettings();

            if (isServer)
            {
                if (string.IsNullOrEmpty(ServerCertificate) || string.IsNullOrEmpty(ServerPrivateKey))
                {
                    throw new Exception("In order to use encrypted communications, when hosting, you must set the server certificate and key.");
                }

                settings.WithSecureServerParameters(ServerCertificate, ServerPrivateKey);
            }
            else
            {
                if (string.IsNullOrEmpty(ServerCommonName))
                {
                    throw new Exception("In order to use encrypted communications, clients must set the server common name.");
                }
                else if (string.IsNullOrEmpty(ClientCaCertificate))
                {
                    settings.WithSecureClientParameters(ServerCommonName);
                }
                else
                {
                    settings.WithSecureClientParameters(ClientCaCertificate, ServerCommonName);
                }
            }

            return settings;
        }

        private NetworkDriver ConstructDriverUDP(bool isServer)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return NetworkDriver.Create(new IPCNetworkInterface());
#else
            NetworkSettings networkSettings = GetNetworkSettings(isServer);

            return NetworkDriver.Create(new UDPNetworkInterface(), networkSettings);
#endif
        }

        private NetworkDriver ConstructDriverWS(bool isServer)
        {
            NetworkSettings networkSettings = GetNetworkSettings(isServer);

            return NetworkDriver.Create(new WebSocketNetworkInterface(), networkSettings);
        }

        private struct NetworkDriverCollections
        {
            public NetworkDriver DriverUdp;
            public NetworkDriver DriverWs;
            public NetworkDriver DriverRelayUdp;
            public NetworkDriver DriverRelayWs;

            public static NetworkDriverCollections ConstructDummy()
            {
                NetworkDriverCollections collections = new NetworkDriverCollections();
                collections.DriverUdp = NetworkDriver.Create(new DummyNetworkInterface());
                collections.DriverWs = NetworkDriver.Create(new DummyNetworkInterface());
                collections.DriverRelayUdp = NetworkDriver.Create(new DummyNetworkInterface());
                collections.DriverRelayWs = NetworkDriver.Create(new DummyNetworkInterface());

                return collections;
            }
        }

        private NetworkDriverCollections ConstructClientNetworkDriverCollection(ClientNetworkProtocol clientNetworkProtocol)
        {
            if (clientNetworkProtocol == ClientNetworkProtocol.None)
            {
                throw new Exception("No Client Network Protocol Chosen");
            }

            NetworkDriverCollections drivers = default;

            if (clientNetworkProtocol == ClientNetworkProtocol.UDP)
                drivers.DriverUdp = ConstructDriverUDP(false);

            if (clientNetworkProtocol == ClientNetworkProtocol.WebSocket)
                drivers.DriverWs = ConstructDriverWS(false);

            if (clientNetworkProtocol == ClientNetworkProtocol.RelayUDP)
                drivers.DriverRelayUdp = ConstructDriverRelayUDP();

            if (clientNetworkProtocol == ClientNetworkProtocol.RelayWebSocket)
                drivers.DriverRelayWs = ConstructDriverRelayWS();

            return drivers;
        }

        private NetworkDriverCollections ConstructServerNetworkDriverCollection(ServerNetworkProtocol serverNetworkProtocol)
        {
            if (serverNetworkProtocol == ServerNetworkProtocol.None)
            {
                throw new Exception("No Server Network Protocol Chosen");
            }

            NetworkDriverCollections drivers = default;

            if (serverNetworkProtocol.HasFlag(ServerNetworkProtocol.UDP))
                drivers.DriverUdp = ConstructDriverUDP(true);

            if (serverNetworkProtocol.HasFlag(ServerNetworkProtocol.WebSocket))
                drivers.DriverWs = ConstructDriverWS(true);

            if (serverNetworkProtocol.HasFlag(ServerNetworkProtocol.RelayUDP))
                drivers.DriverRelayUdp = ConstructDriverRelayUDP();

            if (serverNetworkProtocol.HasFlag(ServerNetworkProtocol.RelayWebSocket))
                drivers.DriverRelayWs = ConstructDriverRelayWS();

            return drivers;
        }

        public override void Run(RunMode mode, int port)
        {
            bool isServer = mode == RunMode.Server;

            if (isServer)
            {
                RunServerMode(port);
                return;
            }

            RunClientMode();
        }

        private void RunServerMode(int port)
        {
            NetworkDriverCollections drivers = ConstructServerNetworkDriverCollection(ServerProtocol);

            NetworkDriver driverUdp = drivers.DriverUdp;
            NetworkDriver driverWs = drivers.DriverWs;
            NetworkDriver driverRelayUdp = drivers.DriverRelayUdp;
            NetworkDriver driverRelayWs = drivers.DriverRelayWs;

            MultiNetworkDriver multiDriver = MultiNetworkDriver.Create();

            int listenPort = port;

            if (ServerProtocol == ServerNetworkProtocol.None)
            {
                throw new Exception($"No Server Protocol was chosen");
            }

            if (ServerProtocol.HasFlag(ServerNetworkProtocol.UDP))
            {
                BindAndListenDriverTo(in driverUdp, listenPort);
                multiDriver.AddDriver(driverUdp);
                listenPort++;
            }

            if (ServerProtocol.HasFlag(ServerNetworkProtocol.WebSocket))
            {
                BindAndListenDriverTo(in driverWs, listenPort);
                multiDriver.AddDriver(driverWs);
                listenPort++;
            }

            if (ServerProtocol.HasFlag(ServerNetworkProtocol.RelayUDP))
            {
                BindAndListenDriverTo(in driverRelayUdp, listenPort);
                multiDriver.AddDriver(driverRelayUdp);
                listenPort++;
            }

            if (ServerProtocol.HasFlag(ServerNetworkProtocol.RelayWebSocket))
            {
                BindAndListenDriverTo(in driverRelayWs, listenPort);
                multiDriver.AddDriver(driverRelayWs);
                listenPort++;
            }

            _driver = multiDriver;

            for (int i = 0; i < Engine.Config.MaxPlayers; i++)
                _freeConnections.Enqueue(new NetickUnityTransportConnection(this));
        }

        private void RunClientMode()
        {
            NetworkDriverCollections drivers = ConstructClientNetworkDriverCollection(ClientProtocol);

            NetworkDriver driverUdp = drivers.DriverUdp;
            NetworkDriver driverWs = drivers.DriverWs;
            NetworkDriver driverRelayUdp = drivers.DriverRelayUdp;
            NetworkDriver driverRelayWs = drivers.DriverRelayWs;

            MultiNetworkDriver multiDriver = MultiNetworkDriver.Create();

            NetworkDriver.Create();

            if (ClientProtocol == ClientNetworkProtocol.UDP)
                multiDriver.AddDriver(driverUdp);
            if (ClientProtocol == ClientNetworkProtocol.WebSocket)
                multiDriver.AddDriver(driverWs);
            if (ClientProtocol == ClientNetworkProtocol.RelayUDP)
                multiDriver.AddDriver(driverRelayUdp);
            if (ClientProtocol == ClientNetworkProtocol.RelayWebSocket)
                multiDriver.AddDriver(driverRelayWs);

            _driver = multiDriver;

            for (int i = 0; i < Engine.Config.MaxPlayers; i++)
                _freeConnections.Enqueue(new NetickUnityTransportConnection(this));
        }

        private void BindAndListenDriverTo(in NetworkDriver driver, int port)
        {
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort)port);

            if (driver.Bind(endpoint) != 0)
            {
                Debug.LogError($"Failed to bind to port {port}");
                return;
            }

            driver.Listen();
        }
        public override void Shutdown()
        {
            if (_driver.IsCreated)
                _driver.Dispose();
            _connections.Dispose();
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLength)
        {
            int clientDriverId = 1;
            var endpoint = NetworkEndpoint.Parse(address, (ushort)port);
            if (connectionData != null)
            {
                if (_connectionRequestNative.IsCreated)
                    _connectionRequestNative.Dispose();

                _connectionRequestNative = new NativeArray<byte>(connectionData, Allocator.Persistent);
                _serverConnection = _driver.Connect(clientDriverId, endpoint, _connectionRequestNative);
            }
            else
                _serverConnection = _driver.Connect(clientDriverId, endpoint);
        }

        public override void Disconnect(TransportConnection connection)
        {
            var conn = (NetickUnityTransport.NetickUnityTransportConnection)connection;
            if (conn.Connection.IsCreated)
            {
                _driver.Disconnect(conn.Connection);

                TransportDisconnectReason reason = TransportDisconnectReason.Shutdown;

                NetworkPeer.OnDisconnected(conn, reason);
                _freeConnections.Enqueue(conn);
                _connectedPeers.Remove(conn.Connection);

                // clean up connections.
                for (int i = 0; i < _connections.Length; i++)
                {
                    if (_connections[i] == conn.Connection)
                    {
                        _connections.RemoveAtSwapBack(i);
                        i--;
                    }
                }
            }
        }

        public override void PollEvents()
        {
            _driver.ScheduleUpdate().Complete();

            if (Engine.IsClient && !_serverConnection.IsCreated)
                return;

            // reading events
            if (Engine.IsServer)
            {
                // clean up connections.
                for (int i = 0; i < _connections.Length; i++)
                {
                    if (!_connections[i].IsCreated)
                    {
                        _connections.RemoveAtSwapBack(i);
                        i--;
                    }
                }

                // accept new connections in the server.
                Networking.Transport.NetworkConnection c;
                while ((c = _driver.Accept(out var payload)) != default)
                {
                    if (_connectedPeers.Count >= Engine.Config.MaxPlayers)
                    {
                        _driver.Disconnect(c);
                        continue;
                    }

                    if (payload.IsCreated)
                    {
                        CopyTo(payload, _connectionRequestBytes);
                    }

                    bool accepted = NetworkPeer.OnConnectRequest(_connectionRequestBytes, payload.Length, _driver.GetRemoteEndpoint(c).ToNetickEndPoint());
                    if (!accepted)
                    {
                        _driver.Disconnect(c);
                        continue;
                    }

                    var connection = _freeConnections.Dequeue();
                    connection.Connection = c;
                    _connectedPeers.Add(c, connection);
                    _connections.Add(c);

                    connection.MaxPayloadSize = NetworkParameterConstants.MTU - _driver.GetDriverForConnection(connection.Connection).MaxHeaderSize(NetworkPipeline.Null);
                    NetworkPeer.OnConnected(connection);
                }

                for (int i = 0; i < _connections.Length; i++)
                    HandleConnectionEvents(_connections[i], i);
            }
            else
                HandleConnectionEvents(_serverConnection, 0);
        }

        private void CopyTo(in NativeArray<byte> reference, byte[] target)
        {
            Array.Clear(target, 0, target.Length);

            for (int i = 0; i < reference.Length; i++)
            {
                target[i] = reference[i];
            }
        }


        private void HandleConnectionEvents(Networking.Transport.NetworkConnection conn, int index)
        {
            NetworkEvent.Type cmd;

            while ((cmd = _driver.PopEventForConnection(conn, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
            {
                // game data
                if (cmd == NetworkEvent.Type.Data)
                {
                    if (_connectedPeers.TryGetValue(conn, out var netickConn))
                    {
                        stream.ReadBytesUnsafe(_bytesBuffer, stream.Length);
                        _bitBuffer.SetFrom(_bytesBuffer, stream.Length, _bytesBufferSize);
                        NetworkPeer.Receive(netickConn, _bitBuffer);
                    }
                }

                // connected to server
                if (cmd == NetworkEvent.Type.Connect && Engine.IsClient)
                {
                    var connection = _freeConnections.Dequeue();
                    connection.Connection = conn;

                    _connectedPeers.Add(conn, connection);
                    _connections.Add(conn);

                    connection.MaxPayloadSize = NetworkParameterConstants.MTU - _driver.GetDriverForConnection(connection.Connection).MaxHeaderSize(NetworkPipeline.Null);
                    NetworkPeer.OnConnected(connection);
                }

                // disconnect
                if (cmd == NetworkEvent.Type.Disconnect)
                {
                    if (_connectedPeers.TryGetValue(conn, out var netickConn))
                    {
                        TransportDisconnectReason reason = TransportDisconnectReason.Shutdown;

                        NetworkPeer.OnDisconnected(netickConn, reason);
                        _freeConnections.Enqueue(netickConn);
                        _connectedPeers.Remove(conn);
                    }
                    else
                    {
                        if (Engine.IsClient)
                            NetworkPeer.OnConnectFailed(ConnectionFailedReason.Refused);
                    }

                    if (Engine.IsClient)
                        _serverConnection = default;
                    if (Engine.IsServer)
                        _connections[index] = default;
                }
            }
        }

        private struct DummyNetworkInterface : INetworkInterface
        {
            public NetworkEndpoint LocalEndpoint { get => default; }

            public int Initialize(ref NetworkSettings settings, ref int packetPadding) => 0;
            public void Dispose() { }

            public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep) => dep;
            public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep) => dep;

            public int Bind(NetworkEndpoint endpoint) => 0;
            public int Listen() => 0;
        }
    }
}