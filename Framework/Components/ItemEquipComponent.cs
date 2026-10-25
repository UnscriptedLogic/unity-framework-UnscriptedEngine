using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.Item;
using Unity.Netcode;
using Unity.Collections;

public class ItemEquipComponent : UObjectComponent
{
    [SerializeField, Tooltip("Component implementing IInventory to observe. Can also be assigned through SetInventory.")]
    private Component inventoryComponent;
    [SerializeField, Min(0), Tooltip("Number of inventory slots available for selection. Limited by inventory size. The pawn controls which slot is selected.")]
    private int hotbarSize = 10;
    [SerializeField, Tooltip("Parent transform for held visuals. Uses this transform when unassigned.")]
    private Transform equipAnchor;

    [Header("Dropping")]
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0.75f);
    [SerializeField, Min(0f)] private float dropSpeed = 2f;

    [Header("Debug")]
    [SerializeField, Tooltip("Log inventory observation, slot selection, and held visual changes.")]
    private bool debug;

    private IInventory inventory;
    private bool observingInventory;
    private GameObject heldVisual;
    private ItemDataSO heldItem;
    private ItemBehaviour heldBehaviour;
    private int heldSlotIndex = -1;
    private bool refreshing;
    private bool clearing;
    private readonly NetworkVariable<int> selectedSlot = new(-1);
    private readonly NetworkVariable<BehaviourSnapshot> behaviourState = new();
    private readonly NetworkVariable<EquipAnchorPose> replicatedAnchor = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private Transform proxyAnchor;
    private uint equipmentSession;
    private bool networkUsePending;
    private bool awaitingSelection;
    private uint previousSelectionSession;
    private readonly System.Collections.Generic.Queue<(string Action, bool Pressed)> queuedUse = new();

    public struct BehaviourSnapshot : INetworkSerializable, System.IEquatable<BehaviourSnapshot>
    {
        public int Slot;
        public uint Session;
        public FixedString64Bytes ItemId;
        public FixedString4096Bytes State;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Session);
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref State);
        }

        public bool Equals(BehaviourSnapshot other) => Slot == other.Slot && Session == other.Session
            && ItemId.Equals(other.ItemId) && State.Equals(other.State);
    }

    public struct EquipAnchorPose : INetworkSerializable, System.IEquatable<EquipAnchorPose>
    {
        public bool Valid;
        public Vector3 Position;
        public Quaternion Rotation;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Valid);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }

        public bool Equals(EquipAnchorPose other) => Valid == other.Valid
            && Position.Equals(other.Position) && Rotation.Equals(other.Rotation);
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        selectedSlot.OnValueChanged += ApplySelectedSlot;
        behaviourState.OnValueChanged += ApplyBehaviourState;
        replicatedAnchor.OnValueChanged += ApplyAnchorPose;
        UpdateHeldAnchor();
        if (!IsServer) ApplySelectedSlot(-1, selectedSlot.Value);
        else PublishBehaviourState();
    }

    private void ApplySelectedSlot(int previous, int current)
    {
        SelectedSlotIndex = current;
        RefreshHeldItem();
    }

    public IInventory Inventory => inventory;
    public int SelectedSlotIndex { get; private set; } = -1;
    public int VisibleSlotCount => IsInventoryAvailable()
        ? Mathf.Min(Mathf.Max(0, hotbarSize), Mathf.Max(0, inventory.InventorySize)) : 0;
    public ItemDataSO HeldItem => heldItem;
    public GameObject HeldVisual => heldVisual;

    private Transform LocalEquipAnchor => equipAnchor != null ? equipAnchor : transform;

    private Transform GetVisualAnchor()
    {
        if (!IsSpawned || IsOwner) return LocalEquipAnchor;
        if (proxyAnchor == null)
        {
            // Camera rigs are commonly disabled on proxies. Their transforms remain
            // usable as authored fallbacks, but their children cannot be rendered.
            proxyAnchor = new GameObject("Proxy held item anchor").transform;
            proxyAnchor.SetParent(NetworkObject.transform, false);
            proxyAnchor.SetPositionAndRotation(LocalEquipAnchor.position, LocalEquipAnchor.rotation);
            ApplyAnchorPose(default, replicatedAnchor.Value);
        }
        return proxyAnchor;
    }

    private void UpdateHeldAnchor()
    {
        if (!IsSpawned) return;
        if (IsOwner)
        {
            Transform root = NetworkObject.transform;
            var pose = new EquipAnchorPose
            {
                Valid = true,
                Position = root.InverseTransformPoint(LocalEquipAnchor.position),
                Rotation = Quaternion.Inverse(root.rotation) * LocalEquipAnchor.rotation
            };
            var previous = replicatedAnchor.Value;
            if (!previous.Valid || (previous.Position - pose.Position).sqrMagnitude > 0.000001f
                || Quaternion.Angle(previous.Rotation, pose.Rotation) > 0.1f)
                replicatedAnchor.Value = pose;
        }
        else ApplyAnchorPose(default, replicatedAnchor.Value);

        if (heldVisual != null)
        {
            Transform anchor = GetVisualAnchor();
            if (heldVisual.transform.parent != anchor) heldVisual.transform.SetParent(anchor, false);
        }
    }

    private void ApplyAnchorPose(EquipAnchorPose previous, EquipAnchorPose current)
    {
        if (IsOwner || proxyAnchor == null || !current.Valid || !IsFinite(current.Position)) return;
        Quaternion rotation = current.Rotation;
        float length = Quaternion.Dot(rotation, rotation);
        if (float.IsNaN(length) || float.IsInfinity(length) || length < 0.0001f) return;
        proxyAnchor.localPosition = current.Position;
        proxyAnchor.localRotation = rotation.normalized;
    }

    private void ClearProxyAnchor()
    {
        if (proxyAnchor == null) return;
        if (Application.isPlaying) Destroy(proxyAnchor.gameObject);
        else DestroyImmediate(proxyAnchor.gameObject);
        proxyAnchor = null;
    }

    /// <summary>Drop selected items, or queue an owning client's validated server request. Client success means queued.</summary>
    public bool Drop(Vector3 direction, int quantity = 1)
    {
        if (!isActiveAndEnabled || refreshing || clearing || inventory is not UInventoryComponent source
            || GetSelectedItem() == null || direction.sqrMagnitude < 0.0001f) return false;
        if (!IsFinite(direction) || !source.DropsAllowed) return false;
        if (IsSpawned && !IsServer)
        {
            if (!IsOwner) return false;
            RequestDropRpc(SelectedSlotIndex, GetSelectedItem().NetworkId, direction, quantity);
            return true;
        }
        return DropAuthoritative(SelectedSlotIndex, direction, quantity);
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDropRpc(int slot, FixedString64Bytes expectedItem, Vector3 direction, int quantity, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != OwnerClientId || !isActiveAndEnabled || !IsFinite(direction)
            || direction.sqrMagnitude < 0.0001f || slot < 0 || slot >= VisibleSlotCount
            || inventory is not UInventoryComponent source || source.Items == null
            || !source.IsSpawned || source.OwnerClientId != rpc.Receive.SenderClientId
            || !source.Items.TryGetValue(slot, out var item) || item.Item.NetworkId != expectedItem.ToString()) return;
        DropAuthoritative(slot, direction, quantity);
    }

    private bool DropAuthoritative(int slot, Vector3 direction, int quantity)
    {
        if (inventory is not UInventoryComponent source) return false;
        direction.Normalize();
        Vector3 position = dropOrigin != null ? dropOrigin.position : transform.TransformPoint(dropOffset);
        var result = source.TryDropItem(slot, position, Quaternion.LookRotation(direction),
            out UWorldItem pickup, quantity);
        if (!result.Success) { Log(result.Message); return false; }
        if (pickup != null && pickup.TryGetComponent<Rigidbody>(out var body))
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = direction * dropSpeed;
        }
        return true;
    }

    /// <summary>Replicated behaviours execute on the server. Client true means input consumed/request queued.</summary>
    public bool Use(string action, bool pressed)
    {
        if (!isActiveAndEnabled || !CanTick || refreshing || clearing || string.IsNullOrEmpty(action)) return false;
        if (IsSpawned && !IsServer && !IsOwner) return false;
        if (heldBehaviour == null) return false;
        if (!heldBehaviour.ReplicatesState) return heldBehaviour.Use(action, pressed);
        if (!heldBehaviour.HandlesAction(action) || System.Text.Encoding.UTF8.GetByteCount(action) > 61) return false;
        if (IsSpawned && !IsServer)
        {
            if (IsUseSessionReady())
            {
                RequestUseRpc(heldSlotIndex, heldItem.NetworkId, behaviourState.Value.Session, action, pressed, false);
                networkUsePending = true;
            }
            else if (queuedUse.Count < 16) queuedUse.Enqueue((action, pressed));
            return true;
        }
        bool handled = heldBehaviour.Use(action, pressed);
        PublishBehaviourState();
        return handled;
    }

    /// <summary>Call on input focus loss or unpossession, even when gameplay input is blocked.</summary>
    public void CancelUse()
    {
        queuedUse.Clear();
        if (IsSpawned && !IsServer)
        {
            if (!IsOwner) return;
            if (networkUsePending && heldBehaviour?.ReplicatesState == true && MatchesHeldItem(behaviourState.Value))
                RequestUseRpc(heldSlotIndex, heldItem.NetworkId, behaviourState.Value.Session, default, false, true);
        }
        networkUsePending = false;
        heldBehaviour?.CancelUse();
        PublishBehaviourState();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestUseRpc(int slot, FixedString64Bytes expectedItem, uint session,
        FixedString64Bytes action, bool pressed, bool cancel, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != OwnerClientId || !isActiveAndEnabled || !CanTick
            || refreshing || clearing || heldBehaviour?.ReplicatesState != true
            || slot != heldSlotIndex || slot != SelectedSlotIndex || session != equipmentSession
            || heldItem == null || heldItem.NetworkId != expectedItem.ToString() || GetSelectedItem() != heldItem
            || inventory is not UInventoryComponent source || !source.IsSpawned
            || source.OwnerClientId != rpc.Receive.SenderClientId) return;
        if (cancel) heldBehaviour.CancelUse();
        else if (heldBehaviour.HandlesAction(action.ToString())) heldBehaviour.Use(action.ToString(), pressed);
        PublishBehaviourState();
    }

    private bool MatchesHeldItem(BehaviourSnapshot state) => state.Session != 0 && heldItem != null
        && state.Slot == heldSlotIndex && state.ItemId.ToString() == heldItem.NetworkId;

    private bool IsUseSessionReady() => MatchesHeldItem(behaviourState.Value)
        && (!awaitingSelection || behaviourState.Value.Session != previousSelectionSession);

    private void PublishBehaviourState()
    {
        if (!IsSpawned || !IsServer) return;
        behaviourState.Value = new BehaviourSnapshot
        {
            Slot = heldSlotIndex,
            Session = equipmentSession,
            ItemId = heldItem != null ? heldItem.NetworkId : string.Empty,
            State = heldBehaviour?.ReplicatesState == true ? heldBehaviour.CaptureState() : string.Empty
        };
    }

    private void ApplyBehaviourState(BehaviourSnapshot previous, BehaviourSnapshot current)
    {
        if (IsServer || heldBehaviour?.ReplicatesState != true || !IsUseSessionReady()) return;
        heldBehaviour.ApplyState(current.State.ToString());
        awaitingSelection = false;
        if (!IsOwner || !CanTick) return;
        while (queuedUse.Count > 0)
        {
            var input = queuedUse.Dequeue();
            RequestUseRpc(heldSlotIndex, heldItem.NetworkId, current.Session, input.Action, input.Pressed, false);
            networkUsePending = true;
        }
    }

    protected override void Tick(float deltaTime)
    {
        UpdateHeldAnchor();
        heldBehaviour?.Tick(deltaTime);
        PublishBehaviourState();
    }
    protected override void FixedTick(float deltaTime) => heldBehaviour?.FixedTick(deltaTime);

    protected override void OnDespawned()
    {
        awaitingSelection = false;
        selectedSlot.OnValueChanged -= ApplySelectedSlot;
        behaviourState.OnValueChanged -= ApplyBehaviourState;
        replicatedAnchor.OnValueChanged -= ApplyAnchorPose;
        StopObservingInventory();
        ClearHeldVisual();
        ClearProxyAnchor();
        base.OnDespawned();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Log("Enabled; observing inventory.");
        
        if (inventory == null)
        {
            inventory = inventoryComponent as IInventory;
        }

        if (inventory == null && inventoryComponent != null)
        {
            inventory = inventoryComponent.GetComponent<IInventory>();
        }
        
        ObserveInventory();
        
        
        RefreshHeldItem();
    }

    protected override void OnDisable()
    {
        Log("Disabled; removing subscriptions and clearing held visual.");
        StopObservingInventory();
        ClearHeldVisual();
        base.OnDisable();
    }

    protected override void OnDestroyed()
    {
        StopObservingInventory();
        ClearHeldVisual();
        ClearProxyAnchor();
        base.OnDestroyed();
    }

    protected override void BeginPlay()
    {
        base.BeginPlay();
        ObserveInventory();
        RefreshHeldItem();
    }

    protected override void EndPlay()
    {
        StopObservingInventory();
        ClearHeldVisual();
        base.EndPlay();
    }

    public void SetInventory(IInventory newInventory)
    {
        if (refreshing || clearing) throw new System.InvalidOperationException("Cannot replace inventory during equipment callbacks.");
        awaitingSelection = false;
        StopObservingInventory();
        inventory = newInventory;
        inventoryComponent = newInventory as Component;
        if (debug) Log($"Inventory assigned: {newInventory}.");
        SelectedSlotIndex = -1;
        ClearHeldVisual();
        if (!isActiveAndEnabled) return;
        ObserveInventory();
        RefreshHeldItem();
    }

    private bool IsInventoryAvailable()
    {
        return inventory != null && (!(inventory is Object unityObject) || unityObject != null);
    }

    private void ObserveInventory()
    {
        if (observingInventory) return;
        if (!IsInventoryAvailable())
        {
            Log("Cannot observe inventory: assign a component implementing IInventory or call SetInventory.");
            return;
        }
        inventory.Changed += RefreshHeldItem;
        observingInventory = true;
        if (debug) Log($"Observing inventory with {inventory.InventorySize} slots; {VisibleSlotCount} hotbar slots available.");
    }

    private void StopObservingInventory()
    {
        if (!observingInventory) return;
        inventory.Changed -= RefreshHeldItem;
        observingInventory = false;
        Log("Stopped observing inventory.");
    }

    public bool SelectSlot(int slotIndex)
    {
        if (IsSpawned && !IsServer && !IsOwner) return false;
        if (refreshing || clearing || !isActiveAndEnabled || slotIndex < 0 || slotIndex >= VisibleSlotCount)
        {
            if (debug) Log($"Selection rejected: slot {slotIndex}, available slots {VisibleSlotCount}, active {isActiveAndEnabled}.");
            return false;
        }
        if (debug) Log($"Selecting slot {slotIndex}; previous slot {SelectedSlotIndex}.");
        if (IsSpawned && !IsServer && SelectedSlotIndex != slotIndex)
        {
            awaitingSelection = true;
            previousSelectionSession = behaviourState.Value.Session;
        }
        SelectedSlotIndex = slotIndex;
        if (IsSpawned)
        {
            if (IsServer) selectedSlot.Value = slotIndex;
            else SelectSlotRpc(slotIndex);
        }
        RefreshHeldItem();
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SelectSlotRpc(int slot, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId == OwnerClientId) SelectSlot(slot);
    }

    private void RefreshHeldItem()
    {
        if (refreshing || clearing || !isActiveAndEnabled) return;
        refreshing = true;
        try { RefreshHeldItemInternal(); }
        finally { refreshing = false; }
        if (IsServer) PublishBehaviourState();
        else ApplyBehaviourState(default, behaviourState.Value);
    }

    private void RefreshHeldItemInternal()
    {
        int slotCount = VisibleSlotCount;
        if (debug) Log($"Refreshing held item; selected slot {SelectedSlotIndex}, available slots {slotCount}.");
        SelectedSlotIndex = slotCount == 0 ? -1 : Mathf.Clamp(SelectedSlotIndex, 0, slotCount - 1);
        if (IsSpawned && IsServer) selectedSlot.Value = SelectedSlotIndex;
        ItemDataSO item = GetSelectedItem();
        if (item != null && item == heldItem && heldSlotIndex == SelectedSlotIndex
            && (heldVisual != null || FindEquipFragment(item)?.VisualPrefab == null)) return;
        ClearHeldVisual();
        if (!IsSpawned || IsServer)
        {
            unchecked { equipmentSession++; }
            if (equipmentSession == 0) equipmentSession++;
        }
        if (!isActiveAndEnabled || item == null) return;
        UItemFragEquip fragment = FindEquipFragment(item);
        if (fragment != null && fragment.VisualPrefab != null)
        {
            Transform anchor = GetVisualAnchor();
            heldVisual = Instantiate(fragment.VisualPrefab, anchor, false);
            heldVisual.transform.localPosition = fragment.AnchorPosition;
            heldVisual.transform.localRotation = Quaternion.Euler(fragment.AnchorRotation);
        }
        heldItem = item;
        heldSlotIndex = SelectedSlotIndex;
        try
        {
            if (item.fragments != null)
                foreach (UItemFragment candidate in item.fragments)
                    if (candidate is UItemFragBehaviour factory)
                    {
                        heldBehaviour = factory.CreateBehaviour(new ItemBehaviourContext(
                            this, inventory, item, heldSlotIndex, heldVisual));
                        heldBehaviour?.Equip();
                        break;
                    }
        }
        catch
        {
            ClearHeldVisual();
            throw;
        }
        if (debug) Log($"Equipped {item.name} from slot {heldSlotIndex}.");
    }

    private ItemDataSO GetSelectedItem()
    {
        if (SelectedSlotIndex < 0 || !IsInventoryAvailable() || inventory.Items == null) return null;
        return inventory.Items.TryGetValue(SelectedSlotIndex, out InventorySlot slot)
            && slot != null && slot.Quantity > 0 ? slot.Item : null;
    }

    private static UItemFragEquip FindEquipFragment(ItemDataSO item)
    {
        if (item == null || item.fragments == null) return null;
        foreach (UItemFragment fragment in item.fragments)
        {
            if (fragment is UItemFragEquip equipFragment) return equipFragment;
        }
        return null;
    }

    private void ClearHeldVisual()
    {
        if (clearing) return;
        clearing = true;
        try { ClearHeldVisualInternal(); }
        finally { clearing = false; }
    }

    private void ClearHeldVisualInternal()
    {
        // Detach first: callbacks can disable/destroy this component recursively.
        ItemBehaviour behaviour = heldBehaviour;
        queuedUse.Clear();
        networkUsePending = false;
        GameObject visual = heldVisual;
        heldBehaviour = null;
        heldVisual = null;
        heldItem = null;
        heldSlotIndex = -1;
        if (behaviour != null)
        {
            Cleanup(behaviour.CancelUse);
            Cleanup(behaviour.Unequip);
            Cleanup(behaviour.Dispose);
        }
        if (visual == null) return;
        visual.SetActive(false);
        if (Application.isPlaying) Destroy(visual);
        else DestroyImmediate(visual);
    }

    private void Cleanup(System.Action callback)
    {
        try { callback(); }
        catch (System.Exception exception) { UnityEngine.Debug.LogException(exception, this); }
    }

    private void Log(string message)
    {
        if (!debug) return;
        UnityEngine.Debug.Log($"[ItemEquipComponent] {message}", this);
    }

}

