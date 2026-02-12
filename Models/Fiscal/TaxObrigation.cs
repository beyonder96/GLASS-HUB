using System;
using System.Linq;

namespace GlassHub.Models.Fiscal;

public class TaxObrigation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty; // e.g., DAS, SPED
    public DateTime DueDate { get; set; }
    public ObrigationStatus Status { get; set; } = ObrigationStatus.ToDo;
    public decimal Value { get; set; } 
    public string? Description { get; set; }
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.Monthly;
    
    public string? AttachmentPath { get; set; }
    public string? Observation { get; set; }
}

public enum ObrigationStatus
{
    ToDo,
    Doing,
    Done,
    Late,
    Cancelled
}

public enum RecurrenceType
{
    None,
    Daily,
    Weekly,
    Fortnightly,
    Monthly,
    Semiannual,
    Yearly
}
