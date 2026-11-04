# Enemy spawn policy

Executes the actual pure IntercomEnemySpawnPolicy implementation. Checks visible
arrival cadence, finite camera rectangle classification, player/ally spacing and
marker cooldown boundaries, including random geometry and overflow cases.
Actual camera projection, visibility, navigation and collision remain Unity checks.
