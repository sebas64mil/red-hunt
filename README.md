

# Red Hunt — Estado y Arquitectura del Proyecto (Actualizado 30/03/2026)

## Resumen de cambios recientes (commit lobby)

- **Lobby robusto y seguro:**
  - El host siempre es ID 1 (evita condiciones de carrera).
  - IDs de jugadores reutilizables y control de máximo de jugadores.
  - Flujo de join/leave/kick robusto: broadcast de REMOVE_PLAYER, limpieza local y desconexión ordenada.
  - Mejoras en handshake y transporte cliente-servidor, manejo de errores y desconexión.
  - Soporte para iniciar partida y sincronizar estado del lobby.

- **Principales archivos modificados:**
  - `LobbyNetworkService.cs`: Forzado de ID host, lógica de join/leave, shutdown ordenado, manejo de paquetes y sincronización de estado.
  - `LobbyManager.cs`: Añadir players remotos con ID, bloqueo para operaciones remotas, control de límite y notificaciones.
  - `PlayerRegistry.cs`: IDs reutilizables, métodos para aceptar IDs explícitos y actualizar tipo de jugador.
  - `ClientConnectionManager.cs`: IDs de cliente desde 2, reutilización y limpieza.
  - `ClientPacketHandler.cs`: Manejo de asignación de player, desconexión y limpieza de estado.
  - `ClientState.cs`: Estado de conexión y eventos.
  - `Client.cs`: Handshake robusto, mejor manejo de transporte y desconexión.
  - `Server.cs`: Dispatch de mensajes y limpieza.
  - `BroadcastService.cs`: Broadcast a todos los clientes.
  - `PacketBuilder.cs`: Nuevos builders para todos los paquetes clave.
  - `SpawnManager.cs`: Spawn/remove de players remotos y posiciones.
  - `UI/Admin/*`: Listado de jugadores, botón kick, flujo de kick y limpieza de estado.
  - `Network/Handlers/*`: Manejo centralizado y robusto de paquetes admin/connection.
  - `JoinLobbyCommand.cs` y `LeaveLobbyCommand.cs`: Integración de comandos en el flujo de lobby.
  - **Documentación:** Registro de cambios y explicación de problemas UDP/reordenamiento y soluciones.

## Objetivos cumplidos

- Evitar condiciones de carrera en asignación de IDs (host = ID 1 garantizado).
- Flujo de join/leave/kick robusto y ordenado.
- Reutilización segura de IDs y control del máximo de players.
- Mejoras en handshake, transporte y manejo de errores.
- Sincronización de estado y soporte para iniciar partida.

---

## Resumen Ejecutivo

El proyecto ahora cuenta con una **arquitectura profesional y escalable**:
- Separación clara de capas (Network, Application, Presentation)
- Patrón Installers para inicialización limpia
- Sin God Classes
- Bajo acoplamiento
- Fácil de testear y mantener

---


## Arquitectura actual

- **Separación clara de capas:** Network, Application, Presentation.
- **Patrón Installers:** Inicialización limpia y desacoplada.
- **Sin God Classes:** Cada responsabilidad está centralizada y desacoplada.
- **Fácil de testear y mantener:** Cambios localizados, bajo acoplamiento.

---

---


## Estructura del proyecto


```
Assets/red hunt/Scripts/
├── Network/
│   ├── Transport/ (Server, Client, ClientConnectionManager, ClientState, BroadcastService)
│   ├── Handlers/ (ConnectionHandler, ClientPacketHandler, AdminPacketHandler, etc.)
│   ├── Packets/ (BasePacket, PacketBuilder, etc.)
│   └── ...
├── Application/
│   └── Services/
│       ├── LobbyGame/ (LobbyManager, LobbyNetworkService, JoinLobbyCommand, LeaveLobbyCommand)
│       └── Session/ (PlayerRegistry, PlayerSession)
├── Presentation/
│   ├── Bootstrap/ (GameBootstrap, NetworkInstaller, ApplicationInstaller, PresentationInstaller)
│   └── UI/ (AdminUI, LobbyUI, etc.)
└── ...
```

---



