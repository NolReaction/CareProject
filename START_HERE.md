# CareProject: с чего начать

## Первый запуск

Распакуй полный архив в новую папку. В Unity Hub нажми **Add → Add project from disk**
и выбери папку, где рядом находятся `Assets`, `Packages` и `ProjectSettings`.
Открывай в Unity **6000.3.26f1** и дождись первого импорта. Пакеты Unity загрузятся
из реестра; дополнительная установка обновлений проекта не требуется.

При первом открытии пустого проекта учебная сцена открывается автоматически.
Ручной переход: **Tools → CareProject → Open Scene** либо двойной щелчок по
`Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity`.
Затем нажми Play и выбери «Свободный режим», «50 роботов» или «Спасение».
Команды Setup нужны при изменении ресурсов; сцена в архиве уже собрана.

**Tools → CareProject → Start Here** — навигатор по основным карточкам,
модели, звуку, свету и документации. Кнопка «Проверить ссылки разделов»
проверяет наличие основных ресурсов.

## Что редактировать

| Задача | Начни здесь |
| --- | --- |
| Урон, темп, магазин и перезарядка | `Content/Weapons/Pistols/Settings/DualPistols.asset` |
| Звуки, позы и эффекты пистолетов | `Content/Weapons/Pistols` |
| Модель, материалы и Animator героя | `Content/Characters/Robot` |
| Скорость и анимационные добавки | Объект Robot: Motor, Ground Feedback, Character Animation |
| Характеристики врага | `Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset` |
| Параметры режимов | `Content/Missions` |
| Музыка, шаги и попадания | `Content/Shared/Audio/Settings/GameAudio.asset` |
| Планировка, материалы и модули двора | `Content/Environments/TutorialYard` |
| Свет и цвет | `Settings/Rendering/Lighting/TutorialDaylight.asset` |

Пути в таблице относительны к `Assets/IntercomMVP`. Числа и ссылки в карточках
меняй вне Play Mode, чтобы сохранить изменения.

Правила игры находятся в `Runtime`, редакторские команды — в `Editor`.
Ресурсы конкретного оружия и врага находятся в `Content`; общая механика
не дублируется вместе с каждой новой моделью.

## Blender

`Source/Robot/intercom-armed.blend` — очищенный исходник для Blender **5.2+**.
Внутри 25 Actions и 28 текстур. Рабочие клипы и дополнительные примеры движений
описаны в [Source/Robot/README.md](Source/Robot/README.md).
Готовый FBX уже находится в Unity; для первого запуска Blender не нужен.

## Если что-то не работает

Сначала дождись окончания импорта и посмотри первую красную ошибку Console.
При пустой сцене открой рабочую сцену указанной выше командой.
Для задержки воспроизведи её, останови Play и выбери
**Tools → CareProject → Performance → Open Last Capture**.

Сохраняй `.meta` вместе с ресурсом при переносе: он хранит GUID ссылок.
Настоящую производительность, звук, Animator и физику проверяй в Unity и на устройстве.

[Архитектура](Docs/Architecture.md) · [Практика разработки](Docs/DeveloperGuide.md) ·
[Навигатор](Docs/ProjectNavigator.md) · [Художественное направление](Docs/ArtDirection.md)
