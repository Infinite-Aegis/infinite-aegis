<div align="center">

<img src="https://github.com/Infinite-Aegis.png" width="180" alt="Infinite Aegis logo">

# SS14: Infinite Aegis

### An urban role-playing world where the old rules no longer apply.

[Русский](README.md) | **English**

[![Discord](https://img.shields.io/badge/Discord-join-5865F2?logo=discord&logoColor=white)](https://discord.gg/t8XAuymcDs)
[![Boosty](https://img.shields.io/badge/Boosty-support-F15F2C)](https://boosty.to/infiniteaegis)
[![Development](https://img.shields.io/badge/status-active%20development-yellow)](#development-status)

</div>

---

## About the Project

**SS14: Infinite Aegis** is an independent project based on Space Station 14, focused on urban punk life, conflicts, personal stories, and unrestricted role-playing interaction.

Our goal is to reconsider the familiar Dark RP format and create an environment where players are not limited by the established framework of a traditional space station.

Infinite Aegis is not merely a change of scenery. It is being developed as a separate world with its own atmosphere, city structure, occupations, organizations, conflicts, and rules.

## Core Ideas

- an urban environment instead of a traditional space station;
- a punk and rock-and-roll atmosphere;
- a strong focus on character interaction and the consequences of decisions;
- original maps, roles, mechanics, items, and visual identity;
- open development that allows the community to follow the project's progress;
- gradual team formation and preliminary distribution of role-playing positions.

## Development Status

The project is currently in **active development**.

Current work includes:

- the technical foundation of the project;
- the structure of the game world;
- the city map;
- roles and organizations;
- original game mechanics;
- graphical and audio resources;
- documentation for project contributors.

The public game server has not launched yet.

## Join the Project

Follow development, discuss the project, and learn about team recruitment in our Discord community:

**Discord:** https://discord.gg/t8XAuymcDs

Support development:

**Boosty:** https://boosty.to/infiniteaegis

## Repository Structure

Original Infinite Aegis files are placed in dedicated directories:

```text
Content.Client/_InfiniteAegis/
Content.Server/_InfiniteAegis/
Content.Shared/_InfiniteAegis/

Resources/Prototypes/_InfiniteAegis/
Resources/Textures/_InfiniteAegis/
Resources/Audio/_InfiniteAegis/
Resources/Maps/_InfiniteAegis/
Resources/Locale/ru-RU/_InfiniteAegis/
```

This structure separates project-specific content from upstream Space Station 14 code and simplifies future upstream updates.

Placing a file inside an `_InfiniteAegis` directory does not automatically change the license of third-party material. Derivative and third-party materials remain governed by their original licenses.

## Cloning the Repository

```bash
git clone --recurse-submodules https://github.com/Infinite-Aegis/infinite-aegis.git
cd infinite-aegis
```

When the repository was cloned without submodules:

```bash
git submodule update --init --recursive
```

## Project Setup

```bash
python RUN_THIS.py
```

## Building

```bash
dotnet restore
dotnet build
```

## Starting the Server

```bash
dotnet run --project Content.Server
```

## Starting the Client

Run in a second terminal:

```bash
dotnet run --project Content.Client
```

Connect to the local server using:

```text
localhost
```

## Upstream

Infinite Aegis is based on:

[space-wizards/space-station-14](https://github.com/space-wizards/space-station-14)

The official repository is used as `upstream` for engine and base-content updates.

Example upstream configuration:

```bash
git remote add upstream https://github.com/space-wizards/space-station-14.git
git fetch upstream
```

Expected remote configuration:

```text
origin    https://github.com/Infinite-Aegis/infinite-aegis.git
upstream  https://github.com/space-wizards/space-station-14.git
```

Infinite Aegis is an independent project and is not officially affiliated with Wizard's Den, the Space Wizards Federation, or the developers of Space Station 14.

## Development Workflow

All permanent changes are stored in the `main` branch.

Each task must be developed in a temporary working branch created from the latest version of `main`:

```bash
git switch main
git pull origin main
git switch -c feature/example-feature
```

After completing the work:

```bash
git add .
git commit -m "Add example feature"
git push -u origin feature/example-feature
```

Then open a Pull Request targeting `main`.

The working branch is deleted after the Pull Request is merged.

Direct changes to `main` are not allowed.

## Licensing

This repository contains materials under different licensing terms.

Upstream Space Station 14 code remains available under the MIT License. RobustToolbox code, third-party libraries, upstream assets, and other third-party materials remain governed by their original licenses, copyright notices, and metadata files.

Original Infinite Aegis code, assets, maps, lore, written materials, documentation, music, sounds, artwork, branding, and other original materials are **all rights reserved** unless a specific file explicitly states otherwise.

Without prior written permission from the applicable copyright holder, you may not:

- copy or distribute original Infinite Aegis materials;
- modify them or create derivative materials from them;
- transfer them to another server, game, modification, project, or community;
- use them for commercial, promotional, or advertising purposes;
- remove or alter authorship and licensing information;
- represent Infinite Aegis materials as the work of another person or project.

Public access to this repository and the technical ability to create a fork do not constitute permission to reuse original Infinite Aegis materials.

Materials derived from MIT, Creative Commons, ShareAlike, or other third-party works remain governed by their respective licenses and do not become proprietary Infinite Aegis materials.

Requests for written permission may be sent to:

**infiniteaegis.org@proton.me**

See:

- [LICENSE](LICENSE)
- [LICENSE-INFINITE-AEGIS.md](LICENSE-INFINITE-AEGIS.md)
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)

---

<div align="center">

**S:IA — breaking the established boundaries.**

</div>