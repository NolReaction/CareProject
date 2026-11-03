# Player controls through menu, focus and retry

This project compiles the production `IntercomPlayerInput`, `IntercomPlayerMotor`,
`IntercomPlayerVitality`, `IntercomHealth`, `IntercomMissionDirector`, mission
state and mission definition together. Changing a component's `enabled` property
invokes its real OnEnable/OnDisable methods in the fixture. The actual Motor's
movement changes the narrow CharacterController stand-in's position.

The missing-link fixture starts with `Input.mission == null`, matching an older
scene where the new component reference was not connected. The old Director
blocks the real Motor but leaves the normal HUD visible. A deflected left touch
stick publishes Move.y=0.7727273, yet the Motor stays disabled and travel is zero.
The corrected Director reconnects Input before acquiring its menu lock, so the
selection is visible. Touching a mode card starts the actual mission, releases
the block and lets the left stick move the actual Motor.

The held-key regression holds W through cancellation and mode selection. Keyboard
movement resumes independently of a consumed firing pointer. Tests also cover
arrow keys, focus return, retry, a fresh fire press after releasing the old
pointer, and death's movement/weapon lock. Early menu locking before
Vitality.Awake and ordinary post-Awake locking are both exercised.

NPCs, ally behavior and navigation are narrow stand-ins here; those production
components have separate tests. This harness does not run Unity's scheduler,
PhysX or Animator. It proves defined component control flow, not Play Mode.
