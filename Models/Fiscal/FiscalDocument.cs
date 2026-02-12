using System;
using System.Collections.Generic;

namespace GlassHub.Models.Fiscal;

public class FiscalDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string XmlContent { get; set; } = string.Empty;
    public FiscalDocumentStatus Status { get; set; } = FiscalDocumentStatus.Pending;
    public string AccessKey { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public enum FiscalDocumentStatus
{
    Pending,
    Valid,
    Invalid,
    Warning
}
