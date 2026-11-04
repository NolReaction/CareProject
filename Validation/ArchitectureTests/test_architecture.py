"""Contract regressions for the architecture validator, using temporary files only."""
from pathlib import Path
import importlib.util
import tempfile
import unittest

source = Path(__file__).resolve().parents[1] / 'check_architecture.py'
spec = importlib.util.spec_from_file_location('architecture_check', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ArchitectureContracts(unittest.TestCase):
    def test_editor_guard_is_release_excluded(self):
        text = 'using UnityEngine;\n#if UNITY_EDITOR\nUnityEditor.Handles.Label();\n#endif\nCall();'
        result = module.without_editor_blocks(text)
        self.assertNotIn('UnityEditor', result)
        self.assertIn('Call()', result)

    def test_editor_else_remains_release_code(self):
        text = '#if UNITY_EDITOR\nEditor();\n#else\nPlayer();\n#endif'
        result = module.without_editor_blocks(text)
        self.assertNotIn('Editor()', result)
        self.assertIn('Player()', result)

    def test_nested_feature_cannot_escape_editor_guard(self):
        text = '#if UNITY_EDITOR\n#if SOME_FEATURE\nUnityEditor.Bad();\n#endif\n#endif\nPlayer();'
        result = module.without_editor_blocks(text)
        self.assertNotIn('UnityEditor', result)
        self.assertIn('Player()', result)

    def test_unknown_feature_keeps_both_possible_player_branches(self):
        text = '#if ENABLE_INPUT_SYSTEM\nPlayerNew();\n#else\nUnityEditor.Bad();\n#endif'
        result = module.without_editor_blocks(text)
        self.assertIn('PlayerNew()', result)
        self.assertIn('UnityEditor.Bad()', result)

    def test_comments_and_labels_are_not_code_dependencies(self):
        text = '// UnityEditor docs\nvar label="UnityEngine"; /* MonoBehaviour */ RealCall();'
        result = module.code_only(text)
        self.assertNotIn('UnityEditor', result)
        self.assertNotIn('UnityEngine', result)
        self.assertIn('RealCall()', result)

    def test_missing_project_layout_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            result = module.audit(Path(folder))
            self.assertEqual(result['status'], 'FAIL')
            self.assertIn('Runtime sources not found', result['failures'])

    def test_duplicate_guid_and_bad_compile_path_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for index in (1, 2):
                path = root / f'Assets/IntercomMVP/Content/Test{index}.txt'
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('test')
                Path(str(path)+'.meta').write_text('guid: '+'a'*32+'\n')
            project = root / 'Validation/ExampleTests/ExampleTests.csproj'
            project.parent.mkdir(parents=True)
            project.write_text('<Project><ItemGroup><Compile Include="missing.cs"/></ItemGroup></Project>')
            result = module.audit(root)
            self.assertTrue(any('Duplicate GUID' in failure for failure in result['failures']))
            self.assertTrue(any('Broken Compile path' in failure for failure in result['failures']))

    def test_shared_health_cannot_depend_on_weapon_feature(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            shared = root / 'Assets/IntercomMVP/Runtime/Shared/Damage/IntercomHealthModel.cs'
            combat = root / 'Assets/IntercomMVP/Runtime/Combat/Weapons/IntercomWeaponModel.cs'
            for path in (shared, combat): path.parent.mkdir(parents=True, exist_ok=True)
            shared.write_text('namespace IntercomMvp { public class IntercomHealthState { float x=IntercomWeaponBalance.Positive(1); } }')
            combat.write_text('namespace IntercomMvp { public class IntercomWeaponBalance {} }')
            result = module.audit(root)
            self.assertTrue(any('Shared layer depends on a feature' in failure for failure in result['failures']))

    def test_navigator_canonical_path_and_guid_must_agree(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            paths_source = root / 'Assets/IntercomMVP/Editor/Project/IntercomProjectPaths.cs'
            asset = root / 'Assets/IntercomMVP/Content/Test.asset'
            paths_source.parent.mkdir(parents=True)
            asset.parent.mkdir(parents=True)
            asset.write_text('asset')
            Path(str(asset)+'.meta').write_text('guid: '+'b'*32+'\n')
            paths_source.write_text('class IntercomProjectPaths { '
                                    'AssetKey A = new AssetKey("Missing", "'+'a'*32+'", "Assets/IntercomMVP/Content/Missing.asset"); '
                                    'AssetKey B = new AssetKey("Wrong GUID", "'+'a'*32+'", "Assets/IntercomMVP/Content/Test.asset"); }')
            result = module.audit(root)
            self.assertTrue(any('Navigator canonical asset path does not exist' in failure for failure in result['failures']))
            self.assertTrue(any('Navigator asset GUID/path mismatch' in failure and 'Test.asset' in failure for failure in result['failures']))

    def test_hidden_sync_file_is_not_a_unity_asset(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            hidden = root / 'Assets/IntercomMVP/Content/.Model.fbx.sync'
            nested = root / 'Assets/IntercomMVP/.Sync/Temporary.cs'
            for path in (hidden, nested):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('temporary')
            result = module.audit(root)
            self.assertFalse(any('Asset lacks metadata' in failure for failure in result['failures']))
            self.assertFalse(any('stored among art/content' in failure for failure in result['failures']))


if __name__ == '__main__': unittest.main()
