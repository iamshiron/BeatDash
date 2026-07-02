using HarmonyLib;

namespace Shiron.BeatDash.Mod.Harmony;

[HarmonyPatch(typeof(global::BeatmapObjectExecutionRatingsRecorder), "HandleScoringForNoteDidFinish")]
internal class BeatmapObjectExecutionRatingsRecorder {
    [HarmonyPostfix]
    public static void HandleScoringForNoteDidFinish_PostFix(ScoringElement scoringElement) {
        if (scoringElement is GoodCutScoringElement goodCut) {
            Plugin.Log.Info("Good cut!");
        }
        if (scoringElement is BadCutScoringElement badCute) {
            Plugin.Log.Info("Bad cut!");
        }
    }
}
