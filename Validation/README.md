# Проверки V6

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

## Актуальные отчёты V6

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
Их SHA и измерения не подтверждают новую анимацию и звук. Итог текущей версии находится в `FoundationV6Review.json`. Старые отчёты описывают
свои версии; часть особо ранних файлов дополнительно помечена `historicalReport: true`.

## Что ещё проверяется в Unity

Реальный импорт модели/native Mesh/HDR, компиляция игры, Animator/PhysX, URP,
прослушивание звука, сборки Android/iOS и частота кадров на устройстве здесь не выполнялись.
Сценарий ручной проверки находится в `MOBILE_TESTING.md`. Визуальные превью в Docs получены
в Blender из геометрии проекта и не являются видеозаписью Unity. Изображения
`AnkleFix_before/after` показывают предыдущее исправление крепления. `Docs/SoundPreviewV6.mp3` — офлайн-превью новых звуков/игровой смеси длительностью
44 секунды. Другие превью относятся к прежним версиям.

Локальные `bin`, `obj`, `results.txt` и `Validation/Results` исключены из Git.
