
# Red Hunt - Estado y Arquitectura del Proyecto

## Resumen Ejecutivo

El proyecto ahora cuenta con una **arquitectura profesional y escalable**:
- Separación clara de capas (Network, Application, Presentation)
- Patrón Installers para inicialización limpia
- Sin God Classes
- Bajo acoplamiento
- Fácil de testear y mantener

---

## Estado Inicial vs Estado Final

### Antes
```
PlayerBootstrap (God Class)
  Inicializaba Network, Application, Presentation
  Conectaba eventos
  500+ líneas de lógica mezclada
```
**Problemas:**
- Imposible de testear
- Un cambio pequeño rompía todo
- Código duplicado
- Difícil de mantener
- No escalable

### Ahora
```
GameBootstrap (Orquestador limpio)
  Ejecuta NetworkInstaller (80 líneas)
  Ejecuta ApplicationInstaller (20 líneas)
  Ejecuta PresentationInstaller (40 líneas)
  Conecta eventos (50 líneas)
  Total: ~60 líneas en GameBootstrap

+ NetworkServices (contiene Network)
+ ApplicationServices (contiene App)
+ PresentationServices (contiene UI)
```
**Beneficios:**
- Fácil de testear (cada Installer independiente)
- Un cambio afecta solo su Installer
- Código centralizado por responsabilidad
- Fácil de mantener
- Escalable (agregar servicio = agregar 5 líneas)

---

## Estructura Actual del Proyecto

```text
Assets/red hunt/Scripts/
├── Network/
│   ├── Transport/
│   │   ├── Server.cs
│   │   ├── Client.cs
│   │   ├── ClientConnectionManager.cs
│   │   ├── ClientState.cs
│   │   └── BroadcastService.cs
│   ├── Handlers/
│   │   ├── ConnectionHandler.cs
│   │   └── ClientPacketHandler.cs
│   ├── Dispatching/
│   │   └── PacketDispatcher.cs
│   ├── Packets/
│   │   ├── BasePacket.cs
│   │   ├── AssignPlayerPacket.cs
│   │   ├── PlayerReadyPacket.cs
│   │   └── PacketBuilder.cs
│   ├── Serialization/
│   │   └── JsonSerializer.cs
│   └── Interfaces/
│       ├── IServer.cs
│       ├── IClient.cs
│       └── ISerializer.cs
│
├── Application/
│   └── Services/
│       ├── LobbyGame/
│       │   ├── ILobbyCommand.cs
│       │   ├── LobbyManager.cs
│       │   ├── JoinLobbyCommand.cs
│       │   └── LeaveLobbyCommand.cs
│       └── Session/
│           ├── PlayerRegistry.cs
│           └── PlayerSession.cs
│
├── Presentation/
│   └── Bootstrap/
│       ├── GameBootstrap.cs (Orquestador)
│       └── installers/
│           ├── NetworkInstaller.cs
│           ├── ApplicationInstaller.cs
│           └── PresentationInstaller.cs
│   └── UI/
```

---

## Archivos Clave Creados/Modificados

1. GameBootstrap.cs - Orquestador limpio
2. NetworkInstaller.cs - Inicialización de red completa
3. ApplicationInstaller.cs - Inicialización de app
4. PresentationInstaller.cs - Inicialización de UI
5. ARCHITECTURE_OVERVIEW.md - Documentación de arquitectura

- `Transport/`: capa base de envio/recepcion (socket, relay, etc.).
- `Serialization/`: serializacion y deserializacion de mensajes.
- `Packets/`: definicion de paquetes/eventos de red.
- `Handlers/`: manejo de paquetes recibidos.
- `Dispatching/`: ruteo de eventos/mensajes a handlers.
- `Lobby/`: flujo de sala, conexion de jugadores y estado previo a partida.
- `Interfaces/`: contratos para desacoplar la red del resto del sistema.

Regla: no debe contener logica de UI; solo comunicacion y adaptacion de datos.

### 4) Presentation (Capa visual/audio)
Responsable de mostrar el estado del juego al jugador.

- `UI/`: pantallas, HUD, menus y feedback visual de interfaz.
- `Animation/`: control de animaciones.
- `VFX/`: efectos visuales.
- `Sounds/`: efectos de audio y musica.

Regla: consume estado/casos de uso; no debe contener reglas de negocio complejas.

## Flujo recomendado entre capas

1. `Presentation` captura input/acciones del jugador.
2. `Application` ejecuta casos de uso.
3. `Application` consulta/actualiza `Domains`.
4. Si hay multiplayer, `Application` interactua con `Network` por interfaces.
5. `Presentation` refresca la vista con el nuevo estado.

## Estado actual del repositorio

La estructura de carpetas de `Scripts` ya tiene implementaciones activas.

Scripts detectados en la revision:

- `Application/Services/LobbyController.cs`
- `Network/Bootstrap/PlayerBootstrap.cs`
- `Network/Dispatching/PacketDispatcher.cs`
- `Network/Handlers/PlayerHandler.cs`
- `Network/Interfaces/IGameConnection.cs`
- `Network/Interfaces/IClient.cs`
- `Network/Interfaces/ISerializer.cs`
- `Network/Interfaces/IServer.cs`
- `Network/Packets/BasePacket.cs`
- `Network/Packets/PacketBuilder.cs`
- `Network/Packets/PlayerPacket.cs`
- `Network/Serialization/JsonSerializer.cs`
- `Network/Transport/Client/Client.cs`
- `Network/Transport/Server/Server.cs`
- `Presentation/UI/Lobby/LobbyUI.cs`

## Actualizacion del proyecto 2

En esta etapa ya se encuentra montada una base funcional para multiplayer y flujo de lobby:

- Contratos de red (`IGameConnection`, `IClient`, `IServer`, `ISerializer`).
- Transporte UDP cliente/servidor con recepcion asincrona.
- Serializacion JSON de paquetes.
- Sistema de paquetes (`BasePacket`, `PlayerPacket`, `PacketBuilder`).
- Dispatching por tipo de paquete y handler dedicado de jugador.
- Bootstrap de dependencias para inicializar red + dispatcher + handler.
- Servicio de aplicacion para crear/unirse a lobby.
- UI de lobby conectada al flujo de seleccion de rol.

Esto confirma que el proyecto ya paso de una fase solo estructural a una fase de implementacion base ejecutable.

## Recomendaciones de mejora (para despues)

1. Validar y sanear payloads antes de deserializar para evitar errores y datos malformados.
2. Definir un protocolo minimo de confiabilidad sobre UDP (ACK, reintentos y timeout).
3. Evitar valores hardcodeados en UI (IP/puerto) y moverlos a configuracion editable.
4. Persistir estado de sesion de jugadores (mapa de cliente, rol, estado en lobby).
5. Implementar reconexion y heartbeat para detectar desconexiones de forma robusta.
6. Reemplazar `Debug.Log` critico por logging estructurado con niveles.
7. Agregar pruebas unitarias para `PacketDispatcher`, `PlayerHandler` y `LobbyController`.
8. Documentar el flujo host/guest con un diagrama simple de mensajes en el README.
9. Revisar el desacoplamiento entre `Presentation` y `Network` para reducir acoplamiento futuro.
10. Definir convencion de versionado de paquetes para compatibilidad hacia adelante.

Cuando empieces a crear clases, intenta mantener esta regla de dependencias:

- `Domains` no depende de nadie.
- `Application` depende de `Domains`.
- `Network` y `Presentation` implementan/adaptan lo que `Application` necesita.

