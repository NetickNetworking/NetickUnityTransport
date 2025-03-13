## Installation

### Prerequisites

Unity Editor version 2022 or later.

Install Netick 2 before installing this package.
https://github.com/NetickNetworking/NetickForUnity

### Steps

- Open the Unity Package Manager by navigating to Window > Package Manager along the top bar.
- Click the plus icon.
- Select Add package from git URL
- Enter https://github.com/karrarrahim/UnityTransport-Netick.git
- You can then create an instance by by double clicking in the Assets folder and going
 - Create > Netick > Transport > UnityTransportProvider

## Feature

### Supported Protocol
- UDP
- WebSocket (TCP)
- Relay UDP
- Relay TCP

### Run multiple protocols in a single machine
  - WebSocket & UDP
  - Allowing players from WebGL and Native device to connect to the server.

Multiple protocols listen port logic
```
firstProtocol = 7777;
secondProtocol = 7777 + 1;
thirdProtocol = 7777 + 2;

Example:
- UDP Starts on 7777
- WebSocket Starts on 7778
- RelayUDP Starts on 7779
```

### Secure Connection (Optional)
- Websocket Secure (TLS)
- Secure UDP (DTLS)

## Relay
- To enable Relay for UTP, add `RELAY_SDK_INSTALLED` to scripting define symbols, and ensure Relay SDK has been installed

#### Host Guide
- Before Launching netick, make sure to use `CreateAllocation` and `GetJoinCode` for Host and supply to the `Allocation` variable. 

#### Client Guide
- Client needs to call `JoinAllocationAsync` and supply it to the public variable.

## Weakness

`NetworkConnectionRequest` Data (`byte[]`) is fixed size (of 200).  
This decision is made to avoid GC at runtime. You may need to use `request.DataLength` or resize the Array by your own to use it accurately.

```cs
public override void OnConnectRequest(NetworkSandbox sandbox, NetworkConnectionRequest request)
{
	
}
```