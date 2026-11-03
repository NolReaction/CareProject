# Проверки V9

В корне проекта:

```sh
python Validation/run_checks.py
```

Нужны Python 3 и .NET SDK 8+. Путь к SDK можно передать через
`--dotnet /path/to/dotnet`. Для уже восстановленных проектов доступен `--no-restore`.
Отдельный запуск: `dotnet run --project Validation/TurningRigTests`.

Проверки собирают производственные алгоритмы и, где требуется, узкие замены Unity API.
В среде подготовки использовались прямой компилятор Roslyn и .NET runtime без NuGet;
это не компиляция всей игры против реальных сборок Unity.

## Актуальные отчёты V9

- `FoundationV9Review.json`: полный прогон и границы проверки.
- `PackageV9Review.json`: состав дополнения поверх V8, ссылки и сохранение GUID.
- `MissionsV9Review.json`: учёт миссий, волны, переиспользование и спасение.
- `NpcV9Review.json`: сопровождающий замах, ближний бой, движение и смена цели.
- `MissionInterfaceV9Review.json`: выбор режима, результаты, подсказки и отмена удержанного ввода.
- `SceneMissionsV9Review.json`: явные ссылки, маркеры и маршруты текущего двора.
- `SceneIntegrityV9Review.json`: локальные ID и владельцы компонентов сцены.
- `ReviewV9.json`: интеграционный обзор и блокировка героя в меню.

Новые проекты: `MissionStateTests`, `MissionRuntimeTests`, `MissionUiTests`,
`MissionSceneRouteTests`. Исполняют производственный код с узкими заменами движка.
Проверки сценовых маршрутов используют настоящую сетку навигации и маркеры сцены.
`Docs/MissionHudV9_*.png` — офлайн-превью команд HUD, не скриншоты Unity.

## Исторические отчёты V8

- `FoundationV8Review.json`: итог автономных проверок и границы результата.
- `PackageV8Review.json`: дополнение поверх V7, состав файлов и сохранение GUID.
- `SceneNpcV8Review.json`: подключения героя, NPC, навигации и материалов в сцене.
- `SceneIntegrityV8Review.json`: локальные ID, владельцы компонентов и материалы коллайдеров.
- `NpcNavigationV8Review.json`: поиск пути, углы, динамические препятствия и бюджет запросов.
- `NpcV8Review.json`: состояния, удары, жизненный цикл и создание капсул.
- `PlayerHealthV8Review.json`: здоровье, смерть, повтор, ввод и безопасная область интерфейса.

Новые проверки находятся в `NpcBrainTests`, `NpcNavigationTests`,
`NpcRuntimeTests`, `NpcArenaTests` и `PlayerVitalityTests`. Это исполнение производственного
кода с узкими заменами Unity API, без реального PhysX или Play Mode.
`Docs/PlayerHealthV8*.png` — офлайн-превью команд отрисовки интерфейса.

## Исторические отчёты V7

- `FoundationV7Review.json`: сводка всех автономных проверок и границы результата.
- `PackageV7Review.json`: состав дополнения, GUID и установка поверх V6/V6.1.
- `CourtyardV7Review.json`: геометрия двора, доступность маршрутов и сохранность
  компонентов персонажа, камеры, света и аудио.
- `SceneIntegrityV7Review.json`: локальные ID, владельцы компонентов и фактические
  материалы всех коллайдеров. Количество выводится из текущей планировки.
- `InterfaceV7Review.json`: безопасные области экрана и настоящий путь ввода.
- `InteractivePropsV7Review.json`: состояния предметов, коллайдеры, сброс и визуальные реакции.
- `MuzzleHeightV7Review.json`: высота стволов и расстояние между ними по исходной
  геометрии; указаны ограничения переноса этих измерений на Animator в Unity.

Новые `HudLayoutTests` и `ReactivePropTests` дополняют существующие проверки.
`Docs/InterfaceV7*.png` — офлайн-превью команд отрисовки интерфейса с заменой
движка. `Docs/CourtyardV7.png` — Blender-превью геометрии карты. Это не скриншоты Unity.

## Исторические отчёты V6

- `FoundationV6Review.json`: итог 25 автономных проектов проверок, синтаксиса и интеграции.
- `AssetIntegrityV6Review.json`, `PackageV6Review.json`: сохранность ресурсов, GUID и установка поверх проекта.
- `SceneV6Review.json`, `SceneIntegrityV6Review.json`: исправление 94 ID за пределами signed Int64,
  связи объектов/компонентов и 101 коллайдер с материалом.
- `CombatV6Review.json`: параллельные пули, магазин, перезарядка, TTK и реальный путь ввода.
- `MotionV6Review.json`, `MotionMeshV6Review.json`: стабилизация корпуса, быстрые повороты,
  контакт ног и проверка пересечений настоящих мешей в Blender 5.2.3.
- `CharacterAnimationV6Review.json`: жесты и выравнивание стволов; владение позами и восстановление.
- `AudioV6Review.json`, `WeaponAudioV6Review.json`, `AudioGameplayV6Review.json`: обработка
  записанных CC0 выстрелов, перезарядка, события аудио и смесь без клиппинга.
- `ReviewV6.json`: независимый обзор и дополнительные интеграционные сценарии.

