package import
// server
dotnet add package Grpc.AspNetCore
// client
dotnet add package Grpc.Net.Client
dotnet add package Google.Protobuf
dotnet add package Grpc.Tools
// log
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File

- build docker
```
docker compose build
docker compose up -d
```

==> remove : docker compose down


// release docker
docker tag udpmaster-grpc_websocket_app:latest ninhmphpl/udpmaster-grpc_websocket_app:1.0
docker push ninhmphpl/udpmaster-grpc_websocket_app:1.0