#!/usr/bin/env python3
"""Serialize the current DistrictLayout.json with the canonical V7 writer.

This compatibility entry point preserves unrelated scene blocks, materials,
lighting, robot, camera and audio. It never regenerates the obsolete capsules.
Use BuildTutorialDistrict.py --apply-scene to regenerate the authored layout.
"""
import json
from BuildTutorialDistrict import LAYOUT, build_modules, clearance, write_scene


def main():
    build_modules()
    layout=json.loads(LAYOUT.read_text())
    report={"pathClearance":clearance(layout),"sceneIntegration":write_scene(layout)}
    print(json.dumps(report,indent=2))


if __name__ == "__main__":
    main()
