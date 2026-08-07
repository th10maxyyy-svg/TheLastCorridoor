# 🏥 The Last Corridoor - Game Design Document (GDD) & Master Plan

## 1. Game Overview
- **Title:** The Last Corridoor
- **Genre:** First-Person Survival Horror / Stealth Escape
- **Inspirations:** Granny, Kamla, Outlast, Amnesia.
- **Core Loop:** The player wakes up in an abandoned asylum and has 5 Days (Lives) to escape. A terrifying Stalker AI hunts the player dynamically by listening to noise and tracking sights. The player must find keys, tools, and solve puzzles to unlock the main escape door or the hidden basement exit.

## 2. Core Mechanics
### 2.1 Player Controller
- **Movement:** Walk, Sprint (consumes stamina), Crouch (silent movement).
- **Headbob:** Dynamic camera bobbing based on movement speed.
- **Interaction (E):** Raycast-based system. Pick up items, open unlocked doors, hide under beds.
- **Inventory System (G):** 1-Slot inventory system. The player physically holds the item in their hand. Pressing 'G' throws the item.
- **Flashlight (F):** A dim light source that can be toggled. If left on, it may attract the enemy faster in dark corridors.

### 2.2 Stealth & Noise System
- **Hiding:** Player can hide inside `Cupboard` or under `Bed` & `SurgeryTable`. The enemy will lose line of sight if the player hides before being spotted.
- **Noise Generation:** Dropping items (`CanOld`, `Plate`, `Mug`) with physics generates a noise event at the exact location.
- **Footsteps:** Sprinting makes louder footsteps (larger noise radius), while crouching makes the player completely silent.

### 2.3 The Stalker AI (`EnemyAI.cs`)
- **State Machine:**
  - `Patrol`: Wanders the Asylum randomly. Occasionally uses "Granny Sense" to move towards the general area of the player.
  - `InvestigateSound`: Hears a dropped item or loud footstep and runs to the exact coordinate. Looks around for 3 seconds before going back to Patrol.
  - `Chase`: Spots the player with its FOV vision cone. Sprints towards the player.
  - `Attack`: Instantly deals massive damage / triggers jumpscare when in range.
- **Failsafes:** Uses a breadcrumb system to teleport the AI safely if it glitches out of the NavMesh or gets stuck in a wall.

## 3. Level Design (The Asylum)
*Using the `Abandoned_Asylum` Asset Pack*

### 3.1 Map Layout
- **Ground Floor:** Main lobby with the locked `MainDoor`. Corridors leading to the Kitchen, Toilets, and Day Room.
- **First Floor:** Surgery rooms, Patient wards (Beds for hiding), and Doctor's office.
- **Basement/Morgue:** Dark, terrifying area filled with `MorgueBox` prefabs. Contains the secret alternate exit.

### 3.2 Key Locations & Puzzles
- **The Red Key:** Spawns randomly in the `KitchenShelf` or inside a `Toilet`. Unlocks the Surgery Room.
- **The Blue Key:** Hidden inside a `MorgueBox`. Unlocks the Security Room.
- **The Master Key:** Found in the Security Room. Unlocks the `MainDoor`.
- **Cutting Pliers:** Used to cut the electric wire blocking the Basement exit.

### 3.3 Hiding Spots Placement
- Place `Cupboard.prefab` in the corridors and wards.
- Place `Bed.prefab` in patient rooms.
- Place `SurgeryTable.prefab` in the surgery room.

### 3.4 Noise Traps
- Carefully place `CanOld.prefab` and `Plate_Broken.prefab` on the edge of tables. If the player bumps into the table, the items fall, alerting the AI.

## 4. Atmosphere & Lighting
### 4.1 Visuals
- **Skybox:** Pitch black.
- **Lighting:** Most `NeonLamp` and `Chandelier` lights will be broken or completely off. Only a few will use `FlickeringLight.cs` to create an eerie vibe.
- **Post-Processing:** 
  - *Vignette:* Darkens the edges of the screen.
  - *Film Grain:* Adds a classic horror movie dirty look.
  - *Color Grading:* Desaturated (greyish/blue) colors.

### 4.2 Audio
- **Ambient:** Constant low drone sound, wind howling.
- **Enemy Audio:** Heavy footsteps (wood creaking) that get louder as the AI approaches.
- **Chase Music:** Tense, high-bpm string instruments that trigger when the AI sees the player.

## 5. Development Roadmap (Action Plan)

### Phase 1 & 2: Scripts (COMPLETED ✅)
- Player movement, camera, inventory, item drop physics.
- Enemy AI NavMesh, Patrol, Chase, Hearing, and Vision.

### Phase 3: World Building (Next Step)
- [ ] Create an empty scene. Place `Asylum_BaseConstruction.prefab`.
- [ ] Mark all floors and walls as `Navigation Static`.
- [ ] Bake the NavMesh (`Window -> AI -> Navigation`).
- [ ] Place the `MainDoor` and set it to Locked.
- [ ] Place `Player` and `Enemy` in the scene.

### Phase 4: Interaction Setup
- [ ] Add `HidingSpot.cs` to Cupboards and Beds. Configure Enter/Exit points.
- [ ] Place `CanOld` and `Mug` items around the map. Add `ThrowableItem.cs` and Rigidbody to them.
- [ ] Place Keys in specific rooms.

### Phase 5: Game Loop & UI
- [ ] Create `GameManager.cs` logic for the "5 Days" system.
- [ ] Create a Main Menu with "Play" and "Settings".
- [ ] Add a Game Over Jump-scare screen.
- [ ] Final Optimization (Bake lighting, Occlusion Culling).

---
*Document Version: 1.0*
*Created for Git Repository - The Last Corridoor*
