using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PickShoot
{
    // 웹(WebGL) 빌드 — 에디터를 띄우지 않고 배치 모드로 부른다 (2026-10-15, README에서 바로 플레이용).
    //   Unity.exe -batchmode -quit -projectPath <프로젝트> -buildTarget WebGL -executeMethod PickShoot.WebBuild.Build -logFile <로그>
    // 결과는 Builds/WebGL (git 무시). GitHub Pages는 압축 헤더를 붙여 주지 않으므로 Gzip + 압축 해제 대체(decompression fallback)로 둔다 (ProjectSettings).
    // 실패하면 예외로 끝나 배치 모드가 0이 아닌 코드로 종료한다.
    public static class WebBuild
    {
        const string OutputPath = "Builds/WebGL";

        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new System.Exception("[PickShoot] WebBuild: Build Settings에 켜진 씬이 없다.");
            BuildReport report = BuildPipeline.BuildPlayer(scenes, OutputPath, BuildTarget.WebGL, BuildOptions.None);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new System.Exception($"[PickShoot] WebBuild: {summary.result} — 오류 {summary.totalErrors}개. 로그에서 'error'를 찾는다.");
            Debug.Log($"[PickShoot] WebBuild: {OutputPath} {summary.totalSize / 1048576f:0.0}MB, {summary.totalTime.TotalSeconds:0}초");
        }
    }
}
