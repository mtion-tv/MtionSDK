using System;
using System.Collections.Generic;
using mtion.room.sdk.compiled;
using Newtonsoft.Json;

namespace mtion.room.sdk
{
    [Serializable]
    public sealed class SDKExportReportIssue
    {
        [JsonProperty("severity")]
        public string Severity { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    [Serializable]
    public sealed class SDKExportReport
    {
        [JsonProperty("report_version")]
        public string ReportVersion { get; set; } = "1.0.0";

        [JsonProperty("started_at_ms")]
        public long StartedAtMS { get; set; }

        [JsonProperty("completed_at_ms")]
        public long CompletedAtMS { get; set; }

        [JsonProperty("succeeded")]
        public bool Succeeded { get; set; }

        [JsonProperty("root_guid")]
        public string RootGuid { get; set; }

        [JsonProperty("root_internal_id")]
        public string RootInternalId { get; set; }

        [JsonProperty("root_type")]
        public string RootType { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("output_directory")]
        public string OutputDirectory { get; set; }

        [JsonProperty("exception")]
        public string Exception { get; set; }

        [JsonProperty("restored_backups")]
        public List<string> RestoredBackups { get; set; } = new List<string>();

        [JsonProperty("issues")]
        public List<SDKExportReportIssue> Issues { get; set; } = new List<SDKExportReportIssue>();

        [JsonProperty("artifacts")]
        public List<ExportManifestArtifact> Artifacts { get; set; } = new List<ExportManifestArtifact>();

        public void AddIssue(string severity, string message)
        {
            Issues.Add(new SDKExportReportIssue
            {
                Severity = severity,
                Message = message,
            });
        }

        public void AddInfo(string message)
        {
            AddIssue("info", message);
        }

        public void AddWarning(string message)
        {
            AddIssue("warning", message);
        }

        public void AddError(string message)
        {
            AddIssue("error", message);
        }
    }
}
