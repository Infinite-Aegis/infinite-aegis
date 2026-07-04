<div align="center">

<img src="https://github.com/Infinite-Aegis.png" width="180" alt="Infinite Aegis logo">

# SS14: Infinite Aegis

### Городское ролевое пространство, где старые правила больше не работают.

[![Discord](https://img.shields.io/badge/Discord-присоединиться-5865F2?logo=discord&logoColor=white)](https://discord.gg/t8XAuymcDs)
[![Boosty](https://img.shields.io/badge/Boosty-поддержать-F15F2C)](https://boosty.to/infiniteaegis)
[![Development](https://img.shields.io/badge/status-active%20development-yellow)](#статус-разработки)

</div>

---

## О проекте

**SS14: Infinite Aegis** — независимый проект на базе Space Station 14, посвящённый городской панковской жизни, конфликтам, личным историям и свободному ролевому взаимодействию.

Мы хотим переосмыслить привычный формат Dark RP и создать среду, в которой игроки не ограничены устоявшимися рамками классической станции.

Это не просто смена декораций. Infinite Aegis создаётся как отдельный мир со своей атмосферой, устройством города, профессиями, организациями, конфликтами и правилами.

## Основные идеи

- городская среда вместо традиционной космической станции;
- панковская и рок-н-ролльная атмосфера;
- упор на взаимодействие персонажей и последствия их решений;
- собственные карты, роли, механики, предметы и визуальный стиль;
- открытая разработка с возможностью следить за развитием проекта;
- постепенное формирование команды и предварительное распределение ролевых позиций.

## Статус разработки

Проект находится в стадии **активной разработки**.

На текущем этапе создаются:

- техническая основа проекта;
- структура игрового мира;
- городская карта;
- игровые роли и организации;
- собственные механики;
- графические и звуковые ресурсы;
- документация для участников команды.

Публичный игровой сервер пока не запущен.

## Присоединиться к проекту

Следить за разработкой, обсуждать проект и узнавать о наборе в команду можно в нашем Discord:

**Discord:** https://discord.gg/t8XAuymcDs

Поддержать разработку:

**Boosty:** https://boosty.to/infiniteaegis

## Структура репозитория

Собственные файлы Infinite Aegis размещаются в выделенных каталогах:

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

Это позволяет отделять собственный контент проекта от исходного кода Space Station 14 и упрощает получение обновлений из upstream.

## Ветки

```text
main
└── стабильная версия проекта

develop
└── актуальная версия разработки

feature/*
└── новые функции и игровые механики

fix/*
└── исправления ошибок

docs/*
└── изменения документации

maintenance/*
└── технические изменения и обновления upstream
```

Все изменения должны попадать в `develop` через Pull Request.

Ветка `main` используется для стабильных версий проекта.

## Клонирование репозитория

```bash
git clone --recurse-submodules https://github.com/Infinite-Aegis/infinite-aegis.git
cd infinite-aegis
```

Если репозиторий уже был клонирован без submodules:

```bash
git submodule update --init --recursive
```

## Подготовка проекта

```bash
python RUN_THIS.py
```

## Сборка

```bash
dotnet restore
dotnet build
```

## Запуск сервера

```bash
dotnet run --project Content.Server
```

## Запуск клиента

Во втором терминале:

```bash
dotnet run --project Content.Client
```

Для подключения к локальному серверу можно использовать:

```text
localhost
```

## Upstream

Проект основан на кодовой базе:

[space-wizards/space-station-14](https://github.com/space-wizards/space-station-14)

Официальный репозиторий используется как `upstream` для получения обновлений движка и базового контента.

Пример настройки upstream:

```bash
git remote add upstream https://github.com/space-wizards/space-station-14.git
git fetch upstream
```

Проверка удалённых репозиториев:

```bash
git remote -v
```

Ожидаемая структура:

```text
origin    https://github.com/Infinite-Aegis/infinite-aegis.git
upstream  https://github.com/space-wizards/space-station-14.git
```

Infinite Aegis является независимым проектом и официально не связан с Wizard's Den или Space Wizards Federation.

## Разработка

Перед началом новой задачи необходимо обновить `develop`:

```bash
git switch develop
git pull origin develop
```

После этого создать отдельную ветку:

```bash
git switch -c feature/example-feature
```

После завершения работы:

```bash
git add .
git commit -m "Add example feature"
git push -u origin feature/example-feature
```

Затем необходимо создать Pull Request в ветку `develop`.

## Лицензия

Условия распространения исходного кода и ресурсов указаны в файле [LICENSE](LICENSE).

Отдельные ресурсы могут распространяться на условиях, указанных в соответствующих каталогах или файлах лицензий.

---

<div align="center">

**S:IA — ломаем устоявшиеся рамки.**

</div>