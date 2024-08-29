## Installation

### Prerequisites

Unity Editor version 2021 or later.

Install Netick 2 before installing this package.
https://github.com/NetickNetworking/NetickForUnity

### Steps

- Open the Unity Package Manager by navigating to Window > Package Manager along the top bar.
- Click the plus icon.
- Select Add package from git URL
- Enter https://github.com/karrarrahim/UnityTransport-Netick.git
- You can then create an instance by by double clicking in the Assets folder and going to Create->Netick->Transport->UnityTransportProvider

### Supported Protocol
- WebSocket (TCP)
- UDP

## Feature
- Run multiple protocols in a single machine
  - WebSocket & UDP
  - Allowing players from WebGL and Native device to connect to the server.

If there were multiple protocols running, the selected port would be:
```
startPort = 7777;
protocolPort = 7777 + n;

E.g

UDP Starts on 7777
WS Starts on 7778

```
