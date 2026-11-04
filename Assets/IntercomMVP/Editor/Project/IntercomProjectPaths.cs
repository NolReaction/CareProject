namespace IntercomMvp.Editor
{
    /// <summary>Named developer entry points. GUID identity has priority over the documented folder location.</summary>
    internal static class IntercomProjectPaths
    {
        internal readonly struct AssetKey
        {
            internal readonly string Label, Guid, CanonicalPath;
            internal AssetKey(string label, string guid, string path)
            { Label = label; Guid = guid; CanonicalPath = path; }
            internal string Resolve() => IntercomAssetCatalog.ResolveGuid(Guid, CanonicalPath);
        }

        internal const string StartGuide = "START_HERE.md";
        internal const string ArchitectureGuide = "Docs/Architecture.md";
        internal const string DeveloperGuide = "Docs/DeveloperGuide.md";
        internal const string AnimationGuide = "Source/Robot/README.md";
        internal const string ValidationFolder = "Validation";
        internal const string RobotSource = "Source/Robot/intercom-armed.blend";

        internal static readonly AssetKey TutorialScene = new AssetKey("Учебная сцена", "7012dfc77afc7c84cb7ba39f79f3538b", "Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity");
        internal static readonly AssetKey Pistols = new AssetKey("Пистолеты — баланс и связанные ресурсы", "efc4ccf9a1224f078dadf974b01d842e", "Assets/IntercomMVP/Content/Weapons/Pistols/Settings/DualPistols.asset");
        internal static readonly AssetKey PistolPose = new AssetKey("Пистолеты — позы и переходы рук", "0ab8d7400fffe4d449da7ba975efd7d1", "Assets/IntercomMVP/Content/Weapons/Pistols/Animation/DualPistolsPose.asset");
        internal static readonly AssetKey RobotModel = new AssetKey("Герой — модель, материалы и клипы FBX", "b5253562122cbe443b18acf9dd9a2e33", "Assets/IntercomMVP/Content/Characters/Robot/Model/IntercomRobot.fbx");
        internal static readonly AssetKey RobotAnimator = new AssetKey("Герой — Animator", "4155e29e155c9574282e54595a238c38", "Assets/IntercomMVP/Content/Characters/Robot/Animation/IntercomDirectional.controller");
        internal static readonly AssetKey Enemy = new AssetKey("Ударник — HP, скорость, удар и появление", "2ce1e51a138c5768a9b9dafbc60a68c1", "Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset");
        internal static readonly AssetKey Elimination = new AssetKey("Задание — 50 роботов", "2524121e461b57e39ef7c1c0f823aa04", "Assets/IntercomMVP/Content/Missions/Kill50.asset");
        internal static readonly AssetKey Rescue = new AssetKey("Задание — спасение", "4ae86bc1b1df5b3690b5358015c52c6b", "Assets/IntercomMVP/Content/Missions/Rescue.asset");
        internal static readonly AssetKey Audio = new AssetKey("Общий звук — музыка, шаги и попадания", "f7fe0dc3980852bd9f9c4bb2c48c718c", "Assets/IntercomMVP/Content/Shared/Audio/Settings/GameAudio.asset");
        internal static readonly AssetKey YardLayout = new AssetKey("Двор — описание размещения объектов", "f07086c37a3d505ca85330a3d0f572cf", "Assets/IntercomMVP/Content/Environments/TutorialYard/Layout/DistrictLayout.json");
        internal static readonly AssetKey Lighting = new AssetKey("Двор — дневной свет", "f5955e7711685945b8dc4d48c8a7d70b", "Assets/IntercomMVP/Settings/Rendering/Lighting/TutorialDaylight.asset");
        internal static readonly AssetKey RenderPipeline = new AssetKey("URP — настройки отрисовки", "93685289a1ce46c4bb4eb119279f4b21", "Assets/IntercomMVP/Settings/Rendering/URP/IntercomPipeline.asset");

        internal static readonly AssetKey[] All = {
            TutorialScene, Pistols, PistolPose, RobotModel, RobotAnimator, Enemy,
            Elimination, Rescue, Audio, YardLayout, Lighting, RenderPipeline
        };
    }
}
