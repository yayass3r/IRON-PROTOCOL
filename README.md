# ⚔️ IRON PROTOCOL

> **Modern Grand Strategy & Tactical Warfare** — Turn-Based Strategy Game for Android

![Unity](https://img.shields.io/badge/Unity-2023.4 LTS-000000?style=flat&logo=unity)
![C#](https://img.shields.io/badge/C%23-12-239120?style=flat&logo=csharp)
![Android](https://img.shields.io/badge/Android-10%2B-3DDC84?style=flat&logo=android)
![URP](https://img.shields.io/badge/Render-URP-7B68EE?style=flat)
![Platform](https://img.shields.io/badge/Platform-Android-green)

---

## 🎮 Game Overview

**IRON PROTOCOL** is a next-generation Turn-Based Strategy (TBS) game built with Unity Engine, merging deep tactical combat with grand strategic decision-making in a modern setting (2025+). Players begin as a minor nation and evolve into a global superpower through military conquest, economic warfare, and diplomatic maneuvering.

## 🏗️ Architecture

The project uses **modular Assembly Definitions** for clean separation:

| Assembly | Namespace | Purpose |
|----------|-----------|---------|
| `IronProtocol.Core` | `IronProtocol.Core` | EventBus, ServiceLocator, ObjectPool, SaveManager |
| `IronProtocol.HexMap` | `IronProtocol.HexMap` | Hex grid, terrain, camera controls |
| `IronProtocol.Military` | `IronProtocol.Military` | Units, combat, flanking, supply lines |
| `IronProtocol.Economy` | `IronProtocol.Economy` | Dynamic market, resources, alliance currency |
| `IronProtocol.Diplomacy` | `IronProtocol.Diplomacy` | Casus Belli, relations, occupation |
| `IronProtocol.AI` | `IronProtocol.AI` | Utility AI + Reinforcement Learning |
| `IronProtocol.Weather` | `IronProtocol.Weather` | Dynamic weather system |
| `IronProtocol.TurnSystem` | `IronProtocol.TurnSystem` | Turn phases, game flow |
| `IronProtocol.Multiplayer` | `IronProtocol.Multiplayer` | Async multiplayer, notifications |
| `IronProtocol.UI` | `IronProtocol.UI` | All UI screens and HUD |

## 📁 Project Structure

```
Assets/
├── _Core/Scripts/                    # Engine core (EventBus, DI, Pool, Save)
├── _GameSystems/
│   ├── HexMap/                       # Hex grid system
│   │   ├── Scripts/                  # HexCoord, HexCell, HexGrid, Renderer, Camera
│   │   └── ScriptableObjects/        # TerrainDefinition
│   ├── Military/                     # Combat system
│   │   ├── Scripts/
│   │   │   ├── GroundUnits/          # Infantry, Armor, Artillery
│   │   │   ├── AirUnits/             # Fighter, Bomber, Drone
│   │   │   ├── NavalUnits/           # Destroyer, Submarine, Carrier
│   │   │   ├── SpecialUnits/         # Cyber, SpecialForces, Missile
│   │   │   ├── UnitBase.cs           # Abstract base unit
│   │   │   ├── CombatResolver.cs     # Full combat engine
│   │   │   ├── FlankingSystem.cs     # Tactical flanking
│   │   │   ├── SupplyLineSystem.cs   # BFS supply tracing
│   │   │   └── CombinedArms.cs       # Synergy bonuses
│   │   └── ScriptableObjects/        # UnitDefinition
│   ├── Economy/                      # Market & resources
│   │   ├── Scripts/                  # DynamicPricing, ResourceManager, AllianceCurrency
│   │   └── ScriptableObjects/        # ResourceDefinition
│   ├── Diplomacy/                    # Political system
│   │   └── Scripts/                  # CasusBelli, DiplomacyManager, Occupation
│   ├── AI/                           # Adaptive AI
│   │   ├── Scripts/UtilityAI/        # StrategicAI, TacticalAI
│   │   └── Scripts/ReinforcementLearning/ # RLAgent
│   ├── Weather/Scripts/              # WeatherManager
│   ├── TurnSystem/Scripts/           # TurnController
│   └── Multiplayer/Scripts/          # AsyncTurnManager, Notifications
├── _UI/Scripts/                      # UIManager, HUD, Market, Battle, Diplomacy UI
├── _Data/ScriptableObjects/          # GameConfig, NationDefinition
├── _Art/                             # Sprites, Models, VFX, Fonts
├── _Audio/                           # Music, SFX
├── _Plugins/                         # Firebase, ML-Agents, DOTween, UniTask
├── _Scenes/                          # Unity scenes
└── Resources/                        # Runtime assets
```

## ⚔️ Core Systems

### Combat System
- **Flanking**: Side (+25% ATK), Rear (+50% ATK), Encirclement (-40% DEF + supply cut)
- **Combined Arms**: 6 synergy rules (Inf+Armor, Inf+Art, Air+Ground, etc.)
- **Suppression**: Artillery pins units (0-3 stacks), -30% DEF per stack
- **Supply Lines**: BFS tracing with interdiction points
- **Weather Effects**: Rain (-30% accuracy), Fog (-20%), Snow (-40% movement)

### Dynamic Market
- 6 resources: Oil, Steel, Silicon, Uranium, Food, Rare Earth
- 4-factor pricing: Supply/Demand, Event Shocks, Trends, Volatility
- Alliance currencies with exchange rates
- Economic warfare through market manipulation

### Adaptive AI
- **Utility AI**: 5 scored actions with RL-adjusted weights
- **Tactical AI**: Flanking-aware movement, priority targeting
- **Reinforcement Learning**: Q-learning with ε-greedy exploration

## 🚀 Getting Started

### Prerequisites
- **Unity 2023.4 LTS** (or Unity 6)
- **Android Build Support** module
- **TextMeshPro** package (via Package Manager)

### Setup
```bash
git clone https://github.com/yayass3r/IRON-PROTOCOL.git
# Open the project folder in Unity Hub
# Wait for Unity to import all assets
# Create game config: Assets → Right Click → Create → Iron Protocol → Game Config
# Create nations: Assets → Right Click → Create → Iron Protocol → Nation Definition
# Build: File → Build Settings → Android → Build
```

### Build for Android
1. Go to **File → Build Settings**
2. Switch platform to **Android**
3. Set minimum API level to **Android 10.0 (API 29)**
4. Set IL2CPP scripting backend
5. Configure keystore in **Player Settings → Publishing Settings**
6. Click **Build** or **Build And Run**

## 📊 Unit Types (12)

| Domain | Unit | ATK | DEF | HP | Special |
|--------|------|-----|-----|-----|---------|
| Ground | Infantry | 35 | 30 | 100 | City capture, +10 DEF urban |
| Ground | Armor | 55 | 45 | 150 | Breakthrough vs suppressed |
| Ground | Artillery | 70 | 15 | 60 | Suppression (2 stacks), min range 2 |
| Air | Fighter | 60 | 25 | 80 | Air superiority |
| Air | Bomber | 80 | 15 | 100 | Strategic bombing |
| Air | Drone | 35 | 10 | 40 | Long endurance, low cost |
| Naval | Destroyer | 50 | 40 | 120 | Versatile anti-sub |
| Naval | Submarine | 65 | 20 | 80 | Stealth, convoy raiding |
| Naval | Carrier | 30 | 50 | 200 | Power projection |
| Special | Cyber | 40 | 5 | 30 | Infrastructure hack (range 5) |
| Special | Spec Ops | 50 | 20 | 70 | Stealth insertion, HQ strike |
| Special | Missile | 200 | 0 | 1 | Strategic decapitation |

## 🛣️ Development Roadmap

| Phase | Timeline | Focus |
|-------|----------|-------|
| Phase 1 | Weeks 1-3 | Hex grid, terrain, camera, units, basic movement |
| Phase 2 | Weeks 4-6 | Combat, flanking, combined arms, AI (scripted) |
| Phase 3 | Weeks 7-9 | Market, diplomacy, occupation, UI screens |
| Phase 4 | Weeks 10-12 | AI (Utility + RL), weather, tech tree |
| Phase 5 | Weeks 13-15 | Multiplayer, Firebase, notifications |
| Phase 6 | Weeks 16-18 | Polish, optimization, QA, beta testing |

## 📜 License

This project is proprietary and confidential. All rights reserved.

---

*Lead Game Architect & Lead Tactical Game Designer | 2025*
