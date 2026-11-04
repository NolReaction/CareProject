# Робот CareProject — очищенный исходник

Открывай intercom-armed.blend в **Blender 5.2 или новее**.
Все 28 текстур упакованы в файл. Дополнительные папки с изображениями не нужны.

## Начать редактирование

Выдели Intercom_Armature_Fixed, открой Dope Sheet → Action Editor и выбери Action.
При открытии выбран RunRelaxed, кадры 1–17, 24 FPS. Пробел запускает просмотр.

В файле 25 Actions:

- 13 игровых: Idle, IdleRelaxed, Run, RunRelaxed, RunBackward,
 RunBackwardRelaxed, RunLeft, RunLeftRelaxed, RunRight, RunRightRelaxed,
 RaiseWeapons, LowerWeapons, Shoot.
- 10 примеров: Reload_BakedPreview, ShootLeft_BakedPreview,
 ShootRight_BakedPreview, HurtFront_BakedPreview, HurtLeft_BakedPreview,
 HurtRight_BakedPreview, WallCompress_BakedPreview, IdleInspect_BakedPreview,
 AimLeft45_BakedPreview, AimRight45_BakedPreview.
- 2 примера опорного поворота: TurnLeft90_RuntimeTrace, TurnRight90_RuntimeTrace.

BakedPreview — обычные редактируемые ключи для типовых жестов.
RuntimeTrace — примеры поворота из промежуточной настройки V12.
Эти 12 дополнительных примеров пока не подключены к игровому Animator.
Unity сочетает бег, поворот корпуса, отдачу и реакцию на окружение программно;
изменения примеров потребуют отдельно подключить их к игре.

## Кости

Body — основание тела и ног. Torso — независимый корпус, плечи и руки.
L/R_Upperarm — плечи пружинных рук; L/R_Hand и пальцы — хват.
Calf → Foot → Toe — короткая нога, ботинок и носок.
Для поворота верхнего корпуса редактируй Torso.
Не растягивай цепочку ног и не отделяй Foot локальным переносом от Calf:
это отрывает ботинок от подшипника.

## Экспорт

Сохрани рабочую копию через Ctrl+S. В Scripting открой встроенный текст
Export_To_Unity.py и нажми Run Script. Выбери существующий файл проекта:
Assets/IntercomMVP/Content/Characters/Robot/Model/IntercomRobot.fbx.
Скрипт экспортирует 13 игровых Actions, сохраняя дополнительные примеры в Blender.
Сохрани существующий .fbx.meta в Unity.

После изменения геометрии, весов или костей: Tools → CareProject → Setup Robot
вне Play Mode, затем проверь импорт и анимацию. Файл Blender храни вне Assets,
например Source/Robot; в Unity используй экспортированный FBX.

## Состав исходника

Удалены 16 архивных версий бега, старый исходный скелет с 14 дублирующими сетками,
неиспользуемая прежняя левая рука, ненужные данные и отсутствующие изображения,
которые относились к удалённым копиям. Старые противоречивые памятки и скрипт
повторного ремонта костей заменены этой инструкцией и актуальным экспортёром.

Сохранены 24 действующие сетки, рабочий скелет, веса, материалы, 28 упакованных
текстур, пистолеты и 25 Actions. Кривые анимаций и 75 контрольных поз проверены
до и после чистки; сохранённый файл повторно открыт для проверки.


Для открытия полного проекта Unity используй [README.md](../../README.md).
Быстрый запуск уже использует готовый FBX: экспорт Blender для него не требуется.
