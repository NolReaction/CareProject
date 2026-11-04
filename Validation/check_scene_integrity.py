#!/usr/bin/env python3
"""Validate Unity local IDs, actual component ownership and material routing.

Also regenerates the scene in a temporary file, never the working scene, and
checks that the generator cannot allocate after Unity's SceneRoots sentinel.
This is a serialized-data regression; it does not execute Unity's importer.
"""
from collections import Counter
from pathlib import Path
import hashlib
import json
import re
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
from UnitySceneIds import MAX_LOCAL_ID, SceneIdAllocator, validate_scene_ids

SCENE = ROOT / "Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity"
SURFACES = ["Asphalt", "Concrete", "Metal", "Gravel", "Wood", "Soil"]


def surface_routing(text):
    validate_scene_ids(text)
    blocks = {int(fid): (int(kind), body) for kind, fid, body in re.findall(
        r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S)}
    names, owners, components, tags = {}, {}, {}, {}
    guid = re.search(r"^guid: (\w+)$", (ROOT / "Assets/IntercomMVP/Runtime/Shared/Surfaces/IntercomFootstepSurface.cs.meta").read_text(), re.M)[1]
    for fid, (kind, body) in blocks.items():
        if kind == 1:
            names[fid] = re.search(r"^  m_Name: (.*)$", body, re.M)[1]
            components[fid] = [int(x) for x in re.findall(r"^  - component: \{fileID: (-?\d+)\}", body, re.M)]
        match = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", body, re.M)
        if match and int(match[1]):
            owners[fid] = int(match[1])
        if kind == 114 and re.search(r"m_Script: \{fileID: 11500000, guid: " + guid + r",", body):
            go = owners[fid]
            assert go not in tags, f"Duplicate surface on GameObject {go}"
            assert "  m_Enabled: 1" in body, f"Disabled surface {fid}"
            material = int(re.search(r"^  kind: (\d+)$", body, re.M)[1])
            assert 0 <= material < len(SURFACES)
            gain = float(re.search(r"^  impactVolume: ([0-9.]+)$", body, re.M)[1])
            assert 0 < gain <= 1
            tags[go] = (fid, SURFACES[material])

    # Textual tags are insufficient: each component must be reachable from its
    # GameObject's m_Component list and point back to exactly that same owner.
    for go, attached in components.items():
        assert len(attached) == len(set(attached)), f"Duplicate component entry on {names[go]}"
        for component in attached:
            assert component in blocks, f"Missing component {component} on {names[go]}"
            assert owners.get(component) == go, f"Wrong owner for component {component} on {names[go]}"
    for component, go in owners.items():
        assert component in components[go], f"Detached component {component} on {names[go]}"

    layout = json.loads((ROOT / "Assets/IntercomMVP/Content/Environments/TutorialYard/Layout/DistrictLayout.json").read_text())
    # Different authored groups may reuse a module name (e.g. Low barrier).
    # Compare the complete counted name/material inventory instead of losing
    # duplicate instances through a dictionary overwrite.
    expected = Counter({("Ground - Future Map", "Asphalt"): 1})
    for group in layout["groups"]:
        for item in group["blocks"]:
            if item["collider"]:
                expected[("Collision - " + item["name"], item["footstepSurface"])] += 1
    expected.update((item["name"], item["footstepSurface"]) for item in layout["targets"])
    routed = []
    for fid, (kind, _) in blocks.items():
        if kind not in {64, 65, 135, 136}:  # Mesh, Box, Sphere, Capsule collider.
            continue
        go = owners[fid]
        name = names[go]
        assert go in tags, f"Untagged collider {name}"
        tag_id, material = tags[go]
        assert tag_id in components[go]
        assert expected[(name, material)] > 0, (name, material)
        routed.append({"object": name, "colliderID": fid, "surfaceID": tag_id, "material": material})
    assert Counter((x["object"], x["material"]) for x in routed) == expected, "Collider inventory differs from authored layout"
    return routed


