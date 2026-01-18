# Unity Transport for Netick

A Netick integration for Unity Transport and Unity Relay, featuring multi-protocol support and cross-platform connectivity. This package enables players from WebGL, mobile, and native platforms to connect to the same server instance using UDP, WebSocket, or Unity's Relay service.

## Features

- **Multiple Protocol Support**: UDP, WebSocket (TCP), Relay UDP, and Relay WebSocket
- **Cross-Platform Connectivity**: Simultaneous protocol hosting for WebGL and native clients
- **Unity Relay Integration**: Built-in support for Unity's Relay service for NAT traversal
- **Secure Connections**: Optional TLS (WebSocket) and DTLS (UDP) encryption

## Installation

This package requires Netick 2 and can be installed via Unity Package Manager using Git URLs.

**Dependencies:**
- **[Netick for Unity](https://github.com/NetickNetworking/NetickForUnity)**

### Installing from Git

All dependencies can be installed via Unity Package Manager using Git URLs. For detailed instructions on installing packages from Git repositories, see the [Unity documentation on installing from a Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

**Quick Installation Steps:**

1. Open Unity Package Manager (Window > Package Manager)
2. Click the **+** button in the top-left corner
3. Select **Add package from git URL...**
4. Add the following packages in order:
   - First, add Netick: `https://github.com/NetickNetworking/NetickForUnity`
   - Then, add this package: `https://github.com/karrarrahim/UnityTransport-Netick.git`
5. Click **Add** for each package

### Creating a Transport Provider

After installation, create a transport provider instance:

1. In Unity, right-click in the Project window
2. Navigate to **Create > Netick > Transport > UnityTransportProvider**
3. Name the asset (e.g., "UnityTransportProvider")
4. Configure transport settings in the Inspector

## Protocol Configuration

### Supported Protocols

The transport supports four distinct protocol types:

- **UDP**: Standard User Datagram Protocol for low-latency connections
- **WebSocket (TCP)**: TCP-based protocol for WebGL compatibility
- **Relay UDP**: Unity Relay service using UDP transport
- **Relay WebSocket**: Unity Relay service using WebSocket transport

### Multi-Protocol Hosting

The transport can simultaneously host multiple protocols on a single server instance, enabling cross-platform connectivity between WebGL clients and native applications.

**Port Allocation Logic:**

When multiple protocols are enabled, ports are automatically allocated sequentially:

```
firstProtocol = 7777
secondProtocol = 7777 + 1
thirdProtocol = 7777 + 2
```

**Example Configuration:**
- UDP listens on port 7777
- WebSocket listens on port 7778
- Relay UDP listens on port 7779

This automatic allocation ensures no port conflicts when running multiple protocols simultaneously.

### Secure Connections

Optional encryption can be enabled for enhanced security:

- **WebSocket Secure (TLS)**: Encrypted WebSocket connections
- **Secure UDP (DTLS)**: Encrypted UDP connections using Datagram Transport Layer Security

**Note**: Secure connections require additional configuration and certificate management. Refer to Unity's networking documentation for certificate setup procedures.

## Unity Relay Integration

Unity Relay provides NAT traversal and relay services, enabling players behind restrictive firewalls to connect. The transport includes built-in Relay support through Unity's Multiplayer Services SDK.

### Prerequisites

**Installing Multiplayer Services Package:**

1. In the Unity Editor, open **Window > Package Manager**
2. Select **Unity Registry** from the dropdown
3. Search for `Multiplayer Services` (or `com.unity.services.multiplayer`)
4. Click **Install**

**Note**: The older Relay SDK has been deprecated. Use the Multiplayer Services package instead.

**Scripting Define Symbol:**

After installing the package, add `MULTIPLAYER_SERVICES_SDK_INSTALLED` to your project's scripting define symbols:

1. Navigate to **Edit > Project Settings > Player**
2. Expand **Other Settings**
3. Add `MULTIPLAYER_SERVICES_SDK_INSTALLED` to **Scripting Define Symbols**

### Relay Configuration

Configure the transport to use Relay services:

1. Select your UnityTransportProvider asset
2. In the Inspector, set **Server Protocol** to either:
   - **Relay UDP**: For Relay-based UDP connections
   - **Relay WebSocket**: For Relay-based WebSocket connections

### Starting as Host

Before launching Netick as a host, initialize the Relay allocation:

```csharp
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public async void StartHost()
{
    int maxPlayers = 10;
    
    // Create relay allocation
    Allocation hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
    NetickUnityTransport.SetAllocation(hostAllocation);
    
    // Generate join code for clients
    string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);
    
    // Display or share the join code with clients
    Debug.Log($"Join Code: {joinCode}");
    
    // Start Netick as host
    Network.StartAsHost(transportRelay, 0, sandboxPrefab);
}
```

**Workflow:**
1. Create a Relay allocation specifying maximum player capacity
2. Provide the allocation to the transport via `SetAllocation()`
3. Generate a join code from the allocation ID
4. Share the join code with clients through your lobby system or UI
5. Initialize Netick as host

### Joining as Client

Clients use the host's join code to establish Relay connections:

```csharp
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public async void JoinHost(string joinCode)
{
    // Join relay allocation using host's code
    JoinAllocation playerAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
    NetickUnityTransport.SetJoinAllocation(playerAllocation);
    
    // Start Netick as client
    NetworkSandbox sandbox = Network.StartAsClient(transportRelay, 0, sandboxPrefab);
    
    // Connect to host (address parameter ignored when using Relay)
    sandbox.Connect(0, "0.0.0.0");
}
```

**Workflow:**
1. Obtain the host's join code (from lobby, UI input, or matchmaking)
2. Call `JoinAllocationAsync()` with the join code
3. Provide the join allocation to the transport via `SetJoinAllocation()`
4. Initialize Netick as client
5. Call `Connect()` (the address parameter is unused with Relay)

**Important**: When using Relay, the address parameter in `Connect()` is ignored. Connection routing is handled automatically through the Relay service using the join allocation.

## Connection Request Data

### Data Size Limitation

The `NetworkConnectionRequest` data buffer (`byte[]`) has a fixed size of 200 bytes to avoid garbage collection at runtime.

**Usage Considerations:**

```csharp
public override void OnConnectRequest(NetworkSandbox sandbox, NetworkConnectionRequest request)
{
    // Use request.DataLength to determine actual data size
    int actualDataLength = request.DataLength;
    
    // Process only the relevant portion of the data
    byte[] relevantData = new byte[actualDataLength];
    Array.Copy(request.Data, relevantData, actualDataLength);
    
    // Or manually resize the array for your use case
    // byte[] customData = new byte[actualDataLength];
    // Buffer.BlockCopy(request.Data, 0, customData, 0, actualDataLength);
}
```

## Protocol Selection Guidelines

### UDP
- **Best for**: Native platforms with low-latency requirements
- **Use cases**: Fast-paced action games, competitive multiplayer
- **Considerations**: May be blocked by restrictive firewalls

### WebSocket
- **Best for**: WebGL builds and browser-based games
- **Use cases**: Cross-platform games requiring browser support
- **Considerations**: Slightly higher latency than UDP due to TCP overhead

### Relay (UDP/WebSocket)
- **Best for**: Games requiring NAT traversal and global connectivity
- **Use cases**: Public matchmaking, mobile games, games with players behind restrictive NATs
- **Considerations**: Introduces additional latency through relay servers; subject to Unity Relay service limits

### Multi-Protocol Configuration
- **Best for**: Games supporting both WebGL and native clients
- **Use cases**: Cross-platform multiplayer games
- **Implementation**: Enable both UDP and WebSocket protocols on the server

## Support Resources

For Netick-related questions: [Netick Discord Server](https://discord.com/invite/uV6bfG66Fx)

For Unity Transport Documentation: [UTP Documentation](https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/)

For Unity Relay documentation: [Unity Relay Services](https://docs.unity.com/relay)