# Проверки CareProject

Для открытия игры эти проверки запускать не нужно. Они предназначены разработчику.

`python Validation/run_checks.py` запускает все текущие тестовые проекты.
Нужны Python3 и .NET SDK8+. Код игры исполняется с ограниченными заменами Unity API.
Это не заменяет импорт, сборку и Play Mode в Unity.

Проверки структуры и данных:

- `python Validation/check_architecture.py` — границы систем, GUID и пути исходников.
- `python -m unittest discover -s Validation/ArchitectureTests` — проверка самого валидатора.
- `python Validation/check_asset_paths.py` — поиск ресурсов, кеш и сохранение идентичности.
- `python Validation/check_scene_integrity.py` — ID сцены, владельцы компонентов и маршрутизация поверхностей.
- `python Validation/check_npc_scene.py` — состав врагов и настройки сцены.
- `python Validation/check_mission_scene.py` — задания, спасение и ссылки режима игры.
- `python Validation/check_courtyard.py` — связность двора и геометрия препятствий.

Некоторым проверкам геометрии нужен NumPy. Исходная сцена при проверке целостности
не перезаписывается: повторная генерация проверяется во временном файле.
Файлы внутри `*Tests` с именем Fixture/SoleProfile — действующие входные данные
тестов геометрии. Папки Results/bin/obj и Python-кеши не входят в поставку.

Текущий общий результат: `ProjectVerification.json`.
