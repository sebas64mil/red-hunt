using System;
using System.Linq;
using UnityEngine;

public class PresentationBootstrap : MonoBehaviour
{
    public PresentationServices Presentation { get; private set; }

    private LobbyUI queuedLobbyUI;
    private SpawnUI queuedSpawnUI;
    private bool installed = false;

    private ApplicationBootstrap appBootstrap;
    private NetworkBootstrap networkBootstrap;
    private LobbyNetworkService lobbyNetworkService;

    // AdminUI guardada para añadir/remover entradas automáticamente
    private AdminUI adminUI;

    // Predicate opcional para suprimir el ShowLobby para el jugador local
    // (por ejemplo cuando el usuario eligió "host" localmente)
    private Func<bool> shouldSuppressShowForLocal;

    public void Init(LobbyUI lobbyUI = null, SpawnUI spawnUI = null)
    {
        if (lobbyUI != null) queuedLobbyUI = lobbyUI;
        if (spawnUI != null) queuedSpawnUI = spawnUI;
        TryInstall();
    }

    public void RegisterLobbyUI(LobbyUI ui)
    {
        if (ui == null)
        {
            queuedLobbyUI = null;
            return;
        }

        queuedLobbyUI = ui;
        TryInstall();
    }

    public void RegisterSpawnUI(SpawnUI ui)
    {
        if (ui == null)
        {
            queuedSpawnUI = null;
            return;
        }

        queuedSpawnUI = ui;
        TryInstall();
    }

    // Permite que Presentation gestione AdminUI (añadir/remover/clear)
    public void RegisterAdminUI(AdminUI admin)
    {
        adminUI = admin;
    }

    // Attach application and network bootstraps so Presentation puede reaccionar a sus eventos
    public void AttachApplication(ApplicationBootstrap app)
    {
        appBootstrap = app ?? throw new ArgumentNullException(nameof(app));
        appBootstrap.OnPlayerJoined += HandlePlayerJoined;
        appBootstrap.OnPlayerLeft += HandlePlayerLeft;
    }

    public void AttachNetwork(NetworkBootstrap net)
    {
        networkBootstrap = net ?? throw new ArgumentNullException(nameof(net));
        lobbyNetworkService = net.GetLobbyNetworkService();

        networkBootstrap.OnPlayerIdAssigned += HandlePlayerIdAssigned;
        networkBootstrap.OnLocalJoinAccepted += HandleLocalJoinAccepted;
        networkBootstrap.OnClientDisconnected += HandleClientDisconnected;

        // Si Presentation ya está instalada, asegurar que el LobbyNetworkService tenga el SpawnManager
        if (installed && lobbyNetworkService != null && Presentation?.SpawnManager != null)
        {
            lobbyNetworkService.SpawnManagerInstance = Presentation.SpawnManager;
        }
    }

    /// <summary>
    /// Registrar un predicate que, cuando devuelve true, suprime la acción de mostrar el lobby
    /// para el jugador local tras recibir su PlayerId (comportamiento equivalente al original).
    /// </summary>
    public void RegisterShowSuppressionPredicate(Func<bool> predicate)
    {
        shouldSuppressShowForLocal = predicate;
    }

    private void TryInstall()
    {
        if (installed) return;

        if (queuedLobbyUI == null || queuedSpawnUI == null)
        {
            Debug.Log("[PresentationBootstrap] UIs incompletas, esperando registro.");
            return;
        }

        Presentation = new PresentationInstaller().Install(queuedLobbyUI, queuedSpawnUI, -1);

        // conectar spawn manager al lobbyNetworkService si ya existe
        if (lobbyNetworkService != null && Presentation?.SpawnManager != null)
            lobbyNetworkService.SpawnManagerInstance = Presentation.SpawnManager;

        installed = true;
        Debug.Log("[PresentationBootstrap] PresentationServices instaladas.");
    }

    private void HandlePlayerJoined(PlayerSession player)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] Player Joined: {player.Id}");
            if (Presentation?.SpawnUI != null)
                Presentation.SpawnUI.OnPlayerAssigned(player.Id, player.PlayerType);

            // actualizar AdminUI si está registrada
            adminUI?.AddPlayerEntry(player.Id);

            // intentar mostrar lobby para el jugador local si procede
            TryShowLobbyForLocalIfNeeded(player.Id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandlePlayerJoined: {e.Message}");
        }
    }

    private void HandlePlayerLeft(int playerId)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] Player Left: {playerId}");
            Presentation?.SpawnUI?.HandlePlayerDisconnected(playerId);

            // actualizar AdminUI si está registrada
            adminUI?.RemovePlayerEntry(playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandlePlayerLeft: {e.Message}");
        }
    }

    private void HandlePlayerIdAssigned(int id)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] PlayerId asignado: {id}");
            Presentation?.SpawnManager?.SetLocalPlayerId(id);
            Presentation?.LobbyUI?.SetLocalPlayerId(id);
            Presentation?.LobbyUI?.ResetReadyState();

            TryShowLobbyForLocalIfNeeded(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandlePlayerIdAssigned: {e.Message}");
        }
    }

    private void HandleLocalJoinAccepted(int id)
    {
        try
        {
            Debug.Log($"[PresentationBootstrap] LocalJoinAccepted para id: {id}");
            TryShowLobbyForLocalIfNeeded(id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandleLocalJoinAccepted: {e.Message}");
        }
    }

    private void HandleClientDisconnected()
    {
        try
        {
            Debug.Log("[PresentationBootstrap] Cliente desconectado - limpiando UI");

            Presentation?.LobbyUI?.ResetAllToMain();
            // limpiar spawns
            var players = appBootstrap?.Services?.LobbyManager?.GetAllPlayers();
            if (players != null)
            {
                foreach (var p in players)
                    Presentation?.SpawnUI?.HandlePlayerDisconnected(p.Id);
            }

            // limpiar admin UI si existe
            adminUI?.ClearAll();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en HandleClientDisconnected: {e.Message}");
        }
    }

    private void TryShowLobbyForLocalIfNeeded(int id)
    {
        try
        {
            if (Presentation?.LobbyUI == null || appBootstrap?.Services == null) return;

            // Preguntar al predicate si debemos suprimir mostrar el lobby (ej: elección host)
            if (shouldSuppressShowForLocal != null && shouldSuppressShowForLocal())
            {
                Debug.Log("[PresentationBootstrap] Supresión de mostrar lobby para local activada; omitiendo ShowLobbyPanel");
                return;
            }

            var clientState = networkBootstrap?.Services?.ClientState;
            if (clientState == null) return;
            if (clientState.PlayerId != id) return;

            var exists = appBootstrap.Services.LobbyManager.GetAllPlayers()?.Any(p => p.Id == id) ?? false;
            if (!exists)
            {
                Debug.Log($"[PresentationBootstrap] player {id} no está aún en LobbyManager, esperando broadcast");
                return;
            }

            Presentation.LobbyUI.ShowLobbyPanel();
            Presentation.LobbyUI.SetConnected(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PresentationBootstrap] Error en TryShowLobbyForLocalIfNeeded: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (appBootstrap != null)
            {
                appBootstrap.OnPlayerJoined -= HandlePlayerJoined;
                appBootstrap.OnPlayerLeft -= HandlePlayerLeft;
            }

            if (networkBootstrap != null)
            {
                networkBootstrap.OnPlayerIdAssigned -= HandlePlayerIdAssigned;
                networkBootstrap.OnLocalJoinAccepted -= HandleLocalJoinAccepted;
                networkBootstrap.OnClientDisconnected -= HandleClientDisconnected;
            }
        }
        catch { }
    }
}