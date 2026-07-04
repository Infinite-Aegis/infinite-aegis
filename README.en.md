<div align="center">



<img src="https://github.com/Infinite-Aegis.png" width="180" alt="Infinite Aegis logo">



\# SS14: Infinite Aegis



\### An urban role-playing world where the old rules no longer apply.



\[Русский](README.md) | \*\*English\*\*



\[!\[Discord](https://img.shields.io/badge/Discord-join-5865F2?logo=discord\&logoColor=white)](https://discord.gg/t8XAuymcDs)

\[!\[Boosty](https://img.shields.io/badge/Boosty-support-F15F2C)](https://boosty.to/infiniteaegis)

\[!\[Development](https://img.shields.io/badge/status-active%20development-yellow)](#development-status)



</div>



\---



\## About the Project



\*\*SS14: Infinite Aegis\*\* is an independent project based on Space Station 14,

focused on urban punk life, conflicts, personal stories, and unrestricted

role-playing interaction.



Our goal is to reconsider the familiar Dark RP format and create an environment

where players are not limited by the established framework of a traditional

space station.



Infinite Aegis is not merely a change of scenery. It is being developed as a

separate world with its own atmosphere, city structure, occupations,

organizations, conflicts, and rules.



\## Core Ideas



\- an urban environment instead of a traditional space station;

\- punk and rock-and-roll atmosphere;

\- a strong focus on character interaction and the consequences of decisions;

\- original maps, roles, mechanics, items, and visual identity;

\- open development that allows the community to follow the project's progress;

\- gradual team formation and preliminary distribution of role-playing

&#x20; positions.



\## Development Status



The project is currently in \*\*active development\*\*.



Current work includes:



\- the technical foundation of the project;

\- the structure of the game world;

\- the city map;

\- roles and organizations;

\- original game mechanics;

\- graphical and audio resources;

\- documentation for project contributors.



The public game server has not launched yet.



\## Join the Project



Follow development, discuss the project, and learn about team recruitment in

our Discord community:



\*\*Discord:\*\* https://discord.gg/t8XAuymcDs



Support development:



\*\*Boosty:\*\* https://boosty.to/infiniteaegis



\## Repository Structure



Original Infinite Aegis files are placed in dedicated directories:



```text

Content.Client/\_InfiniteAegis/

Content.Server/\_InfiniteAegis/

Content.Shared/\_InfiniteAegis/



Resources/Prototypes/\_InfiniteAegis/

Resources/Textures/\_InfiniteAegis/

Resources/Audio/\_InfiniteAegis/

Resources/Maps/\_InfiniteAegis/

Resources/Locale/ru-RU/\_InfiniteAegis/

```



This structure separates project-specific content from upstream Space Station

14 code and simplifies future upstream updates.



\## Branches



```text

main

└── stable project version



develop

└── current development version



feature/\*

└── features and game mechanics



fix/\*

└── bug fixes



docs/\*

└── documentation changes



maintenance/\*

└── maintenance and upstream updates

```



All development changes must reach `develop` through a Pull Request.



The `main` branch is used for stable project versions.



\## Cloning the Repository



```bash

git clone --recurse-submodules https://github.com/Infinite-Aegis/infinite-aegis.git

cd infinite-aegis

```



When the repository was cloned without submodules:



```bash

git submodule update --init --recursive

```



\## Project Setup



```bash

python RUN\_THIS.py

```



\## Building



```bash

dotnet restore

dotnet build

```



\## Starting the Server



```bash

dotnet run --project Content.Server

```



\## Starting the Client



Run in a second terminal:



```bash

dotnet run --project Content.Client

```



Connect to the local server using:



```text

localhost

```



\## Upstream



Infinite Aegis is based on:



\[space-wizards/space-station-14](https://github.com/space-wizards/space-station-14)



The official repository is used as `upstream` for engine and base-content

updates.



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



Infinite Aegis is an independent project and is not officially affiliated with

Wizard's Den or the Space Wizards Federation.



\## Development Workflow



Update `develop` before beginning a task:



```bash

git switch develop

git pull origin develop

```



Create a task branch:



```bash

git switch -c feature/example-feature

```



After completing the work:



```bash

git add .

git commit -m "Add example feature"

git push -u origin feature/example-feature

```



Then open a Pull Request targeting `develop`.



\## Licensing



This repository contains materials under different licensing terms.



Upstream Space Station 14 code remains available under the MIT License.

Upstream and third-party assets remain governed by their original licenses and

metadata.



Original Infinite Aegis code, assets, maps, lore, documentation, branding, and

other original materials are \*\*all rights reserved\*\* unless a file explicitly

states otherwise. They may not be reused in another project or server without

written permission from the applicable copyright holder.



See:



\- \[LICENSE](LICENSE)

\- \[LICENSE-INFINITE-AEGIS.md](LICENSE-INFINITE-AEGIS.md)

\- \[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)



\---



<div align="center">



\*\*S:IA — breaking the established boundaries.\*\*



</div>

