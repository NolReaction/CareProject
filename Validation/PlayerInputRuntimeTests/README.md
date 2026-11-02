# Actual input component regression

Compiles and runs the production `IntercomPlayerInput`, twin-stick state and reload-pointer
capture using a controlled Legacy input backend. The harness executes Awake, OnEnable,
Update and OnGUI call paths, checking mouse/touch reload, drag isolation, uninterrupted
left-stick movement, reused pointer IDs, cancellation/pause and R vs Backspace.

GUI calls are recorded by narrow stand-ins. This is not a real device touch test or an
image/rendering test. The Input System branch also needs actual Unity verification.
