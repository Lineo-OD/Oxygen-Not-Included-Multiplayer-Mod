# Changelog

All notable changes to ONI Multiplayer will be documented in this file.

---

## [0.1.0] - 2024-12-04

### 🎉 Initial Release

#### Core Features
- **Steam P2P Networking** - No port forwarding required
- **Lobby System** - Create/join via Steam overlay invites
- **Up to 3 Players** - Host + 2 clients

#### Dupe System
- Player dupe ownership assignment
- Visual ownership indicators (colored labels above dupes)
- Dupe state synchronization (position, animation, vitals)
- Persistent dupe IDs across sessions

#### World Synchronization  
- Dig/deconstruct sync
- Building placement sync
- Desync detection with warnings

#### Save System
- Separate multiplayer save folder (`save_files_mp/`)
- Save file hash validation
- Import from single-player saves
- Metadata tracking

#### Stability
- Reconnection support (2-minute grace period)
- Toast notification system for errors/events
- Cell-based chore ownership enforcement

---

## Planned Features

- [ ] Steam Workshop publishing
- [ ] More than 3 players
- [ ] Spectator mode
- [ ] In-game chat
- [ ] Critter/plant ownership