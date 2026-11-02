# Исходники звука

Короткие записи для воспроизводимого звукового дизайна и музыкальные исходники.
Это записи указанных авторов, а не записи, сделанные автором кода.
Все исходники опубликованы под CC0 1.0; текст посвящения — `CC0-1.0.txt`.
Имена, ссылки, изменения и SHA-256 — в `manifest.json`; отдельные исходники нового
трека и их манифест — в `MusicV3`.

| Источник | Автор | Использование |
| --- | --- | --- |
| [Fantozzi’s Footsteps](https://opengameart.org/content/fantozzis-footsteps-grasssand-stone) | Fantozzi, нарезка qubodup | Подошва, каменная и сыпучая фактура |
| [Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney | Близкие выстрелы .45 ACP 1911 и атака Walther PPQ |
| [VCSL](https://github.com/sgossner/VCSL) | Versilian Studios / Sam Gossner и участники | Металл, деревянный блок и slapstick |
| [Karoryfer](https://www.karoryfer.com/) | Подробности по каждому набору в `MusicV3/manifest.json` | Электрогитара, бас-гитара и ударная установка нового трека |

**Iron Pulse** — оригинальные ноты, ритм и аранжировка CareProject;
код композиции находится в `Tools/GenerateMainMusic.py`. Готовая песня или чужая
мелодия не заимствуется. Прежние записи клавишных не используются в новом треке.

Из корня проекта: `python Tools/GeneratePrototypeAudio.py`.
Нужны Python 3, NumPy, SciPy и ffmpeg в PATH. Генерация офлайн проверяет контрольные
суммы, собирает WAV/OGG и сохраняет GUID существующих файлов. `--sfx-only` обновляет
только эффекты/фон. Для обычного запуска игры генерация не нужна.

## Попадания V5

Новые `Impact_*.wav` сделаны собственным детерминированным синтезом в
`Tools/GenerateImpactAudio.py`. Для них не используются перечисленные выше записи;
материалы VCSL сохранены для резонансов шагов и воспроизводимости предыдущих версий.
Металл, дерево, бетон и земля имеют разные тембры и огибающие.
Команда `python Tools/GeneratePrototypeAudio.py --impacts-only` пересоздаёт только
эти WAV, сохраняя музыку, шаги и записи пистолетов. Уже готовые WAV входят в проект.