def main():
    assertions = 0
    def rejects(text, message):
        nonlocal assertions
        try:
            validate_scene_ids(text)
        except ValueError as error:
            assert message in str(error), str(error)
            assertions += 1
            return
        raise AssertionError("Invalid fixture was accepted")

    for bad in [str(MAX_LOCAL_ID + 1), str(-(1 << 63) - 1)]:
        rejects(f"--- !u!114 &{bad}\nMonoBehaviour:\n", "outside signed Int64")
        rejects(f"--- !u!1 &1\nGameObject:\n  target: {{fileID: {bad}}}\n", "outside signed Int64")
    rejects("--- !u!1 &1\nGameObject:\n--- !u!114 &1\nMonoBehaviour:\n", "Duplicate")
    rejects("--- !u!1 &1\nGameObject:\n  target: {fileID: 99}\n", "Unresolved")
    rejects(f"--- !u!114 &{MAX_LOCAL_ID}\nMonoBehaviour:\n", "reserved for SceneRoots")
    validate_scene_ids(f"--- !u!1660057539 &{MAX_LOCAL_ID}\nSceneRoots:\n  m_Roots: []\n")
    validate_scene_ids("--- !u!1 &1\nGameObject:\n  material: {fileID: 2100000, guid: 12345678901234567890123456789012, type: 2}\n")
    allocator = SceneIdAllocator([MAX_LOCAL_ID, 8200000001, 8200000002])
    assert allocator.allocate() == 8200000003
    assert allocator.allocate() == 8200000004
    allocator = SceneIdAllocator([MAX_LOCAL_ID, 8200000001, 8200000002])
    allocator.release([8200000001])
    assert allocator.allocate() == 8200000001
    try:
        SceneIdAllocator([MAX_LOCAL_ID], MAX_LOCAL_ID - 1).allocate()
    except OverflowError:
        assertions += 1
    else:
        raise AssertionError("Allocator wrapped after Int64.MaxValue")
    assertions += 5

    audited = []
    for base in [ROOT / "Assets", ROOT / "ProjectSettings"]:
        for path in sorted(base.rglob("*")):
            if not path.is_file():
                continue
            with path.open("rb") as handle:
                if not handle.read(10).startswith(b"%YAML"):
                    continue
            counts = validate_scene_ids(path.read_text())
            audited.append({"path": str(path.relative_to(ROOT)), **counts})
    source = SCENE.read_text()
    contacts = surface_routing(source)
    original_hash = hashlib.sha256(SCENE.read_bytes()).hexdigest()
    # Reproduce the original class of bug using one real attached component.
    target = str(contacts[-1]["surfaceID"])
    invalid = re.sub(r"(?<=&)" + target + r"(?=\s|$)|(?<=fileID: )" + target + r"(?=[,}\s])",
                     str(MAX_LOCAL_ID + 1), source, flags=re.M)
    rejects(invalid, "outside signed Int64")

    import BuildTutorialDistrict as generator
    original_scene = generator.SCENE
    try:
        generator.build_modules()  # In-memory mesh construction; no asset export.
        layout = json.loads((ROOT / "Assets/IntercomMVP/Content/Environments/TutorialYard/Layout/DistrictLayout.json").read_text())
        with tempfile.TemporaryDirectory(prefix="care-scene-v9-") as directory:
            generator.SCENE = Path(directory) / "IntercomMVP.unity"
            generator.SCENE.write_text(source)
            generated_report = generator.write_scene(layout)
            first = generator.SCENE.read_text()
            generated_contacts = surface_routing(first)
            generator.write_scene(layout)
            assert generator.SCENE.read_text() == first, "Regeneration is not idempotent"
            generated_ids = validate_scene_ids(first)
    finally:
        generator.SCENE = original_scene
    assert hashlib.sha256(SCENE.read_bytes()).hexdigest() == original_hash, "Working scene was changed by validation"
    result = {"revision": "scene-integrity-v9", "status": "PASS", "yamlFiles": len(audited),
              "files": audited, "regressionCases": assertions, "sceneSHA256": original_hash,
              "surfaceCount": len(contacts), "surfaceCounts": dict(Counter(x["material"] for x in contacts)),
              "colliderRouting": contacts,
              "generator": {"checkedInTemporaryFile": True, "regenerationIdempotent": True,
                            "localIDs": generated_ids, "surfaceCount": len(generated_contacts),
                            "environmentObjects": generated_report["environmentObjects"]},
              "scope": "Signed Int64 IDs, local reference resolution, bidirectional component ownership and authored material routing. Not Unity importer or PhysX execution."}
    (ROOT / "Validation/SceneIntegrityReview.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({key: result[key] for key in ["status", "yamlFiles", "regressionCases", "surfaceCount", "surfaceCounts"]}))


if __name__ == "__main__":
    main()
