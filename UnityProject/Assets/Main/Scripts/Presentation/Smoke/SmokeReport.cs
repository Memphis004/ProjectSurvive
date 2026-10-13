using System.Collections.Generic;
using ProjectSurvive.Game.Presentation.Web;

namespace ProjectSurvive.Game.Presentation.Smoke
{
    /// <summary>One smoke check's outcome. Never a bare bool — the detail is the evidence.</summary>
    public sealed class ProbeResult
    {
        public ProbeResult(string name, bool passed, string detail)
        {
            Name = name;
            Passed = passed;
            Detail = detail;
        }

        /// <summary>Stable id, e.g. <c>messagepack.roundtrip</c>. Used by the browser test.</summary>
        public string Name { get; }

        public bool Passed { get; }

        /// <summary>What was actually observed, so a failure is diagnosable without a debugger.</summary>
        public string Detail { get; }

        public static ProbeResult Pass(string name, string detail) => new ProbeResult(name, true, detail);

        public static ProbeResult Fail(string name, string detail) => new ProbeResult(name, false, detail);
    }

    /// <summary>
    /// The whole smoke run: every probe's result plus the platform facts that make
    /// a green run meaningful (a pass on the Editor is not a pass on WebGL).
    /// </summary>
    public sealed class SmokeReport
    {
        private readonly List<ProbeResult> _results = new List<ProbeResult>();

        public SmokeReport(string platform, string configHash)
        {
            Platform = platform;
            ConfigHash = configHash;
        }

        /// <summary>Where this run happened, e.g. <c>WebGLPlayer</c> or <c>Editor</c>.</summary>
        public string Platform { get; }

        public string ConfigHash { get; }

        public IReadOnlyList<ProbeResult> Results => _results;

        public int FailedCount
        {
            get
            {
                int failed = 0;
                for (int i = 0; i < _results.Count; i++)
                {
                    if (!_results[i].Passed)
                    {
                        failed++;
                    }
                }

                return failed;
            }
        }

        public bool AllPassed => FailedCount == 0 && _results.Count > 0;

        public void Add(ProbeResult result) => _results.Add(result);

        /// <summary>
        /// The single string the browser reads: a stable shape for Playwright and
        /// a readable blob in the player log.
        /// </summary>
        public string ToJson()
        {
            var json = new JsonReportWriter();
            json.BeginObject()
                .Write("platform", Platform)
                .Write("configHash", ConfigHash)
                .Write("probeCount", _results.Count)
                .Write("failedCount", FailedCount)
                .Write("passed", AllPassed)
                .BeginArrayProperty("probes");

            for (int i = 0; i < _results.Count; i++)
            {
                ProbeResult r = _results[i];
                json.BeginObject()
                    .Write("name", r.Name)
                    .Write("passed", r.Passed)
                    .Write("detail", r.Detail)
                    .EndObject();
            }

            json.EndArray().EndObject();
            return json.ToString();
        }

        /// <summary>Human-readable one-line-per-probe form for the Unity console.</summary>
        public string ToLog()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("ProjectSurvive smoke (").Append(Platform).Append(") ")
              .Append(AllPassed ? "PASS" : "FAIL")
              .Append(" - ").Append(_results.Count).Append(" probes, ")
              .Append(FailedCount).Append(" failed");

            for (int i = 0; i < _results.Count; i++)
            {
                ProbeResult r = _results[i];
                sb.Append('\n')
                  .Append(r.Passed ? "  [PASS] " : "  [FAIL] ")
                  .Append(r.Name)
                  .Append(" - ")
                  .Append(r.Detail);
            }

            return sb.ToString();
        }
    }
}
