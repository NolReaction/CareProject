#!/usr/bin/env python3
"""Run all standalone regression projects. Requires Python 3 and .NET SDK 8+."""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help="Path to the dotnet executable")
    parser.add_argument("--no-restore", action="store_true", help="Use previously restored build assets (offline)")
    args = parser.parse_args()
    executable = shutil.which(args.dotnet)
    if executable is None:
        parser.error(".NET SDK not found. Install .NET SDK 8+ or use --dotnet /path/to/dotnet")
    projects = sorted(Path(__file__).resolve().parent.glob("*Tests/*.csproj"))
    if not projects:
        parser.error("No validation projects found; check that the project archive was fully extracted")
    failures = []
    for project in projects:
        print(f"\n=== {project.stem} ===", flush=True)
        command = [executable, "run", "--project", str(project), "--configuration", "Release"]
        if args.no_restore:
            command.append("--no-restore")
        result = subprocess.run(command)
        if result.returncode:
            failures.append(project.stem)
    print(f"\n{len(projects) - len(failures)}/{len(projects)} projects passed.")
    if failures:
        print("Failed: " + ", ".join(failures))
    print("Standalone logic tests only. Unity import, compilation, Play Mode and device testing are separate.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
