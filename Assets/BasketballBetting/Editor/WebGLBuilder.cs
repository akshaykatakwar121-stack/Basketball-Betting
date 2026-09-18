using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BasketballBetting.EditorTools
{
    /// <summary>
    /// Builds the game for the browser. Settings are applied in code so every build is identical.
    /// Menu: Basketball Betting / Build WebGL.
    /// Batch: -buildTarget WebGL -executeMethod BasketballBetting.EditorTools.WebGLBuilder.BuildFromCommandLine -webglOut &lt;dir&gt;
    /// </summary>
    public static class WebGLBuilder
    {
        [MenuItem("Basketball Betting/Build WebGL")]
        public static void BuildMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("WebGL output", Directory.GetParent(Application.dataPath).FullName, "WebGLBuild");
            if (!string.IsNullOrEmpty(dir))
                Build(dir);
        }

        public static void BuildFromCommandLine()
        {
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "WebGLBuild");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-webglOut")
                    outDir = args[i + 1];
            bool ok = Build(outDir);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Build(string outDir)
        {
            PlayerSettings.productName = "Hoops";
            PlayerSettings.companyName = "BasketballBetting";
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // works on any static host, no special headers
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.WebGL.showDiagnostics = false;
            // Unity 6000.5.4 references IDBFS (PlayerPrefs / persistent storage) without linking it:
            // the page stops at 90% with "IDBFS is not defined" unless the library is forced in.
#pragma warning disable CS0618
            PlayerSettings.WebGL.emscriptenArgs = "-sDEFAULT_LIBRARY_FUNCS_TO_INCLUDE=$IDBFS";
#pragma warning restore CS0618
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);

            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ArenaSceneBuilder.ScenePath },
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                locationPathName = outDir,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[WebGLBuilder] result={s.result} size={s.totalSize / (1024f * 1024f):F1}MB time={s.totalTime} errors={s.totalErrors} out={outDir}");
            return s.result == BuildResult.Succeeded;
        }
    }
}
