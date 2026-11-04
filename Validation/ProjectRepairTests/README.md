# Project repair diagnostics

Runs the production read-only disk audit against temporary project trees: current versus old HUD sources, duplicate imported scripts, nested overlay/full ZIPs, missing scene/menu-camera link, moved files and Windows line endings. Diagnostics must leave all files unchanged.

The Unity window, import, Undo, actual Scene saving and native callbacks require Unity Editor. The test does not claim to execute those operations.
