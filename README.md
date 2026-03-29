# ⚔️ IRON PROTOCOL

> **Modern Grand Strategy & Tactical Warfare** — Turn-Based Strategy Game for Android

![Unity](https://img.shields.io/badge/Unity-2023.4%20LTS-000000?style=flat&logo=unity)
![C#](https://img.shields.io/badge/C%23-12-239120?style=flat&logo=csharp)
![Android](https://img.shields.io/badge/Android-10%2B-3DDC84?style=flat&logo=android)
![Code](https://img.shields.io/badge/Code-38%2C500%20lines-green)
![Files](https://img.shields.io/badge/Files-63%20C%23-blue)
![Systems](https://img.shields.io/badge/Systems-22-red)

---

## 🎮 Game Overview

**IRON PROTOCOL** is a next-generation Turn-Based Strategy (TBS) game built with Unity Engine, merging deep tactical combat with grand strategic decision-making in a modern setting (2025+). Players begin as a minor nation and evolve into a global superpower through military conquest, economic warfare, diplomatic maneuvering, nuclear deterrence, and space domination.

## 📊 Project Statistics

| Metric | Value |
|--------|-------|
| **C# Files** | 63 |
| **Lines of Code** | ~38,500 |
| **Assembly Definitions** | 20 |
| **Game Systems** | 22 |
| **Unit Types** | 12 |
| **Technologies** | 15 |
| **World Events** | 20 |
| **Stock Companies** | 15 |
| **News Outlets** | 8 |
| **Government Policies** | 12 |

## 🏗️ Architecture (20 Assemblies)

| Assembly | Namespace | Purpose |
|----------|-----------|---------|
| `IronProtocol.Core` | Core | EventBus, ServiceLocator, ObjectPool, SaveManager |
| `IronProtocol.HexMap` | HexMap | Hex grid, terrain, camera |
| `IronProtocol.Military` | Military | Units, combat, flanking, supply, combined arms |
| `IronProtocol.Economy` | Economy | Dynamic market, resources, alliance currency |
| `IronProtocol.Diplomacy` | Diplomacy | Casus Belli, relations, occupation |
| `IronProtocol.AI` | AI | Utility AI + Reinforcement Learning |
| `IronProtocol.Weather` | Weather | Dynamic weather system |
| `IronProtocol.TurnSystem` | TurnSystem | Turn phases, game loop |
| `IronProtocol.Multiplayer` | Multiplayer | Async multiplayer, notifications |
| `IronProtocol.UI` | UI | All screens and HUD |
| `IronProtocol.ResearchTech` | ResearchTech | Tech tree, research management |
| `IronProtocol.Nuclear` | Nuclear | Nuclear weapons, MAD, radiation |
| `IronProtocol.Espionage` | Espionage | Spies, intel reports, covert ops |
| `IronProtocol.WorldEvents` | WorldEvents | Dynamic events, disasters, crises |
| `IronProtocol.Elections` | Elections | Democratic elections, government policies |
| `IronProtocol.StockMarket` | StockMarket | Global stocks, trading, portfolios |
| `IronProtocol.ProxyWars` | ProxyWars | Covert warfare, rebellion |
| `IronProtocol.MediaPropaganda` | MediaPropaganda | News, propaganda, public opinion |
| `IronProtocol.AdvancedAlliances` | AdvancedAlliances | Alliance management, shared defense |
| `IronProtocol.SpaceWeapons` | SpaceWeapons | Satellites, orbital strikes |

## ⚔️ All Game Systems

### Core Systems (12)
| # | System | Description |
|---|--------|-------------|
| 1 | **Hex Map** | Interactive hex grid with terrain, cities, touch controls |
| 2 | **Military Combat** | 11-step combat with flanking, combined arms, weather |
| 3 | **Dynamic Economy** | 4-factor market pricing (S/D, shocks, trends, noise) |
| 4 | **Diplomacy** | Casus Belli, relations, alliances, occupation |
| 5 | **Adaptive AI** | Utility AI + Q-Learning reinforcement |
| 6 | **Weather** | 5 types affecting combat and movement |
| 7 | **Turn System** | 6-phase game loop with victory conditions |
| 8 | **Multiplayer** | Async turns with push notifications |
| 9 | **Save System** | JSON serialization with 10 save slots |
| 10 | **UI System** | 8 screens: Map, Battle, Market, Diplomacy, Research, Production, Military, Settings |
| 11 | **Unit System** | 12 unit types across 4 domains |
| 12 | **Supply Lines** | BFS supply tracing with interdiction |

### New Systems (10) 🔥
| # | System | Files | Lines | Key Features |
|---|--------|-------|-------|-------------|
| 13 | **Tech Tree** | 4 | 2,110 | 15 techs, 3 branches (Military/Economic/Cyber), 5 tiers |
| 14 | **Nuclear Weapons** | 3 | 2,298 | 4 warhead types, MAD system, radiation spread |
| 15 | **Espionage** | 2 | 2,426 | 8 spy missions, intel reports, counter-intelligence |
| 16 | **World Events** | 3 | 3,404 | 20 dynamic events (disasters, revolutions, crises) |
| 17 | **Elections** | 2 | 1,703 | 4 gov types, 12 policies, democratic elections |
| 18 | **Stock Market** | 1 | 1,011 | 15 companies, 5 sectors, trading |
| 19 | **Proxy Wars** | 2 | 1,855 | 8 covert actions, rebellion system |
| 20 | **Media** | 2 | 2,746 | 8 news outlets, propaganda campaigns |
| 21 | **Advanced Alliances** | 2 | 2,467 | 5 alliance types, shared defense network |
| 22 | **Space Weapons** | 1 | 1,214 | 7 asset types, orbital kinetic bombardment |

## 🔬 Technology Tree (15 Technologies)

### Military Branch ⚔️
| Tier | Technology | Effect |
|------|-----------|-------|
| 1 | Advanced Rifles | Infantry ATK +10% |
| 2 | Improved Armor | Armor HP +20% |
| 3 | Precision Artillery | Artillery range +1 |
| 3 | SAM Defense | Unlock anti-air building |
| 4 | Stealth Technology | Stealth units +50% evasion |
| 4 | Railgun Research | Unlock railgun unit |
| 5 | EMP Shield | Immune to cyber attacks |
| 5 | Drone Swarm | Unlock advanced drone |

### Economic Branch 💰
| Tier | Technology | Effect |
|------|-----------|-------|
| 1 | Advanced Mining | Resource production +25% |
| 1 | Free Trade | Market fees -50% |
| 2 | Industrial Automation | Production speed +40% |
| 2 | Currency Markets | Unlock alliance currency |
| 3 | Rare Earth Processing | Rare earth yield +50% |
| 3 | Quantum Computing | Cyber attack +30% |
| 4 | Space Economy | Unlock space income |
| 4 | Global Trade Network | All trade +30% |
| 5 | Post-Scarcity Economy | All resource costs -25% |

### Cyber Branch 💻
| Tier | Technology | Effect |
|------|-----------|-------|
| 1 | Network Security | Cyber defense +20% |
| 1 | Basic Hacking | Unlock cyber unit |
| 2 | Electronic Warfare | Disable enemy radar |
| 2 | AI-Assisted Command | All units +5% combat |
| 3 | Advanced Encryption | Immune to basic cyber |
| 3 | Cyber Offense II | Cyber attack +40% |
| 4 | Quantum Encryption | Immune to all cyber |
| 4 | Autonomous Weapons | Units auto-target |
| 5 | Singularity Protocol | Unlock super AI bonuses |

## 💣 Nuclear Weapons System

| Warhead | Yield | Delivery | Special |
|---------|-------|---------|--------|
| Tactical | 10-50 kt | All | Limited radiation |
| Strategic | 100-500 kt | ICBM/SLBM | Massive devastation |
| MIRV | 1000 kt | ICBM | Multiple warheads |
| Neutron | 5 kt | All | Kills people, preserves infrastructure |

- **MAD System**: Automatic deterrence when both nations have nuclear capability
- **Radiation**: Spreads to adjacent hexes, decays over 20 turns
- **Global Tension**: 0-100 meter affected by nuclear events

## 🕵️ Espionage (8 Mission Types)

| Mission | Success Chance | Effect |
|---------|----------------|--------|
| Reconnaissance | High | Reveal units in radius 3 for 5 turns |
| Sabotage | Medium | 30-60% production reduction for 3 turns |
| Assassination | Low | -40 stability, -30 morale |
| Tech Theft | Low (10-30%) | Steal unresearched technology |
| Counter-Intelligence | Medium | +15-35% counter-espionage |
| Install Network | Medium | Permanent passive intel gathering |
| False Flag | Medium | Frame another nation (+50 aggression) |
| Bribe | Low-Medium | Convert enemy unit (30% chance) |

## 🌍 Dynamic World Events (20 Types)

| Category | Events |
|----------|--------|
| Natural Disasters | Earthquake, Flood, Volcanic Eruption, Tsunami |
| Social Crises | Pandemic, Famine, Refugee Crisis, Popular Revolution |
| Economic | Recession, Stock Market Crash, Trade Dispute |
| Geopolitical | Arms Race Escalation, Peace Summit, Assassination |
| Environmental | Nuclear Accident, Oil Spill |
| Technology | Resource Discovery, Tech Breakthrough |
| Military | Piracy Surge, Cyber Attack |

## 🏛️ Government & Elections

### Government Types
- **Democracy**: Elections every 10 turns, high approval needed
- **Autocracy**: Stable but risk of coups
- **Theocracy**: Religious influence on policy
- **Military Junta**: High military output, low diplomacy
- **Constitutional Monarchy**: Balanced approach

### Policy Stances
- **Hawkish**: +20% military, easier war declaration
- **Diplomatic**: +20% diplomacy, cheaper alliances
- **Isolationist**: -50% trade, +30% defense
- **Moderate**: Balanced approach

## 📈 Stock Market (15 Companies)

| Sector | Companies |
|--------|----------|
| Military | Titan Defense (TDEF), SkyForce (SKFS), CyberShield (CSHC) |
| Technology | QuantumCore (QCOR), NanoTech (NTIN), Silicon Dynamics (SVDX) |
| Energy | PetroGlobal (PGLB), GreenPower (GPWR), UraniumOne (URAN) |
| Finance | GlobalBank (GBK), CryptoVault (CRVT), InsuranceWorld (INSW) |
| Infrastructure | BuildRight (BRLT), TeleCom (TCGL), SpaceXport (SPTX) |

## 🚀 Getting Started

```bash
git clone https://github.com/yayass3r/IRON-PROTOCOL.git
# Open in Unity Hub (Unity 2023.4 LTS)
# Import TextMeshPro from Package Manager
# Create GameConfig: Create → Iron Protocol → Game Config
# Build for Android: File → Build Settings → Android → Build
```

## 🛣️ Development Roadmap

| Phase | Status | Systems |
|-------|--------|---------|
| Phase 1 | ✅ Complete | Core Engine, HexMap, Military, Economy, Diplomacy, AI |
| Phase 2 | ✅ Complete | UI, Weather, Multiplayer, Save System, Turn System |
| Phase 3 | ✅ Complete | Tech Tree, Nuclear, Espionage, World Events |
| Phase 4 | ✅ Complete | Elections, Stock Market, Proxy Wars, Media |
| Phase 5 | ✅ Complete | Advanced Alliances, Space Weapons |
| Phase 6 | 🔲 Planned | 3D Models, VFX, Sound Design, QA Testing |
| Phase 7 | 🔲 Planned | Balance Testing, Beta, Polish, Launch |

## 📜 License

This project is proprietary and confidential. All rights reserved.

---

*Lead Game Architect & Lead Tactical Game Designer | 2025*
