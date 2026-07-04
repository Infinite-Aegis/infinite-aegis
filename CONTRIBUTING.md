\# Contributing to Infinite Aegis



Thank you for your interest in contributing to SS14: Infinite Aegis.



Infinite Aegis is an independent project based on Space Station 14. All changes to this repository must follow the workflow and licensing rules described below.



\## Development Workflow



The `main` branch is protected. Direct pushes to `main` are not allowed.



Before starting a task:



```bash

git switch main

git pull origin main

git switch -c feature/example-feature

```



Use an appropriate branch prefix:



\- `feature/` — new features and mechanics;

\- `fix/` — bug fixes;

\- `docs/` — documentation;

\- `maintenance/` — maintenance and upstream updates;

\- `ci/` — automation and GitHub Actions;

\- `content/` — content additions;

\- `mapping/` — map changes;

\- `art/` — sprites and visual assets;

\- `localization/` — localization changes.



After completing the work:



```bash

git add .

git commit -m "Add example feature"

git push -u origin feature/example-feature

```



Then open a Pull Request targeting `main`.



\## Pull Requests



A Pull Request should:



\- have a clear title;

\- explain what was changed and why;

\- include testing instructions;

\- include screenshots or video for visible in-game changes;

\- avoid unrelated changes;

\- pass all required GitHub checks;

\- resolve all review conversations before merging.



Pull Requests may be rejected if they contain unrelated formatting changes, generated files, unlicensed assets, or changes outside the stated scope.



\## Code and Content Organization



Original Infinite Aegis files should be placed in the dedicated project directories whenever possible:



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



Changes to upstream files should be kept as small and isolated as possible to reduce conflicts during upstream updates.



\## Testing



Before submitting a Pull Request:



```bash

python RUN\_THIS.py

dotnet restore

dotnet build

```



When relevant, also run the server and client locally and verify the affected feature in game.



\## Assets and Third-Party Materials



Do not submit assets, code, audio, fonts, or other materials unless you have the right to contribute them.



For third-party materials, preserve:



\- original author;

\- source;

\- license;

\- attribution requirements;

\- modification information.



Materials derived from Creative Commons Attribution-ShareAlike or other ShareAlike licenses must remain under the required compatible license.



Do not place third-party materials inside `\_InfiniteAegis` directories and describe them as proprietary project materials.



\## Licensing of Contributions



Original Infinite Aegis materials are governed by the licensing notices in this repository.



See:



\- \[LICENSE](LICENSE)

\- \[LICENSE-INFINITE-AEGIS.md](LICENSE-INFINITE-AEGIS.md)

\- \[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)



Unless a separate written agreement applies, contributors retain copyright in their own original contributions.



By submitting a contribution, you confirm that:



\- you have the right to submit it;

\- it does not unlawfully include third-party material;

\- all required attribution and licensing information is included;

\- the contribution may be stored, reviewed, modified, and distributed as part of the Infinite Aegis repository under the applicable licensing terms.



\## Conduct



All contributors must follow the repository's \[Code of Conduct](CODE\_OF\_CONDUCT.md).



\## Contact



Questions about contribution rules or licensing may be sent to:



\*\*infiniteaegis.org@proton.me\*\*



Project Discord:



https://discord.gg/t8XAuymcDs

