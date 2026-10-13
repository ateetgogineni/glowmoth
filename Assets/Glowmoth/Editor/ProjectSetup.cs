using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Glowmoth.EditorTools
{
    /// <summary>
    /// One-time project configuration that runs automatically when the project is opened:
    ///  - makes sure Scenes/Main.unity exists and is the only scene in Build Settings
    ///  - portrait orientation, product name, Android IL2CPP/ARM64 + high stripping (small build)
    ///  - legacy Input Manager enabled (the game uses UnityEngine.Input)
    /// Also adds a "Glowmoth" menu with art export and a one-click Android build.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Glowmoth/Scenes/Main.unity";
        const string ArtExportPath = "Assets/Glowmoth/Art/Generated/GlowmothAtlas.png";
        const string ApkPath = "Builds/Android/Glowmoth.apk";

        static string DoneKey => "Glowmoth.Setup.v1." + Application.dataPath.GetHashCode();

        static ProjectSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureScene();
            if (EditorPrefs.GetBool(DoneKey, false)) return;
            ApplyPlayerSettings();
            EditorPrefs.SetBool(DoneKey, true);
            Debug.Log("[Glowmoth] Project configured: portrait, IL2CPP/ARM64, stripping, Main scene in Build Settings. Press Play to run.");
        }

        [MenuItem("Glowmoth/Run Project Setup Again")]
        static void RunSetupMenu()
        {
            EditorPrefs.DeleteKey(DoneKey);
            AutoSetup();
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }

            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path == ScenePath && s.enabled) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Glowmoth";
            PlayerSettings.productName = "Glowmoth";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;       // cheapest on low-end GPUs; art is flat colour
            PlayerSettings.stripEngineCode = true;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.glowmoth.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.glowmoth.game");
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.High);

            QualitySettings.antiAliasing = 0;
            EnsureLegacyInput();
        }

        /// <summary>
        /// The game reads UnityEngine.Input. If the project is set to "Input System Package (New)" only,
        /// switch it to "Both" so legacy input works (needs an editor restart to take effect).
        /// </summary>
        static void EnsureLegacyInput()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty handler = so.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue != 1) return;
            handler.intValue = 2; // 0 = old, 1 = new, 2 = both
            so.ApplyModifiedProperties();
            Debug.LogWarning("[Glowmoth] Active Input Handling set to 'Both'. Please restart the Unity editor.");
        }

        [MenuItem("Glowmoth/Export Generated Art (PNG)")]
        static void ExportArt()
        {
            byte[] png = SpriteFactory.ExportAtlasPng();
            Directory.CreateDirectory(Path.GetDirectoryName(ArtExportPath));
            File.WriteAllBytes(ArtExportPath, png);
            AssetDatabase.Refresh();
            Debug.Log("[Glowmoth] Exported the generated sprite atlas to " + ArtExportPath + " (reference only; the game builds it at runtime).");
        }

        /// <summary>
        /// Portrait 540x960 windowed Windows build, used to test on PC and to record gameplay with
        ///   Builds/Windows/Glowmoth.exe -glowmothCapture C:\frames
        /// Command line: Unity.exe -batchmode -quit -projectPath . -executeMethod Glowmoth.EditorTools.ProjectSetup.BuildWindows
        /// </summary>
        [MenuItem("Glowmoth/Build Windows (portrait window)")]
        public static void BuildWindows()
        {
            EnsureScene();
            ApplyPlayerSettings();
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/Glowmoth.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("[Glowmoth] Windows build " + summary.result + ": " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        [MenuItem("Glowmoth/Build Android APK")]
        public static void BuildAndroid()
        {
            EnsureScene();
            ApplyPlayerSettings();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[Glowmoth] Android build " + summary.result + ": " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB -> " + ApkPath);
            if (summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(ApkPath);
        }
    }
}