Новые проверки исполняют реальные компоненты с узкими заменами движка:
`MagazineTests`, `PlayerInputRuntimeTests`, `ShooterIntegrationTests`,
`CharacterAnimationTests`, `AnimationGroundingIntegrationTests`.
Физика, GUI и Animator в них моделируются; это не Play Mode.

Дополнительно:

```sh
python Validation/check_scene_integrity.py
python Validation/check_audio_assets.py
```

## Исторические отчёты V5

- `FoundationV5Review.json`: итог интеграции и границы проверки.
- `AssetIntegrityV5Review.json`: GUID, .meta, свет сцены и сохранность модели/музыки/карты.
- `ArcadeMotionV5Review.json`: скорость, разгон, широкий шаг и контакт на 30/60/120 FPS.
- `WeaponV5Review.json`: темп .18 с, здоровье/TTK и поведение после пропуска кадров.
- `ImpactAudioV5Review.json`, `MaterialImpactRoutingV5Review.json`, `AudioGameplayV5Review.json`:
  новые звуки, маршрутизация поверхностей и проверка офлайн-смеси.
- `CoverBulletHeightV5Review.json`: высота низких предметов; пуля выше предмета не попадает в него.
- `PaletteV5Review.json`: материалы без изменения геометрии и планировки.
- Проверки `OcclusionMathTests` и `OcclusionRuntimeTests`: реальные меши склада/ящика,
  отрезки перед героем, независимая прозрачность и восстановление.

`run_checks.py` автоматически находит все `*Tests/*.csproj`, включая новые.
Дополнительная проверка аудио: `python Validation/check_audio_assets.py`.

## Исторические отчёты V4

- `FoundationV4Review.json`: итог всей версии; 19 автономных проектов проверок.
- `FoundationV4AssetIntegrity.json`: GUID, метаданные, подключения сцены и сохранность модели.
- `WeaponFoundationV4Review.json`: баланс, таймер позы, здоровье, направление пули.
- `ImpactAudioV4Review.json`: громкость ударов, затухание и ограничения повторов.
- `AssetOrganizationV4Review.json`: перенос ресурсов и безопасная повторная установка.
- `CartoonEnvironmentV4.json`: геометрия и воспроизводимость художественного прохода.
- `CameraTrackingTests`: свободная зона, плавность, проекции, остановка и телепортация.

Команда `run_checks.py` запускает все проекты, включая новые. В среде подготовки
SDK CLI не смог прочитать сведения процесса через `/proc`; тот же C# был собран
напрямую Roslyn `csc.dll` с .NET8 reference assemblies и выполнен .NET runtime.
Это проверяет тестируемые компоненты, но не заменяет сборку Unity.

## Сохранённые отчёты V3 и предыдущих версий

- `GameplayV3Review.json`: итог интеграции, версия GitHub, состав проверок и границы результата.
- `GroundedRunV3*.json`: опорные траектории восьми клипов, сохранение рига и текстур,
  повторный импорт FBX и проверка настоящих мешей после программных поправок.
- `RunningContactTests`: контакт, скольжение и движение ботинок на сохранённых позах
  модели; собственные замены движка, без настоящего Unity Animator/PhysX.
- `TutorialEnvironment.json`: меши, нормали/индексы, отдельные коллайдеры и доступность маршрутов.
- `TutorialDaylight.json`: параметры света/URP, подтверждённые GUID и API пакета 17.3.
- `AudioGameplayV3Review.json` и `MusicV3Review.json`: новые звуки, Iron Pulse,
  происхождение записей, смесь без перегрузки и воспроизводимость генерации.
- `CombatV3Review.json`: темп стрельбы, материалы попаданий и геометрия трассера.
- `SurfaceResponsesV3.json`: материалы всех 101 коллайдера и трёх мишеней.
- `EditorSetupV3Review.json`: восстановление профиля подошв, сохранение сцены и безопасная очистка старых файлов.
- Проекты `TurnStepTests` и `TurningRigTests`: последовательность шагов, реальные размеры
  рига с тестовыми заменами движка, длинные серии поворотов, посадка и переход к бегу.

Остальные `*Tests` проверяют движение, калибровку, звук, сбросы ввода/оружия,
перекрытия камеры и выбор препятствия для пули. Все проекты обнаруживаются автоматически.

Отчёты `Ankle*.json`, `RepairReview.json`, `MotionPolish*.json`, `TutorialReview.json`,
`MovementRuntimeChecks.json`, `TurnActualMesh*.json` и `AudioV4Review.json` оставлены
как **история предыдущих версий**.
Их SHA и измерения не подтверждают новую анимацию и звук. Итог текущей версии находится в `FoundationV7Review.json`. Старые отчёты описывают
свои версии; часть особо ранних файлов дополнительно помечена `historicalReport: true`.

## Что ещё проверяется в Unity

Реальный импорт модели/native Mesh/HDR, компиляция игры, Animator/PhysX, URP,
прослушивание звука, сборки Android/iOS и частота кадров на устройстве здесь не выполнялись.
Сценарий ручной проверки находится в `MOBILE_TESTING.md`. Визуальные превью в Docs получены
в Blender из геометрии проекта и не являются видеозаписью Unity. Изображения
`AnkleFix_before/after` показывают предыдущее исправление крепления. `Docs/SoundPreviewV6.mp3` — офлайн-превью новых звуков/игровой смеси длительностью
44 секунды. Другие превью относятся к прежним версиям.

Локальные `bin`, `obj`, `results.txt` и `Validation/Results` исключены из Git.
