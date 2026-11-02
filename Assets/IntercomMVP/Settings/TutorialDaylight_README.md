# Дневной свет учебного двора — V5

Настройка сохранена в сцене. Для ручного подключения к другой сцене используй
**Tools → Intercom MVP → Apply Tutorial Daylight** вне Play, затем сохрани сцену.
Команда изменяет освещение и обработку изображения, сохраняя ракурс и параметры объектива.
Автоматического выполнения при открытии проекта или игрового скрипта освещения нет.

| Параметр | Значение |
| --- | --- |
| Основной свет | Один Directional, угол 45° / −35°, intensity 1.45 |
| Цвет солнца | RGB 1.0 / 0.98 / 0.94 |
| Окружение | Мягкий серо-голубой Trilight, нейтральный нижний свет |
| Тени | 2048 atlas, 2 cascades, distance 40 m, low soft shadows |
| Изображение | HDR, render scale 1, MSAA 2×, ACES |
| Грейдинг | Exposure 0 EV, contrast −3, saturation 0 |
| Контактное затемнение | SSAO: intensity 0.30, radius 0.25, half resolution, 4 samples |
| Отражения | Статический HDR cubemap, 128 px/face, reflection intensity 0.55 |

`TutorialDaylight.asset` содержит только Tonemapping и Color Adjustments.
Bloom, depth of field, motion blur, виньетка и хроматическая аберрация не добавлены.
`TutorialDaylightReflection.hdr` — аналитический линейный градиент неба/горизонта/земли
с широким солнечным бликом; исходник 512×256. Он импортируется как Cubemap с prefiltered
specular mipmaps, RGBAHalf. Примерный объём с mipmaps — 1 MiB. Захват отражений в реальном
времени не выполняется.

Для слабого телефона сначала отключи **Tutorial Contact AO** в `IntercomRenderer.asset`.
Это убирает отдельный проход SSAO, сохраняя дневной свет и тени. Другие источники света
не рендерятся текущим профилем: в этой сцене используется один солнечный источник.
Производительность и итоговый баланс нужно проверить в Unity и на целевом телефоне.

Поля renderer feature и GUID сверены с исходниками Unity Graphics ветки
[6000.3/staging](https://github.com/Unity-Technologies/Graphics/tree/6000.3/staging/Packages/com.unity.render-pipelines.universal).
[Официальные параметры SSAO Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/post-processing-ssao/ssao-renderer-feature-reference).

V5 убирает усиление насыщенности и чрезмерную синеву рассеянного света. Основное снижение
яркости цветных деталей сделано в самих материалах: разметка охристая, металл приглушённый,
асфальт почти нейтральный. Shadow strength остаётся 0.72. Цвета окружения: sky 0.62/0.68/0.74,
equator 0.48/0.53/0.56, ground 0.35/0.35/0.32. Сетка и число проходов прежние;
меньшая интенсивность SSAO меняет внешний вид, но сама по себе не ускоряет его проход.
Подробнее о форме, палитре и будущих моделях: `Docs/ART_DIRECTION.md`.
