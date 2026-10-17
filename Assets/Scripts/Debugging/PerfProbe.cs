using System.Text;
using UnityEngine;
using Unity.Profiling;

namespace NasCon.Debugging
{
    /// <summary>
    /// Records the numbers that decide where a frame goes, and writes them to Logs/perf.txt.
    ///
    /// Reading a video of the Profiler is not reliable - the numbers are small text and video
    /// compression destroys them. This samples the same counters directly, over a few seconds of
    /// real play, and leaves a file behind that survives the session ending.
    ///
    /// Drop it on any object in the scene, play for ten seconds, then read the file.
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        [Tooltip("How long to sample before writing the summary.")]
        [SerializeField] float sampleSeconds = 12f;

        [Tooltip("Seconds between progress lines.")]
        [SerializeField] float lineEvery = 2f;

        [Tooltip("Also write to the console.")]
        [SerializeField] bool logToConsole = true;

        ProfilerRecorder batches;
        ProfilerRecorder setPass;
        ProfilerRecorder mainThread;
        ProfilerRecorder gpuFrame;

        StringBuilder report;
        float elapsed;
        float nextLine;
        int frames;
        float worstFrame;
        float bestFrame = 999f;

        // accumulated ms so the summary is an average, not a single unlucky frame
        float cpuSum, gpuSum;
        int cpuSamples, gpuSamples;
        long batchSum, setPassSum;
        int renderSamples;

        void OnEnable()
        {
            // names differ slightly by Unity version; unavailable counters simply report invalid
            batches   = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches", 1);
            setPass   = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls", 1);
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1);
            gpuFrame  = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);

            report = new StringBuilder(4096);
            report.AppendLine("NasCon'26 performance probe");
            report.AppendLine("resolution " + Screen.width + "x" + Screen.height);
            report.AppendLine("target frame rate " + Application.targetFrameRate + "   vSync " + QualitySettings.vSyncCount);
            report.AppendLine("");

            elapsed = 0f;
            nextLine = 0f;
            frames = 0;
        }

        void OnDisable()
        {
            batches.Dispose();
            setPass.Dispose();
            mainThread.Dispose();
            gpuFrame.Dispose();

            if (report != null) Finish();
        }

        void Update()
        {
            // unscaled: if something has stopped the clock the probe still needs to report
            float dt = Time.unscaledDeltaTime;

            // A stalled frame is not a game frame. Unfocusing the editor, an asset reimport or a flood
            // of warnings can all produce a multi-second delta that would swamp every average and read
            // as "0 fps". Skip those rather than averaging them in.
            if (dt <= 0f || dt > 0.5f) return;

            elapsed += dt;
            frames++;

            if (dt > worstFrame) worstFrame = dt;
            if (dt < bestFrame) bestFrame = dt;

            if (batches.Valid) { batchSum += batches.LastValue; renderSamples++; }
            if (setPass.Valid) setPassSum += setPass.LastValue;
            if (mainThread.Valid) { cpuSum += mainThread.LastValue / 1000000f; cpuSamples++; }
            if (gpuFrame.Valid) { gpuSum += gpuFrame.LastValue / 1000000f; gpuSamples++; }

            if (elapsed >= nextLine)
            {
                nextLine += lineEvery;

                // GC counters are the point of this probe: a single 200 ms hitch with a 3 ms average
                // frame is almost always a collection, and these two numbers prove it or rule it out.
                string gc = "   gc" + System.GC.CollectionCount(0) +
                            "/" + System.GC.CollectionCount(1) +
                            "/" + System.GC.CollectionCount(2);
                string managed = "   managed " + (System.GC.GetTotalMemory(false) / 1048576).ToString("0") + " MB";
                string unity = "   unity " + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576).ToString("0") + " MB";

                Write("t=" + elapsed.ToString("0.0") + "s  fps " + (frames / elapsed).ToString("0") +
                      "   batches " + (batches.Valid ? batches.LastValue.ToString() : "-") +
                      "   setpass " + (setPass.Valid ? setPass.LastValue.ToString() : "-") +
                      "   cpu " + (mainThread.Valid ? (mainThread.LastValue / 1000000f).ToString("0.0") + " ms" : "-") +
                      "   gpu " + (gpuFrame.Valid ? (gpuFrame.LastValue / 1000000f).ToString("0.0") + " ms" : "-") +
                      gc + managed + unity);
            }

            if (elapsed >= sampleSeconds)
            {
                Finish();
                enabled = false;
            }
        }

        void Finish()
        {
            float avgFps = elapsed > 0f ? frames / elapsed : 0f;

            report.AppendLine("");
            report.AppendLine("=== summary over " + elapsed.ToString("0.0") + "s, " + frames + " frames");
            report.AppendLine("avg fps        " + avgFps.ToString("0.0"));
            report.AppendLine("worst frame    " + (worstFrame * 1000f).ToString("0.0") + " ms  (" + (worstFrame > 0f ? (1f / worstFrame).ToString("0") : "?") + " fps)");
            report.AppendLine("best frame     " + (bestFrame * 1000f).ToString("0.0") + " ms");

            if (renderSamples > 0)
                report.AppendLine("avg batches    " + (batchSum / renderSamples).ToString("0") +
                                  (batchSum / renderSamples > 400 ? "   <- HIGH: batching is not working" : ""));
            if (renderSamples > 0)
                report.AppendLine("avg setpass    " + (setPassSum / renderSamples).ToString("0"));
            if (cpuSamples > 0)
                report.AppendLine("avg cpu        " + (cpuSum / cpuSamples).ToString("0.0") + " ms");
            if (gpuSamples > 0)
                report.AppendLine("avg gpu        " + (gpuSum / gpuSamples).ToString("0.0") + " ms");

            report.AppendLine("");
            if (cpuSamples > 0 && gpuSamples > 0)
                report.AppendLine("verdict: " + (gpuSum / gpuSamples > cpuSum / cpuSamples
                    ? "GPU-bound - lights, post processing or overdraw"
                    : "CPU-bound - scripts, physics or culling"));
            else
                report.AppendLine("verdict: GPU Frame Time unavailable, compare avg fps against the frame budget");

            Write(report.ToString());

            string path = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "perf.txt");
            try
            {
                System.IO.File.WriteAllText(path, report.ToString());
                Debug.Log("[PerfProbe] wrote " + System.IO.Path.GetFullPath(path));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PerfProbe] could not write file: " + e.Message);
            }
        }

        void Write(string line)
        {
            report.AppendLine(line);
            if (logToConsole) Debug.Log("[PerfProbe] " + line);
        }
    }
}
