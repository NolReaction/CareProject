# CareProject — навигация проекта

Полный проект открывается из Unity Hub через Add project from disk.
Выбери папку с Assets, Packages и ProjectSettings, используй Unity 6000.3.26f1
и дождись первого импорта. Затем открой Tools → CareProject → Start Here.

Рабочая сцена: Scenes/TutorialYard/IntercomMVP.unity.
Ручное открытие: Tools → CareProject → Open Scene.

| Что ищешь | Папка |
| --- | --- |
| Код героя | Runtime/Player |
| Общая механика оружия и пуль | Runtime/Combat |
| Враги и поиск пути | Runtime/Enemies |
| Режимы и волны | Runtime/Missions |
| Общие здоровье, урон и поверхности | Runtime/Shared |
| Предметы двора | Runtime/World |
| Камера, UI и аудио | Runtime/Presentation |
| Инструменты редактора | Editor |
| Модель и клипы героя | Content/Characters/Robot |
| Пистолеты: карточка, позы, звуки, эффекты | Content/Weapons/Pistols |
| Карточка ударника | Content/Enemies/Udarnik/Settings |
| Параметры заданий | Content/Missions |
| Учебная локация | Content/Environments/TutorialYard |
| Сцена | Scenes/TutorialYard |

Пути таблицы относительны к Assets/IntercomMVP.
Blender находится вне Assets: Source/Robot/intercom-armed.blend, Blender 5.2+.
Все 28 текстур упакованы внутрь.

Текущие инструкции в корне: START_HERE.md, Docs/Architecture.md,
Docs/DeveloperGuide.md и MOBILE_TESTING.md. Source/Robot/README.md описывает
анимации и экспорт. Validation/ProjectVerification.json фиксирует проверки.

Карточки .asset задают числа и ссылки. Runtime выполняет поведение.
Editor настраивает проект. Переноси ресурсы вместе с .meta.
