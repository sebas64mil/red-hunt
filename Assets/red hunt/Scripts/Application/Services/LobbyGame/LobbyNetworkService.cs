using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using UnityEngine;

public class LobbyNetworkService : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private BroadcastService broadcastService;
    private PacketBuilder packetBuilder;
    private ClientState clientState;
    private ClientPacketHandler clientPacketHandler;

    private bool isHost;
    public bool IsHost => isHost;

    public event Action<string> OnStartGameReceived;
    public event Action<int> OnLocalJoinAccepted;
    public event Action OnReturnToLobbyReceived; 
    public event Action<int, string, bool> OnGameWinReceived; 

    public bool GameStarted { get; private set; } = false;

    public SpawnManager SpawnManagerInstance { get; set; }

    private IServer server;
    private ClientConnectionManager connectionManager;


    public void Init(
        LobbyManager lobbyManager,
        BroadcastService broadcastService,
        PacketBuilder packetBuilder,
        bool isHost,
        ClientState clientState = null,
        ClientPacketHandler clientPacketHandler = null,
        IServer server = null,
        ClientConnectionManager connectionManager = null)
    {
        this.lobbyManager = lobbyManager;
        this.broadcastService = broadcastService;
        this.packetBuilder = packetBuilder;
        this.isHost = isHost;
        this.clientState = clientState;
        this.clientPacketHandler = clientPacketHandler;

        this.server = server;
        this.connectionManager = connectionManager;

        this.clientState = clientState;
        if (this.clientState != null)
        {
            this.clientState.OnPlayerIdAssigned += id =>
            {
                try
                {
                    SpawnManagerInstance?.SetLocalPlayerId(id);
                }
                catch { }

                try
                {
                    ModularLobbyBootstrap.Instance?.GetComponent<PlayerCameraBootstrap>()?.SetLocalPlayerId(id);
                }
                catch { }
            };
        }

        lobbyManager.OnPlayerJoined += async (player) => await HandlePlayerJoined(player);
        lobbyManager.OnPlayerReady += async (id) => await HandlePlayerReady(id);

        lobbyManager.OnPlayerLeft += HandlePlayerLeft;
    }

    public void SetIsHost(bool value)
    {
        this.isHost = value;
    }

    public void JoinLobby(string playerType = null)
    {
        string resolvedType = playerType;

        if (string.IsNullOrEmpty(resolvedType))
        {
            resolvedType = isHost
                ? PlayerType.Killer.ToString()
                : PlayerType.Escapist.ToString();
        }

            Debug.Log($"[LobbyNetworkService] JoinLobby called. isHost={isHost}, resolvedType={resolvedType}");

        // Si el host ya inició la partida, no permitir joins
        if (GameStarted)
        {
            Debug.LogWarning("[LobbyNetworkService] JoinLobby ignorado: la partida ya ha comenzado.");
            return;
        }

        if (!isHost)
        {
            clientState?.SetPendingPlayerType(resolvedType);
            return;
        }

        try
        {
            clientState?.SetPlayerId(1);
            clientState?.SetConnected(true);
            clientState?.SetIsHost(true); 
        }
        catch { }

        var added = lobbyManager.AddPlayerRemote(1, resolvedType);
        if (added != null)
        {
            SpawnManagerInstance?.SpawnRemotePlayer(added.Id, ParsePlayerType(added.PlayerType));

            if (clientState != null && clientState.PlayerId == added.Id)
            {
                try
                {
                    OnLocalJoinAccepted?.Invoke(added.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LobbyNetworkService] Error invocando OnLocalJoinAccepted: {e.Message}");
                }
            }

            return;
        }

        var command = new JoinLobbyCommand(resolvedType);
        lobbyManager.ExecuteCommand(command);
    }
    public async Task LeaveLobby()
    {
        Debug.Log("[LobbyNetworkService] Leaving lobby...");

        if (!isHost)
        {
            if (clientPacketHandler != null)
            {
                await clientPacketHandler.SendDisconnect();
                await Task.Delay(100);
            }

            try
            {
                var players = lobbyManager.GetAllPlayers().ToList();
                foreach (var p in players)
                {
                    lobbyManager.RemovePlayerRemote(p.Id);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error limpiando estado local en LeaveLobby: {e.Message}");
            }

            return;
        }

        Debug.Log("[LobbyNetworkService] Host leave requested - implementar cierre del host si es necesario");
        await ShutdownServer();
    }


    public async Task ShutdownServer()
    {
        Debug.Log("[LobbyNetworkService] ShutdownServer: enviando DISCONNECT a todos los clientes");

        try
        {
            var packet = packetBuilder.CreateDisconnect();
            await broadcastService.SendToAll(packet);

            await Task.Delay(100);

            server?.Disconnect();
            connectionManager?.Clear();

            var players = lobbyManager.GetAllPlayers().ToList();
            foreach (var p in players)
            {
                lobbyManager.RemovePlayerRemote(p.Id);
                SpawnManagerInstance?.RemovePlayer(p.Id);
            }

            Debug.Log("[LobbyNetworkService] DISCONNECT enviado, el Server debe desconectarse ahora.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error en ShutdownServer: {e.Message}");
        }
    }

    public async Task StartGame(string sceneName)
    {
        if (!isHost)
        {
            return;
        }

        // Evitar múltiples llamadas a StartGame
        if (GameStarted)
        {
            Debug.LogWarning("[LobbyNetworkService] StartGame ya fue invocado - ignorando llamada duplicada");
            return;
        }

        try
        {
            // Marcar que la partida empezó para bloquear joins posteriores
            GameStarted = true;

            Debug.Log($"[LobbyNetworkService] Enviando START_GAME('{sceneName}') a todos los clientes");
            var startPacket = packetBuilder.CreateStartGame(sceneName);
            await broadcastService.SendToAll(startPacket);

            // Dar tiempo a que los clientes carguen la escena y registren su SpawnUI
            const int postStartDelayMs = 500;
            await Task.Delay(postStartDelayMs);

            // Reenviar estado de players para que cada cliente pueda spawnear sus players en la nueva escena
            Debug.Log("[LobbyNetworkService] Reenviando PLAYER packets tras START_GAME");
            var allPlayers = lobbyManager.GetAllPlayers().ToList();
            foreach (var p in allPlayers)
            {
                string packetJson = packetBuilder.CreatePlayer(p.Id, p.PlayerType.ToString());
                await broadcastService.SendToAll(packetJson);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error en StartGame: {e.Message}");
        }
    }

    private async Task HandlePlayerJoined(PlayerSession player)
    {
        if (!isHost) return;

        Debug.Log("[LobbyNetworkService] Broadcasting PLAYER packets");

        var allPlayers = lobbyManager.GetAllPlayers();
        int hostId = allPlayers.Min(p => p.Id);

        foreach (var p in allPlayers)
        {
            string packetJson = packetBuilder.CreatePlayer(p.Id, p.PlayerType.ToString());
            await broadcastService.SendToAll(packetJson);
        }
    }

    private async Task HandlePlayerReady(int playerId)
    {
        Debug.Log($"[LobbyNetworkService] Player Ready: {playerId}");

        string packetJson = packetBuilder.CreatePlayerReady(playerId);
        await broadcastService.SendToAll(packetJson);
    }

    private void HandlePlayerLeft(int id)
    {
        Debug.Log($"[LobbyNetworkService] Removing player {id}");

        SpawnManagerInstance?.RemovePlayer(id);
    }

    // ==================== EVENTOS DE RED ====================

    public void HandlePacketReceived(string packetJson)
    {
        if (string.IsNullOrEmpty(packetJson)) return;

        var packetType = packetBuilder.GetPacketType(packetJson);

        switch (packetType)
        {
            case "PLAYER":
                HandlePlayerPacket(packetJson);
                break;

            case "PLAYER_READY":
                HandlePlayerReadyPacket(packetJson);
                break;

            case "ASSIGN_PLAYER":
                HandleAssignPlayerPacket(packetJson);
                break;

            case "REMOVE_PLAYER":
                HandleRemovePlayerPacket(packetJson);
                break;

            case "ASSIGN_REJECT":
                Debug.Log("[LobbyNetworkService] ASSIGN_REJECT recibido");
                break;

            case "START_GAME":
                HandleStartGamePacket(packetJson);
                break;

            case "RETURN_TO_LOBBY":
                Debug.Log("[LobbyNetworkService] RETURN_TO_LOBBY recibido");
                HandleReturnToLobbyPacket();
                break;

            case "WIN_GAME": 
                HandleWinGamePacket(packetJson);
                break;

            case "HEALTH_UPDATE":  // ⭐ NUEVO
                HandleHealthUpdatePacket(packetJson);
                break;

            default:
                Debug.LogWarning($"Paquete desconocido recibido: {packetType}");
                break;
        }
    }

    // ⭐ NUEVO: Manejador para HEALTH_UPDATE
    private void HandleHealthUpdatePacket(string json)
    {
        var packet = packetBuilder.DeserializeHealthUpdate(json);
        if (packet == null)
        {
            Debug.LogWarning("[LobbyNetworkService] HEALTH_UPDATE packet inválido");
            return;
        }

        Debug.Log($"[LobbyNetworkService] HEALTH_UPDATE recibido: playerId={packet.playerId}, health={packet.currentHealth}/{packet.maxHealth}");

        // ✅ SI SOY HOST: Rebroadcastear a todos los clientes
        if (isHost && broadcastService != null)
        {
            Debug.Log($"[LobbyNetworkService] 📡 Soy HOST - rebroadcasteando HEALTH_UPDATE a todos");
            _ = broadcastService.SendToAll(json);
        }

        // ✅ Aplicar el update localmente (todos procesan)
        try
        {
            var targetPlayerGO = SpawnManagerInstance?.GetPlayerGameObject(packet.playerId);
            if (targetPlayerGO != null)
            {
                var escapistHealth = targetPlayerGO.GetComponent<EscapistHealth>();
                if (escapistHealth != null)
                {
                    escapistHealth.SetHealth(packet.currentHealth);
                    Debug.Log($"[LobbyNetworkService] ✅ Salud actualizada localmente para player {packet.playerId}: {packet.currentHealth}/{packet.maxHealth}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error actualizando salud local: {e.Message}");
        }
    }

    // ⭐ Manejador para el paquete WIN (cuando el HOST lo recibe de un cliente)
    private void HandleWinGamePacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<WinGamePacket>(json);
        if (packet == null)
        {
            Debug.LogWarning("[LobbyNetworkService] WIN_GAME packet inválido");
            return;
        }

        Debug.Log($"[LobbyNetworkService] WIN_GAME recibido: winnerId={packet.winnerId}, winnerType={packet.winnerType}, isKillerWin={packet.isKillerWin}");

        // ✅ SI SOY HOST: REBROADCASTEAR A TODOS (incluyendo al cliente que lo envió)
        if (isHost)
        {
            Debug.Log($"[LobbyNetworkService] 📡 Soy HOST - rebroadcasteando WIN_GAME a todos los clientes");
            _ = BroadcastGameWin(packet.winnerId, packet.winnerType, packet.isKillerWin);
        }

        // ✅ INVOCAR EVENTO PARA QUE TODOS PROCESEN (host y cliente)
        try
        {
            OnGameWinReceived?.Invoke(packet.winnerId, packet.winnerType, packet.isKillerWin);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error invocando OnGameWinReceived: {e.Message}");
        }
    }

    // ⭐ NUEVO: Método auxiliar para rebroadcastear WIN_GAME
    private async Task BroadcastGameWin(int winnerId, string winnerType, bool isKillerWin)
    {
        if (!isHost || broadcastService == null) return;

        try
        {
            var winPacket = packetBuilder.CreateWinGame(winnerId, winnerType, isKillerWin);
            
            Debug.Log($"[LobbyNetworkService] 📤 Reenviando WIN_GAME a todos: winnerId={winnerId}");
            
            // ✅ Esperar a que se envíe a todos
            await broadcastService.SendToAll(winPacket);
            
            // ✅ Después de enviar, esperar un poco para asegurar recepción
            await Task.Delay(100);
            
            Debug.Log("[LobbyNetworkService] ✅ WIN_GAME reenviado a todos los clientes");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] ❌ Error rebroadcasteando WIN_GAME: {e.Message}");
        }
    }

    private void HandlePlayerPacket(string json)
    {
        var playerPacket = packetBuilder.Serializer.Deserialize<PlayerPacket>(json);
        if (playerPacket == null) return;

        string requestedType = playerPacket.playerType;

        if (isHost && GameStarted)
        {
            var existsHost = lobbyManager.GetAllPlayers().FirstOrDefault(p => p.Id == playerPacket.id);
            if (existsHost == null)
            {
                Debug.LogWarning($"[LobbyNetworkService] Ignorando intento de join de player {playerPacket.id} porque la partida ya ha comenzado.");

                try
                {
                    var rejectPacket = packetBuilder.CreateAssignReject(playerPacket.id, "Game already started");

                    if (connectionManager != null && server != null)
                    {
                        if (connectionManager.TryGetEndpointById(playerPacket.id, out IPEndPoint endpoint) && endpoint != null)
                        {
                            _ = server.SendToClientAsync(rejectPacket, endpoint);
                            Debug.Log($"[LobbyNetworkService] ASSIGN_REJECT dirigido enviado a {endpoint} para player {playerPacket.id}");

                            try
                            {
                                var disconnectPacket = packetBuilder.CreateDisconnect();
                                _ = server.SendToClientAsync(disconnectPacket, endpoint);
                                Debug.Log($"[LobbyNetworkService] DISCONNECT dirigido enviado a {endpoint} para player {playerPacket.id}");
                            }
                            catch (Exception exDisconnect)
                            {
                                Debug.LogWarning($"[LobbyNetworkService] Error enviando DISCONNECT dirigido a {endpoint}: {exDisconnect.Message}");
                            }

                            // Eliminar endpoint del connection manager para liberar slot/recursos
                            try
                            {
                                connectionManager.RemoveClient(endpoint);
                                Debug.Log($"[LobbyNetworkService] Endpoint {endpoint} eliminado del ConnectionManager para player {playerPacket.id}");
                            }
                            catch (Exception exRemove)
                            {
                                Debug.LogWarning($"[LobbyNetworkService] Error eliminando endpoint del ConnectionManager: {exRemove.Message}");
                            }

                            return;
                        }
                    }

                    // Fallback: si no hay endpoint disponible, usar broadcast como antes
                    _ = broadcastService.SendToAll(rejectPacket);
                    Debug.Log($"[LobbyNetworkService] ASSIGN_REJECT enviado por broadcast para player {playerPacket.id}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LobbyNetworkService] Error enviando ASSIGN_REJECT: {e.Message}");
                }

                return;
            }
        }

        if (isHost)
        {
            var currentPlayers = lobbyManager.GetAllPlayers().ToList();
            bool anyKiller = currentPlayers.Any(p => p.PlayerType == PlayerType.Killer.ToString());

            int hostId = currentPlayers.Any() ? currentPlayers.Min(p => p.Id) : -1;
            bool hostIsEscapist = false;
            if (hostId != -1)
            {
                var hostPlayer = currentPlayers.FirstOrDefault(p => p.Id == hostId);
                hostIsEscapist = hostPlayer != null && hostPlayer.PlayerType == PlayerType.Escapist.ToString();
            }

            if (hostIsEscapist && !anyKiller)
            {
                if (requestedType != PlayerType.Killer.ToString())
                {
                    Debug.Log("[LobbyNetworkService] Host es Escapist y no hay Killer; promoviendo este cliente a Killer");
                    requestedType = PlayerType.Killer.ToString();
                }
            }
            else
            {
                if (requestedType == PlayerType.Killer.ToString() && anyKiller)
                {
                    Debug.Log("[LobbyNetworkService] Ya existe un Killer; asignando Escapist en su lugar");
                    requestedType = PlayerType.Escapist.ToString();
                }
            }
        }

        var existing = lobbyManager.GetAllPlayers().FirstOrDefault(p => p.Id == playerPacket.id);
        if (existing != null)
        {
            if (existing.PlayerType != requestedType)
            {
                Debug.Log($"[LobbyNetworkService] Actualizando tipo player {playerPacket.id} -> {requestedType}");
                var updated = lobbyManager.UpdatePlayerTypeRemote(playerPacket.id, requestedType);
                if (updated)
                {
                    SpawnManagerInstance?.RemovePlayer(playerPacket.id);
                    SpawnManagerInstance?.SpawnRemotePlayer(
                        playerPacket.id,
                        ParsePlayerType(requestedType)
                    );
                }
            }

            return;
        }

        var added = lobbyManager.AddPlayerRemote(playerPacket.id, requestedType);
        if (added == null)
        {
            Debug.LogWarning($"[LobbyNetworkService] Player {playerPacket.id} no pudo ser añadido (lobby lleno u error)");
            
            try
            {
                var rejectPacket = packetBuilder.CreateAssignReject(playerPacket.id, "Lobby full or internal error");
                
                if (connectionManager != null && server != null)
                {
                    if (connectionManager.TryGetEndpointById(playerPacket.id, out IPEndPoint endpoint) && endpoint != null)
                    {
                        _ = server.SendToClientAsync(rejectPacket, endpoint);
                        Debug.Log($"[LobbyNetworkService] ASSIGN_REJECT dirigido enviado a {endpoint} para player {playerPacket.id}");
                    }
                }
                else
                {
                    _ = broadcastService.SendToAll(rejectPacket);
                    Debug.Log($"[LobbyNetworkService] ASSIGN_REJECT enviado por broadcast para player {playerPacket.id}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error enviando ASSIGN_REJECT: {e.Message}");
            }
            
            return;
        }

        if (clientState != null && clientState.PlayerId == added.Id)
        {
            try
            {
                OnLocalJoinAccepted?.Invoke(added.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error invocando OnLocalJoinAccepted: {e.Message}");
            }
        }
    }

    private void HandlePlayerReadyPacket(string json)
    {
        var readyPacket = packetBuilder.Serializer.Deserialize<PlayerReadyPacket>(json);
        if (readyPacket == null) return;

        var existing = lobbyManager.GetAllPlayers().FirstOrDefault(p => p.Id == readyPacket.id);
        if (existing != null)
        {
            return;
        }

        var added = lobbyManager.AddPlayerRemote(readyPacket.id, PlayerType.Escapist.ToString());
        if (added == null) return;

        SpawnManagerInstance?.SpawnRemotePlayer(
            added.Id,
            ParsePlayerType(added.PlayerType)
        );
    }

    private void HandleAssignPlayerPacket(string json)
    {
        var assignPacket = packetBuilder.Serializer.Deserialize<AssignPlayerPacket>(json);
        if (assignPacket == null) return;

        Debug.Log($"[LobbyNetworkService] Received ASSIGN for id {assignPacket.id}");
    }

    private void HandleRemovePlayerPacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<RemovePlayerPacket>(json);
        if(packet == null) return;

        Debug.Log($"[LobbyNetworkService] Player {packet.id} disconnected");

        lobbyManager.RemovePlayerRemote(packet.id);
    }

    private void HandleStartGamePacket(string json)
    {
        var packet = packetBuilder.Serializer.Deserialize<StartGamePacket>(json);
        if (packet == null)
        {
            Debug.LogWarning("[LobbyNetworkService] START_GAME recibido pero packet inválido");
            return;
        }

        Debug.Log($"[LobbyNetworkService] START_GAME recibido -> escena: {packet.sceneName}");
        try
        {
            OnStartGameReceived?.Invoke(packet.sceneName);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyNetworkService] Error invocando OnStartGameReceived: {e.Message}");
        }
    }

    public void AddRemotePlayer(int id, string type)
    {
        lobbyManager.AddPlayerRemote(id, type);
    }

    public void AddRemotePlayer(int id, PlayerType type)
    {
        lobbyManager.AddPlayerRemote(id, type.ToString());
    }

    public void SetPlayerReady(int id)
    {
        lobbyManager.SetPlayerReady(id);
    }

    private PlayerType ParsePlayerType(string type)
    {
        return type == PlayerType.Killer.ToString()
            ? PlayerType.Killer
            : PlayerType.Escapist;
    }

    public async Task ReturnAllPlayersToLobby()
    {
        if (!isHost)
        {
            Debug.LogWarning("[LobbyNetworkService] ReturnAllPlayersToLobby: solo el host puede usarlo");
            return;
        }

        Debug.Log("[LobbyNetworkService] Enviando RETURN_TO_LOBBY a todos los clientes");

        try
        {
            var returnPacket = packetBuilder.CreateReturnToLobby();
            await broadcastService.SendToAll(returnPacket);
            
            Debug.Log("[LobbyNetworkService] RETURN_TO_LOBBY enviado a todos los clientes");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error en ReturnAllPlayersToLobby: {e.Message}");
        }
    }
    private void HandleReturnToLobbyPacket()
    {
        try
        { 
            GameStarted = false;
            
            try
            {
                var players = lobbyManager.GetAllPlayers().ToList();
                foreach (var p in players)
                {
                    SpawnManagerInstance?.RemovePlayer(p.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyNetworkService] Error limpiando players: {e.Message}");
            }
            
            OnReturnToLobbyReceived?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error en HandleReturnToLobbyPacket: {e.Message}");
        }
    }

    public void ResetGameStarted()
    {
        GameStarted = false;
    }



    // ⭐ NUEVO: Versión async de SendGameWinToHost
    public async Task SendGameWinToHostAsync(int winnerId, string winnerType, bool isKillerWin)
    {
        if (isHost)
        {
            Debug.LogWarning("[LobbyNetworkService] SendGameWinToHostAsync: Solo clientes deben usar esto");
            return;
        }

        if (clientPacketHandler == null)
        {
            Debug.LogError("[LobbyNetworkService] SendGameWinToHostAsync: ClientPacketHandler no disponible");
            return;
        }

        try
        {
            await clientPacketHandler.SendGameWin(winnerId, winnerType, isKillerWin);
            Debug.Log($"[LobbyNetworkService] ✅ WIN_GAME enviado AL HOST (async)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] Error enviando WIN_GAME al host: {e.Message}");
        }
    }

    // ⭐ HOST: Enviar WIN_GAME a TODOS y esperar antes de cambiar escena
    public async Task SendGameWinAsync(int winnerId, string winnerType, bool isKillerWin)
    {
        if (!isHost)
        {
            Debug.LogWarning("[LobbyNetworkService] SendGameWinAsync: Solo el host puede enviar WIN_GAME a todos");
            return;
        }

        if (broadcastService == null)
        {
            Debug.LogError("[LobbyNetworkService] SendGameWinAsync: BroadcastService no disponible");
            return;
        }

        try
        {
            var winPacket = packetBuilder.CreateWinGame(winnerId, winnerType, isKillerWin);
            
            Debug.Log($"[LobbyNetworkService] 📡 Enviando WIN_GAME a TODOS: winnerId={winnerId}, winnerType={winnerType}");
            
            // ✅ ESPERAR a que se envíe a todos los clientes
            await broadcastService.SendToAll(winPacket);
            
            // ✅ DESPUÉS de enviar, procesar localmente
            HandleWinGamePacket(winPacket);
            
            
            Debug.Log("[LobbyNetworkService] ✅ WIN_GAME enviado a todos los clientes - seguro cambiar escena");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyNetworkService] ❌ Error en SendGameWinAsync: {e.Message}\n{e.StackTrace}");
        }
    }

    // ⭐ Mantener para compatibilidad
    public async void SendGameWin(int winnerId, string winnerType, bool isKillerWin)
    {
        await SendGameWinAsync(winnerId, winnerType, isKillerWin);
    }

    // ⭐ NUEVO: Método para resetear el estado de win (usado al volver al lobby)
    public void ResetGameWin()
    {
        GameStarted = false;
        Debug.Log("[LobbyNetworkService] Game win state resetted");
    }
}