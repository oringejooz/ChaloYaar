// ─────────────────────────────────────────────────────────────────────────────
//  SETUP_GUIDE.cs  — Not a runtime file. Read-only reference for configuring
//                    Chalo Yaar! systems in the Unity inspector.
// ─────────────────────────────────────────────────────────────────────────────

/*
═══════════════════════════════════════════════════════════════
  CHALO YAAR! — PLAYER SYSTEMS SETUP GUIDE
═══════════════════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────────┐
│  STEP 1 — Player GameObject hierarchy                       │
└─────────────────────────────────────────────────────────────┘

  [Player Root GameObject]
    ├── PlayerMovementAdvanced.cs       (existing)
    ├── PlayerStats.cs                  (NEW)
    ├── PlayerNeeds.cs                  (NEW)
    ├── PlayerClass.cs                  (NEW)
    ├── PlayerController.cs             (NEW — facade)
    ├── Inventory.cs                    (NEW)
    └── [Camera Child]
          ├── SimpleFPSController.cs    (existing)
          ├── InteractionSystem.cs      (NEW)
          └── PhotographySystem.cs      (NEW)

┌─────────────────────────────────────────────────────────────┐
│  STEP 2 — Scene-level GameObjects                           │
└─────────────────────────────────────────────────────────────┘

  [GameManager]  (DontDestroyOnLoad, persistent)
    └── MoneySystem.cs

  [Van]
    └── VanSystemsHub.cs
        └── (child colliders with van interactables below)

┌─────────────────────────────────────────────────────────────┐
│  STEP 3 — Van Interior Object Setup                         │
└─────────────────────────────────────────────────────────────┘

  For each prop, add the matching Interactable script, set
  Layer to "Interactable", and wire VanSystemsHub reference.

  Prop                   →  Script
  ──────────────────────────────────────────────
  Refrigerator           →  FridgeInteractable
  Stove                  →  StoveInteractable
  Sink                   →  SinkInteractable
  Trash Bin              →  TrashBinInteractable
  Wrench                 →  WrenchInteractable
  Water Dispenser        →  WaterDispenserInteractable
  Bunk Bed (×4)          →  BunkBedInteractable
  Handheld Console       →  HandheldConsoleInteractable

  Also add a Collider on each prop and set it to the
  "Interactable" layer. InteractionSystem's raycast uses
  that layer mask.

┌─────────────────────────────────────────────────────────────┐
│  STEP 4 — Create CharacterData assets                       │
└─────────────────────────────────────────────────────────────┘

  Right-click in Project → Chalo Yaar → Character Data
  Create one per playable character. Suggested baseline:

  Name       Gender  Perk 1          Perk 2        Notes
  ─────────────────────────────────────────────────────────
  Arjun      M       Mechanic        —             Repairs cost 50% less
  Priya      F       Photographer    —             20% photo sell bonus
  Kabir      M       NightOwl        —             Drowsiness 40% slower
  Sanya      F       Chef            IronStomach   Cooked food +20%, no flatulence

  Leave stat multipliers at 1.0 until you balance in playtesting.

┌─────────────────────────────────────────────────────────────┐
│  STEP 5 — Create ItemData assets                            │
└─────────────────────────────────────────────────────────────┘

  Right-click → Chalo Yaar → Item Data
  Pre-made consumables from GDD:

  Item          Hunger  Thirst  Stamina  Drowsy  Side Effect
  ──────────────────────────────────────────────────────────
  Energy Bar    +20     -5      +15      0       none
  Apple         +8      +5      +5       0       none
  Coffee        0       -10     +30      0       Delayed drowsiness +20 after 120s
  Burger        +30     -10     +10      0       none
  Cheese Block  +15     -8      0        0       causesFlatulence = true
  Juice Box     +5      +25     +10      0       none

  Set ItemType = Consumable on all food items.
  Set isStackable = true, maxStack = 5 for most food.

┌─────────────────────────────────────────────────────────────┐
│  STEP 6 — Input Manager                                     │
└─────────────────────────────────────────────────────────────┘

  Add these to Edit → Project Settings → Input Manager:

  Name            Key          Used by
  ──────────────────────────────────────
  Interact        E            InteractionSystem
  Camera          F (toggle)   PhotographySystem
  Fire1           Mouse0       PhotographySystem (shutter)

┌─────────────────────────────────────────────────────────────┐
│  STEP 7 — PlayerMovementAdvanced.cs patch                   │
└─────────────────────────────────────────────────────────────┘

  Open PlayerMovementAdvanced.cs and add one public property
  anywhere inside the class body:

      public bool IsSprinting => _isSprinting;

  PlayerNeeds reads this to know when to drain stamina.

┌─────────────────────────────────────────────────────────────┐
│  STEP 8 — HUDManager canvas structure                       │
└─────────────────────────────────────────────────────────────┘

  [Canvas]
    ├── [StatBars]
    │     ├── HungerSlider
    │     ├── ThirstSlider
    │     ├── StaminaSlider
    │     └── DrowsinessSlider
    ├── [MoneyText]   (TMP)
    ├── [InteractPrompt]
    │     └── PromptText  (TMP)
    ├── [AlertToast]
    │     └── AlertText   (TMP)
    └── [VanHUD]
          ├── FuelSlider
          ├── VanHealthSlider
          ├── GasSlider
          ├── WaterSlider
          └── TrashSlider

  Assign all references to HUDManager in the inspector.

═══════════════════════════════════════════════════════════════
  NETCODE NOTE (Phase 3)
═══════════════════════════════════════════════════════════════

  PlayerStats, Inventory, and MoneySystem are all designed to
  be authority-friendly. When adding Netcode for GameObjects:

  • PlayerStats   → NetworkVariable<float> per stat, owned by client
  • MoneySystem   → NetworkVariable<int>, owned by host/server
  • Inventory     → NetworkList<ItemSlot>, owned by client
  • VanSystemsHub → NetworkVariable<float> per resource, owned by host
  • InteractionSystem → ServerRpc + ClientRpc for Interact calls

═══════════════════════════════════════════════════════════════
*/
