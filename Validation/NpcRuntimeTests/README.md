# NPC runtime/factory integration checks (V24)

`dotnet run --project Validation/NpcRuntimeTests` compiles the production capsule
controller, factory, definition and brain against narrow stand-ins. The real
health arithmetic and shared navigation are checked by their own test projects.

Coverage includes initialization before activation, disabled visual colliders,
shared materials, early tracking followed by the original fixed commitment window,
actual reach limits, low and upper obstruction rays, death/pause/respawn
cancellation, target handoffs without stale strikes, pool reuse and new home
positions, bounded investigation of a recorded location without hidden target
tracking, stable approach sides, lateral stuck recovery, safe deferred routes,
final motion checks and owned mesh disposal.

V11 also executes the actual controller arrival: warning/underground phase,
collision and vulnerability lease, no hidden melee, frozen time/focus/pause,
death/disable/respawn cancellation, exact final pose, and 100 pooled
arrival/death cycles without new objects/materials/destruction. Distant sight
samples are counted over100 updates; close-strike rays and geometry/target
invalidation remain immediate, and cached perception retains the last sampled
location rather than reading later movement through a wall.

The stand-ins provide deterministic callbacks and ray results. This validates
the controller's use of those results, not actual Unity lifecycle ordering,
Physics raycasting, CharacterController contact resolution or rendered output.

V18 также исполняет реальный адаптер импортированной модели: один prefab на слот пула, точные часы замаха/удара, шаг от фактически пройденного пути, неподвижный collider root, безопасный неактивный reset, death/emerge и восстановление исходного MaterialPropertyBlock. Animator API заменён счётчиком выборки поз: воспроизведение FBX и skinning проверяются отдельно в Blender и Unity.


V20 проверяет три архетипа на одном пуле без копирования Definition: здоровье,
скорость, урон, размеры коллайдера и визуала, постоянный оттенок атласа,
возврат к Standard и восстановление после появления/урона/смерти. Публичный
реестр даёт диагностике доступ к актёрам без выделения массива.

Для толпы проверяются разные устойчивые места вокруг одной цели, освобождение
мест при смерти/смене цели/отключении, обход дальней стороны цели по внешней
дуге, закрытые препятствием направления, торможение и окончательное ограничение
ускоренной скорости окружностями соседей. Учитываются и соседи с другой целью.

Два динамических сценария по 12 секунд исполняют реальный контроллер восьми
смешанных и восьми тяжёлых противников. Даже без раздвигания PhysX между
коллайдерами остаётся зазор 0,03 м, и каждый получает возможность замаха.
Это кинематические заменители API, а не результат настоящего Unity/Profiler.
Нативный сценарий редактора и замеры отдельно описаны в документации проекта.

V24 checks a bounded trail of genuinely observed target travel. On an open-ground
elbow, newly opened visibility and navigation replans retain the oldest recorded
turn as their goal. Cached sight does not reveal newer live positions. A stopped
target is caught up, and target changes, respawn and hidden emergence discard
old history.

Arrival at a target point inside the inflated navigation boundary uses a safe
projected endpoint, with low/upper rays preventing consumption across a closed
wall. Reopening the corridor resumes pursuit. Low-FPS endpoint steps cannot
overshoot a committed corner or redirect a legal detour into blocked geometry.
The 512-point overflow case keeps every retained corner, reaches its actual
last point, then allows fresh visible acquisition without teleporting the actor.

The V22 movement/combat profile is preserved: 2.65m/s base speed, runner1.12,
10m/s² acceleration, .30s windup, .48s recovery and original early attack
tracking. Nearest-side final melee reservations remain, but only after the
recorded route has been caught up. Local wall-safe A* remains available to
reach each recorded point; the grid is not converted to a stair-step path.

Color-only hit feedback is exercised for all three archetypes with both the
capsule fallback and imported Animator: exact visual TRS, locomotion phase and
follow-through state survive an accepted hit. The renderer still receives a
short .16s warm tint. Hurt/HurtHeavy clips remain compatible assets but are
never selected by live damage presentation.
