"""Signed Int64 local IDs for Unity text scenes; never allocate after SceneRoots.

Unity's SceneRoots object uses Int64.MaxValue. A max(existing_ids)+1 allocator
therefore generates invalid component IDs even in an otherwise ordinary scene.
"""
import re

MIN_LOCAL_ID = -(1 << 63)
MAX_LOCAL_ID = (1 << 63) - 1
SCENE_ROOTS_CLASS = 1660057539
ANCHOR = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", re.M)
REFERENCE = re.compile(r"\{([^{}\n]*\bfileID:\s*(-?\d+)[^{}\n]*)\}")


class SceneIdAllocator:
    """Allocate in an explicit positive range, skipping reserved/existing IDs."""

    def __init__(self, existing_ids, start=8200000000):
        self.used = set(existing_ids)
        if any(not MIN_LOCAL_ID <= value <= MAX_LOCAL_ID for value in self.used):
            raise ValueError("Existing scene has a local ID outside signed Int64")
        if not 0 <= start < MAX_LOCAL_ID:
            raise ValueError("Scene allocation start must be below Int64.MaxValue")
        self.next = start

    def release(self, removed_ids):
        self.used.difference_update(removed_ids)

    def allocate(self):
        while self.next < MAX_LOCAL_ID - 1:
            self.next += 1
            if self.next not in self.used:
                self.used.add(self.next)
                return self.next
        raise OverflowError("No signed Int64 scene IDs remain in the allocation range")


def validate_scene_ids(text, require_local_references=True):
    """Validate anchors, signed ranges and local references; skip external GUID refs.

    The sentinel is legal only as the SceneRoots anchor. External fileIDs belong
    to their referenced assets and are range-checked but not resolved in this file.
    """
    objects = {}
    for match in ANCHOR.finditer(text):
        kind, fid = int(match[1]), int(match[2])
        if not MIN_LOCAL_ID <= fid <= MAX_LOCAL_ID:
            raise ValueError(f"Object ID {fid} is outside signed Int64")
        if fid == 0:
            raise ValueError("Zero is a null reference, not an object ID")
        if fid == MAX_LOCAL_ID and kind != SCENE_ROOTS_CLASS:
            raise ValueError("Int64.MaxValue is reserved for SceneRoots")
        if fid in objects:
            raise ValueError(f"Duplicate object ID {fid}")
        objects[fid] = kind
    local_count = external_count = 0
    for match in REFERENCE.finditer(text):
        contents, fid = match[1], int(match[2])
        if not MIN_LOCAL_ID <= fid <= MAX_LOCAL_ID:
            raise ValueError(f"Reference ID {fid} is outside signed Int64")
        if re.search(r"\bguid:\s*[0-9a-fA-F]{32}\b", contents):
            external_count += 1
            continue
        local_count += 1
        if require_local_references and fid != 0 and fid not in objects:
            raise ValueError(f"Unresolved local reference {fid}")
    return {"objects": len(objects), "localReferences": local_count,
            "externalReferences": external_count}
