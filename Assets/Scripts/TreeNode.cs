using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class TreeNode : NetworkBehaviour
{
    // =========================================================
    // ESTADO DEL ÁRBOL
    // =========================================================

    // Indica si el árbol tiene dueño (todos pueden leerlo).
    private NetworkVariable<bool> isClaimed =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Vida / cantidad de cortes.
    public NetworkVariable<int> logs =
        new NetworkVariable<int>(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Nombre del dueño.
    public NetworkVariable<FixedString32Bytes> ownerName =
        new NetworkVariable<FixedString32Bytes>(
            new FixedString32Bytes("Sin reclamar"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );


    // =========================================================
    // COLORES (editables desde el Inspector)
    // =========================================================

    [SerializeField] private Color freeColor = Color.white;    // sin dueño
    [SerializeField] private Color mineColor = Color.yellow;   // mío
    [SerializeField] private Color othersColor = Color.red;    // de otro jugador


    // =========================================================
    // TEXTO DEL ÁRBOL
    // =========================================================

    [SerializeField]
    private TextMeshProUGUI treeInfoText;


    // =========================================================
    // CLIC EN EL ÁRBOL
    // =========================================================

    private void OnMouseDown()
    {
        if (!IsSpawned)
            return;

        // Si está libre, cualquier jugador puede intentar reclamarlo.
        if (!isClaimed.Value)
        {
            ClaimTreeRpc();
        }

        // Si tiene dueño, solamente el dueño puede cortarlo.
        else if (IsOwner)
        {
            ChopTreeRpc();
        }

        // Otro jugador intenta tocar un árbol ocupado.
        else
        {
            Debug.Log("[Cliente] No puedes cortar este árbol, pertenece a " + ownerName.Value + ".");
        }
    }


    // =========================================================
    // RECLAMAR EL ÁRBOL
    // =========================================================

    [Rpc(SendTo.Server)]
    private void ClaimTreeRpc(RpcParams rpcParams = default)
    {
        // El servidor valida nuevamente que esté libre.
        if (isClaimed.Value)
        {
            Debug.Log("[Servidor] Reclamo rechazado: el árbol ya pertenece a " + ownerName.Value + ".");
            return;
        }

        // Identificamos al jugador que hizo clic.
        ulong who = rpcParams.Receive.SenderClientId;

        // El árbol queda reclamado.
        isClaimed.Value = true;

        // El servidor entrega la propiedad al jugador.
        NetworkObject.ChangeOwnership(who);

        // Guardamos el nombre del dueño.
        ownerName.Value =
            new FixedString32Bytes("Player " + who);

        Debug.Log("[Servidor] Player " + who + " reclamó el árbol.");
    }


    // =========================================================
    // CORTAR EL ÁRBOL
    // =========================================================

    [Rpc(SendTo.Server)]
    private void ChopTreeRpc(RpcParams rpcParams = default)
    {
        // El servidor valida que quien corta sea el dueño real.
        if (!isClaimed.Value ||
            rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning("[Servidor] Corte rechazado: Player " +
                rpcParams.Receive.SenderClientId + " no es el dueño del árbol.");
            return;
        }

        // Restamos una vida.
        logs.Value--;

        Debug.Log(
            "[Servidor] Corte realizado. Cortes restantes: " + logs.Value
        );

        // Cuando se queda sin vidas...
        if (logs.Value <= 0)
        {
            // El árbol vuelve a estar libre.
            NetworkObject.RemoveOwnership();

            isClaimed.Value = false;

            // Eliminamos el dueño.
            ownerName.Value =
                new FixedString32Bytes("Sin reclamar");

            // Reiniciamos la vida.
            logs.Value = 3;

            Debug.Log("[Servidor] El árbol se agotó y vuelve a estar libre.");
        }
    }


    // =========================================================
    // CALLBACKS DE VARIABLES DE RED
    // =========================================================

    private void OnClaimedChanged(bool previous, bool current)
    {
        ChangeColor();
    }

    private void OnLogsChanged(int previous, int current)
    {
        Debug.Log("[Cliente] Cortes restantes del árbol: " + current);

        UpdateTreeInfo();
    }

    private void OnOwnerNameChanged(
        FixedString32Bytes previous,
        FixedString32Bytes current)
    {
        Debug.Log("[Cliente] Dueño actual del árbol: " + current);

        UpdateTreeInfo();
    }


    // =========================================================
    // CAMBIOS DE PROPIEDAD DEL NETWORKOBJECT
    // =========================================================

    // isClaimed y la propiedad llegan en momentos distintos,
    // por eso se refresca el color también aquí.
    public override void OnGainedOwnership()
    {
        ChangeColor();
    }

    public override void OnLostOwnership()
    {
        ChangeColor();
    }


    // =========================================================
    // CUANDO EL ÁRBOL APARECE EN LA RED
    // =========================================================

    public override void OnNetworkSpawn()
    {
        // Nos suscribimos a los cambios de las variables de red.
        isClaimed.OnValueChanged += OnClaimedChanged;
        logs.OnValueChanged += OnLogsChanged;
        ownerName.OnValueChanged += OnOwnerNameChanged;

        // Aplicamos inmediatamente el estado actual
        // (importante para jugadores que entran tarde).
        ChangeColor();
        UpdateTreeInfo();

        Debug.Log(
            "[Cliente] Árbol sincronizado -> " +
            "Reclamado: " + isClaimed.Value +
            " | Cortes restantes: " + logs.Value +
            " | Dueño: " + ownerName.Value
        );
    }


    // =========================================================
    // CUANDO EL ÁRBOL SALE DE LA RED
    // =========================================================

    public override void OnNetworkDespawn()
    {
        isClaimed.OnValueChanged -= OnClaimedChanged;
        logs.OnValueChanged -= OnLogsChanged;
        ownerName.OnValueChanged -= OnOwnerNameChanged;
    }


    // =========================================================
    // CAMBIAR COLOR VISUAL
    // =========================================================

    private void ChangeColor()
    {
        Renderer renderer = GetComponent<Renderer>();

        if (renderer == null)
            return;

        if (!isClaimed.Value)
            renderer.material.color = freeColor;      // sin dueño: blanco
        else if (IsOwner)
            renderer.material.color = mineColor;      // mío: amarillo
        else
            renderer.material.color = othersColor;    // de otro: rojo
    }


    // =========================================================
    // ACTUALIZAR INFORMACIÓN SOBRE EL ÁRBOL
    // =========================================================

    private void UpdateTreeInfo()
    {
        if (treeInfoText == null)
            return;

        treeInfoText.text =
            ownerName.Value.ToString() +
            "\nVida: " +
            logs.Value;
    }
}