# Item creator

Open **Tools > UnscriptedEngine > Gameplay > Items**. Choose an existing root folder
inside Assets, enter an item name, and click **Create Item** once. Missing child folders
are created. Display name, stack capacity, and world pickup quantity are optional settings.
Item names must start with a letter and contain only letters, digits, or underscores.

**Model override (optional)** accepts a prefab or imported model asset (for example,
FBX or OBJ). Leave it empty to keep the default cube. Both world and held prefabs use
the same nested artwork asset, preserving its authored position, rotation, scale,
materials, and hierarchy. Later artwork changes propagate through that shared reference.
The selection is retained across the compilation/reload step, including asset moves.

The generated world BoxCollider fits the artwork's renderer bounds at creation time;
adjust it if the artwork changes size afterward. Artwork colliders are disabled and
rigidbodies made kinematic in both generated instances so the model follows its item.
The source asset is not edited. Use an artwork prefab without network components;
the creator supplies networking on the world item root.

Choose **Default behaviour** in the creator (also editable on the world prefab):

- **None:** interaction performs no built-in item action. Existing/custom interaction
  events can still run.
- **Pick Up:** any interacting object with a `UInventoryComponent` on itself or a child
  can collect the item; no player/pawn type is required. Full or missing inventories
  leave the item in the world. This remains the default for existing items.

For root `Assets/MyGame` and item `Flashlight`, output is:

```text
Assets/MyGame/
  Scripts/Items/Flashlight/
    FlashlightBehaviour.cs
    FlashlightFragment.cs
    FlashlightVisual.cs
    FlashlightWorldItem.cs
  Data/Items/
    Flashlight.asset
  Prefabs/Items/Flashlight/
    FlashlightWorld.prefab
    FlashlightHeld.prefab
```

Generated classes use namespace `Game.Items`. The tool refuses existing output files,
orphan metadata, and conflicting script names; it never replaces custom code or assets.

Unity first compiles the four scripts. After the domain reload, creation automatically
resumes and wires:

- ItemDataSO: details, stack limit, equip fragment referencing the held prefab, and the
  generated behaviour fragment.
- Held prefab: the generated visual MonoBehaviour and the selected model, or a small
  placeholder cube. No active artwork collider or NetworkObject is added to the held visual.
- World prefab: NetworkObject, generated UWorldItem subclass, instant/repeatable
  UInteractableComponent, BoxCollider, and selected model (or placeholder cube). Pickup references the same
  ItemDataSO and the configured quantity.

With cube fallback, replace the `Model (replace with artwork)` child in each prefab.
With a model override, edit the shared artwork asset to update both visuals.
Adjust the world's BoxCollider if the artwork changes size. Put gameplay settings on the generated fragment,
presentation references on the visual component, and actual actions in the behaviour.
The generated Use method deliberately handles no action until you implement one. Connect
your pawn's actions to ItemEquipComponent.Use and cancellation to CancelUse as described
in [ItemBehaviours](ItemBehaviours.md).

To let players drop the generated item from its selected slot, add `UItemFragDrop` to its
`ItemDataSO` and assign the generated world prefab. `ItemEquipComponent.Drop` uses that
prefab and its configured drop origin/speed. See [Inventory System](InventorySystem.md)
and [ItemEquipComponent](ItemEquipComponent.md).

Pickup adds the full quantity to the interacting source's child UInventoryComponent.
The inventory must be initialized. If it is missing or full, the world item remains
available. Successful pickup despawns a networked item on the server, or destroys an
offline item. Spawned `UInventoryComponent` contents replicate automatically; save-game
persistence and game-specific server rules remain project setup. The editor generates
`Assets/Resources/UnscriptedItemCatalog.asset` from item assets and rebuilds it before
Play Mode and builds. Register the world prefab in your game's NetworkPrefabsList when
spawning it dynamically over the network. The tool does not change scenes or
NetworkManager configuration.

If compilation fails, fix Console errors and use **Finish Pending Item**. A custom
assembly definition containing generated scripts must reference UnscriptedEngine and
the assemblies needed by your extensions. Generation state survives script reloads in
the current Editor session. Do not close the Editor while creation is pending.

Failed asset creation rolls back only assets created by that attempt and retains source
files for retry. **Cancel Pending Item** clears the pending operation but retains its
scripts and folders. To abandon it permanently, remove those generated files yourself
or choose a different item name.
