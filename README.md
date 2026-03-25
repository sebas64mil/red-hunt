# Red Hunt - Organizacion del Proyecto

Este proyecto sigue una organizacion por capas dentro de la carpeta `Assets/red hunt/Scripts`.

## Estructura General

```text
Assets/
	red hunt/
		Scripts/
			Application/
				Gameplay/
				Mappers/
				Services/
				Systems/
			Domains/
				data/
				Entities/
				Enums/
				Interfaces/
			Network/
				Dispatching/
				Handlers/
				Interfaces/
				Lobby/
				Packets/
				Serialization/
				Transport/
			Presentation/
				Animation/
				Sounds/
				UI/
				VFX/
```

## Que hace cada capa

### 1) Domains (Reglas de negocio)
Esta capa define el nucleo del juego: entidades, contratos y tipos del dominio.

- `Entities/`: modelos principales del juego (jugador, partida, estado, etc.).
- `Enums/`: enumeraciones para estados y valores cerrados del dominio.
- `Interfaces/`: contratos del dominio (puertos) que otras capas implementan.
- `data/`: estructuras de datos puras del dominio.

Regla: no debe depender de Unity visual (UI/VFX) ni de detalles concretos de red.

### 2) Application (Casos de uso)
Coordina la logica del juego usando el dominio.

- `Gameplay/`: flujo de reglas jugables (inicio, rondas, validaciones de partida).
- `Services/`: servicios de aplicacion que orquestan casos de uso.
- `Systems/`: procesos de alto nivel que conectan varios servicios/casos.
- `Mappers/`: conversion entre modelos (por ejemplo DTO <-> entidades de dominio).

Regla: usa `Domains` y puede invocar infraestructura (red, presentacion) por interfaces.

### 3) Network (Comunicacion multiplayer)
Contiene toda la logica de transporte y mensajes de red.

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

