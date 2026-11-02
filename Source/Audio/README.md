# Исходники звука

Здесь лежат короткие записи, необходимые для воспроизведения звуков и музыки.
Это записанные инструменты и шумы, а не записи, сделанные автором кода.
Все исходные записи опубликованы под CC0 1.0; текст посвящения в `CC0-1.0.txt`.
Имена, ссылки на публикации, изменения и SHA-256 указаны в `manifest.json`.

| Источник | Автор | Использование |
| --- | --- | --- |
| [Fantozzi’s Footsteps](https://opengameart.org/content/fantozzis-footsteps-grasssand-stone) | Fantozzi, нарезка qubodup | Контакт подошвы с твёрдым покрытием и сыпучим грунтом |
| [Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney | Два разных записанных выстрела Walther PPQ |
| [VCSL](https://github.com/sgossner/VCSL) | Versilian Studios / Sam Gossner и участники VCSL | Акустическое пианино Kawai, маримба, барабаны, шейкер и удар по металлу |

Музыка **Morning Shift**: новая аранжировка CareProject, 32 такта, 92 BPM,
около 83,5 секунды. Последовательность нот и ритм записаны в
`Tools/GeneratePrototypeAudio.py`. Запись чужой песни или готовая мелодия не используются.

Из корня проекта: `python Tools/GeneratePrototypeAudio.py`.
Нужны Python 3, NumPy, SciPy и ffmpeg в PATH. Запуск офлайн: генератор проверяет
контрольные суммы источников, собирает WAV/OGG и сохраняет существующие GUID.
Готовые клипы уже в `Assets/IntercomMVP/Audio`; для обычного запуска игры генерация не нужна.
