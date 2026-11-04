"""Rebuild the Udarnik source and derived game assets with Blender 5.2+.

Original.glb -> repaired editable parts -> ten Actions -> packed .blend,
one game skin and six static breakup chunks. Run from any directory:
  blender -b --python-exit-code 1 --python BlenderTools/PrepareUdarnik.py
This regenerates the source .blend; back up any manual edits first.
"""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name('AnimateUdarnik.py')), run_name='__main__')
