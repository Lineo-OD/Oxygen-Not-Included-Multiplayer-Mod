# 🎮 ONI Multiplayer

**Play Oxygen Not Included together with friends!**

> ⚠️ **Note:** This is in very early pre-alpha stage and still doesn't work properly.

A host-authoritative multiplayer mod for ONI where each player controls their own duplicants in a shared colony. Uses Steam P2P networking - no port forwarding required!

---

## ✨ Features

### 🎯 Core Multiplayer
- **Steam Integration** - Create lobbies, invite friends via Steam overlay
- **P2P Networking** - Direct connection through Steam relay, no port forwarding needed
- **Up to 3 Players** - Host + 2 clients in the same colony

### 🧑‍🤝‍🧑 Dupe Ownership
- Each player controls specific duplicants
- Visual indicators show who owns which dupe (colored labels above heads)
- Host can assign/reassign dupes via in-game panel
- Dupes only work on tasks assigned by their owner

### 🌍 World Synchronization
- Host runs the full simulation
- Clients see real-time world changes (digging, building, etc.)
- Desync detection with automatic warnings
- Save file validation ensures everyone has matching worlds

### 💾 Multiplayer Saves
- Separate save folder for multiplayer games
- Import single-player saves to multiplayer
- Hash validation prevents mismatched saves
- Metadata tracking (players, cycles, last played)

### 🔄 Connection Features
- Reconnection support (2-minute grace period)
- Toast notifications for all events
- Graceful disconnect handling

---

## 📦 Installation

### Manual Installation
1. Download the latest release
2. Copy the `release/` folder contents to your ONI mods folder:
   ```
   Documents\Klei\OxygenNotIncluded\mods\Local\OniMultiplayer\
   ```
3. Enable the mod in-game

### Build from Source
1. Clone this repository
2. Update `ONIGamePath` in `OniMultiplayer.csproj` to your ONI installation
3. Run `dotnet build`
4. Run `install.bat` to copy to your mods folder

---

## 🎮 How to Play

### As Host
1. Launch ONI with the mod enabled
2. Click **"Multiplayer"** in the main menu
3. Click **"Create Lobby"**
4. Select a save file (or import one from single-player)
5. Use Steam overlay (Shift+Tab) to invite friends
6. Once everyone joins, click **"Start Game"**
7. Assign dupes to players using the assignment panel

### As Client
1. Accept Steam invite from host
2. Click **"Join"** in the multiplayer menu
3. Wait for host to start the game
4. Control your assigned duplicants!

---

## 🛠️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                        HOST                              │
│  ┌─────────────────────────────────────────────────────┐│
│  │              Full Game Simulation                    ││
│  │  - World state, physics, resources                  ││
│  │  - All dupe AI and pathfinding                      ││
│  │  - Chore assignment and completion                  ││
│  └─────────────────────────────────────────────────────┘│
│                         │                                │
│                    Steam P2P                             │
│                         │                                │
└─────────────────────────┼───────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
┌───────────────┐ ┌───────────────┐ ┌───────────────┐
│   CLIENT 1    │ │   CLIENT 2    │ │   CLIENT 3    │
│  Render Only  │ │  Render Only  │ │  Render Only  │
│  Send Input   │ │  Send Input   │ │  Send Input   │
└───────────────┘ └───────────────┘ └───────────────┘
```

### Key Components
| Component | Purpose |
|-----------|---------|
| `SteamP2PManager` | Steam networking & packet routing |
| `SteamLobbyManager` | Lobby creation & player management |
| `DupeOwnership` | Tracks which player owns which dupes |
| `DupeSyncManager` | Broadcasts dupe positions & animations |
| `WorldSyncManager` | Syncs world changes (dig, build) |
| `ChorePatches` | Enforces dupe ownership of tasks |
| `ReconnectionManager` | Handles disconnects & reconnects |

---

## 📁 Project Structure

```
OniMultiplayer/
├── Components/          # Unity MonoBehaviours
├── Network/             # Networking (P2P, packets, handlers)
│   └── Packets/         # Packet definitions
├── Patches/             # Harmony patches for game hooks
├── Systems/             # Core multiplayer systems
├── UI/                  # UI components (screens, panels)
├── Tools/               # Dev tools (inspector, etc.)
├── docs/                # Documentation
└── release/             # Build output for distribution
```

---

## 🐛 Known Issues

- Some visual effects may not sync perfectly
- Large bases (1000+ tiles changed) may have initial sync delay
- Critters and plants don't have ownership (controlled by host AI)

---

## 🔮 Roadmap

- [ ] Steam Workshop publishing
- [ ] More than 3 players
- [ ] Spectator mode
- [ ] Critter ownership
- [ ] In-game chat

---

## 📄 License

This mod is provided as-is for personal use with Oxygen Not Included.

---

## 🙏 Credits

- **Klei Entertainment** - For creating ONI
- **Harmony** - Patching framework
- **Steamworks.NET** - Steam integration
- **LiteNetLib** - Packet serialization

---

*Made with ❤️ for the ONI community*
