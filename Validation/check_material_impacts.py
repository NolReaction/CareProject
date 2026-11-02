#!/usr/bin/env python3
"""V5: authored collider -> material bank audit and deterministic PCM regression.

This validates saved assets/data. It does not simulate PhysX or listen to Unity.
"""
from collections import Counter
from pathlib import Path
import json
import re
import sys
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
from GenerateImpactAudio import impact, inspect_wave, RATE, SURFACES


def main():
    scene = (ROOT / "Assets/IntercomMVP/Scenes/IntercomMVP.unity").read_text()
    blocks = {int(fid): body for _, fid, body in re.findall(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", scene, re.M | re.S)}
    names = {}
    tags = {}
    surfaces = ["Asphalt", "Concrete", "Metal", "Gravel", "Wood", "Soil"]
    surface_guid = re.search(r"^guid: (\w+)$", (ROOT / "Assets/IntercomMVP/Runtime/IntercomFootstepSurface.cs.meta").read_text(), re.M)[1]
    for fid, body in blocks.items():
        if body.startswith("GameObject:"):
            names[fid] = re.search(r"^  m_Name: (.*)$", body, re.M)[1]
        if surface_guid in body:
            go = int(re.search(r"m_GameObject: \{fileID: (\d+)\}", body)[1])
            kind = int(re.search(r"^  kind: (\d+)$", body, re.M)[1])
            assert 0 <= kind < len(surfaces), names.get(go, go)
            assert "  m_Enabled: 1" in body
            gain = float(re.search(r"^  impactVolume: ([0-9.]+)$", body, re.M)[1])
            assert 0 < gain <= 1, (names.get(go, go), gain)
            tags[go] = surfaces[kind]
    layout = json.loads((ROOT / "Assets/IntercomMVP/World/DistrictLayout.json").read_text())
    expected = {"Ground - Future Map": "Asphalt"}
    for group in layout["groups"]:
        for block in group["blocks"]:
            if block["collider"]:
                name = "Collision - " + block["name"]
                assert name not in expected or expected[name] == block["footstepSurface"]
                expected[name] = block["footstepSurface"]
    expected.update({target["name"]: target["footstepSurface"] for target in layout["targets"]})
    contacts = []
    for fid, body in blocks.items():
        if not body.startswith(("BoxCollider:", "MeshCollider:", "CapsuleCollider:", "SphereCollider:")):
            continue
        go = int(re.search(r"m_GameObject: \{fileID: (\d+)\}", body)[1])
        name = names[go]
        assert go in tags, f"Untagged collider: {name}"
        assert expected[name] == tags[go], (name, expected[name], tags[go])
        contacts.append({"name": name, "material": tags[go]})
    assert len(contacts) >= 100 and {"Metal", "Wood", "Concrete"} <= {x["material"] for x in contacts}

    measured = {}
    for surface in SURFACES:
        measured[surface] = []
        for index in range(3):
            path = ROOT / f"Assets/IntercomMVP/Audio/Impact_{surface.title()}_{index+1:02d}.wav"
            with wave.open(str(path)) as wav:
                assert wav.getframerate() == RATE and wav.getnchannels() == 1 and wav.getsampwidth() == 2
                pcm = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2")
            assert np.array_equal(pcm, np.rint(impact(index, surface) * 32767).astype("<i2")), path
            x = pcm.astype(float) / 32768
            assert x[0] == 0 and x[-1] == 0 and np.max(abs(x)) < .4
            metrics = inspect_wave(x)
            assert metrics["clippedSamples"] == 0 and metrics["seconds"] < .26
            spectrum = abs(np.fft.rfft(x)) ** 2
            f = np.fft.rfftfreq(len(x), 1 / RATE)
            assert sum(spectrum[f > 6000]) / sum(spectrum) < .1, "Excessive high-frequency splash"
            measured[surface].append(metrics)
    # Regression against four generic cracks: require different body spectra and
    # longer metal decay across ALL variants, rather than comparing file hashes.
    centroids = {s: [m["energyCentroidHz"] for m in values] for s, values in measured.items()}
    assert max(centroids["wood"]) < min(centroids["metal"]) * .55
    assert max(centroids["metal"]) * 2 < min(centroids["concrete"])
    assert min(m["energy90ms"] for m in measured["metal"]) > 1.6 * max(m["energy90ms"] for m in measured["wood"])

    result = {"revision": "V5", "colliders": len(contacts), "surfaceCounts": dict(Counter(x["material"] for x in contacts)),
              "contacts": contacts, "audioMetrics": measured,
              "scope": "Serialized collider routing, reproducible decoded samples, timbre/envelope separation. Not runtime hits or listening approval."}
    (ROOT / "Validation/MaterialImpactRoutingV5Review.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({"colliders": len(contacts), "surfaceCounts": result["surfaceCounts"], "impactClips": 12, "status": "PASS"}))


if __name__ == "__main__":
    main()
