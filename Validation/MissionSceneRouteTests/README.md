# Mission scene routes

Reads the shipped Unity scene's nine spawn markers, rescue point, extraction point
and player start. Reconstructs the authored courtyard's collision bounds and uses
the production navigation grid to test all 144 ordered routes, corner-safe path
segments and two independent rescue approach points.

This checks authored positions against the real path algorithm. It does not run
Unity's importer, CharacterController or PhysX.
