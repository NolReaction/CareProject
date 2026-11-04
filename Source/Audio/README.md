# Исходные записи звука

Здесь сохранены записи, из которых собираются игровые звуки и музыка,
а также лицензии и контрольные суммы. Они нужны для дальнейшего редактирования
и воспроизводимости генераторов. Для обычного запуска игры генерация не нужна:
все готовые банки уже находятся в Assets.

## Текущие ресурсы

- Пистолеты и перезарядка: Assets/IntercomMVP/Content/Weapons/Pistols/Audio.
- Попадания по роботам: Assets/IntercomMVP/Content/Enemies/Shared/Audio.
- Реакции героя: Assets/IntercomMVP/Content/Characters/Robot/Audio.
- Шаги, материалы попаданий и музыка: Assets/IntercomMVP/Content/Shared/Audio.
- Промышленный фон: Assets/IntercomMVP/Content/Environments/TutorialYard/Audio.
- Микс: Assets/IntercomMVP/Content/Shared/Audio/Settings/GameAudio.asset.

Tools/GenerateCombatAudio.py собирает текущие выстрелы и попадания по роботам.
Tools/GenerateMainMusic.py содержит композицию Iron Pulse.
Остальные Generate*.py рядом содержат подготовку записей, синтез, музыку,
шаги и вспомогательные слои. Для ручной генерации нужны Python3, NumPy, SciPy
и ffmpeg. Перед запуском сохраняй свои правки ресурсов.

## Источники и условия

CC0-1.0.txt и manifest.json содержат условия, авторов, источники и SHA256
исходных коротких записей. Подпапка MusicV3 содержит действующие инструментальные
записи, отдельный manifest.json и исходные тексты лицензий.

Записи: Fantozzi’s Footsteps; Free Firearm Sound Library;
VCSL Versilian Studios / Sam Gossner; инструменты Karoryfer / D. Smolken.
Точные авторы и ссылки перечислены в манифестах. Музыкальные ноты и аранжировка
Iron Pulse написаны для CareProject; сэмплы инструментов взяты из указанных наборов.
Исторические записи для сравнения старой и новой версии в поставку не входят.
