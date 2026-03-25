using UnityEngine;

public class PlayerBootstrap : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Server server;
    [SerializeField] private Client client;
    [SerializeField] private LobbyUI lobbyUI;

    private ISerializer serializer;
    private PacketDispatcher dispatcher;
    private PlayerHandler playerHandler;
    private LobbyController lobbyController;

    private void Awake()
    {
        InitPlayerLayer();
        BindUI();
    }

    private void InitPlayerLayer()
    {
        serializer = new JsonSerializer();

        dispatcher = new PacketDispatcher(serializer);

        playerHandler = new PlayerHandler(serializer);

        dispatcher.Register("PLAYER", playerHandler.Handle);

        server.Init(dispatcher, serializer);

        PacketBuilder builder = new PacketBuilder();

        lobbyController = new LobbyController(
            server,
            client,
            serializer,
            builder
        );

        Debug.Log("[PlayerBootstrap] Player system initialized");
    }

    private void BindUI()
    {
        lobbyUI.OnCreateLobby += lobbyController.CreateLobby;
        lobbyUI.OnJoinLobby += lobbyController.JoinLobby;
    }
}