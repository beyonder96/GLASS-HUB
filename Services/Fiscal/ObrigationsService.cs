using GlassHub.Models.Fiscal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassHub.Services.Fiscal;

public class ObrigationsService
{
    private List<TaxObrigation> _obligations = new();

    public event Action? OnChange;

    public ObrigationsService()
    {
    }

    public List<TaxObrigation> GetObrigations()
    {
        return _obligations.OrderBy(o => o.DueDate).ToList();
    }

    public void AddObrigation(TaxObrigation obligation)
    {
        _obligations.Add(obligation);
        
        // Handle recurrence generation (basic implementation)
        if (obligation.Recurrence != RecurrenceType.None)
        {
            GenerateRecurrences(obligation);
        }

        NotifyStateChanged();
    }
    
    public void UpdateObrigation(TaxObrigation obligation)
    {
        var existing = _obligations.FirstOrDefault(o => o.Id == obligation.Id);
        if (existing != null)
        {
            // Update fields manually to avoid replacing reference if needed, or just replace object in list logic
            // Here, since it's in-memory, we can just find index and replace
            var index = _obligations.IndexOf(existing);
            _obligations[index] = obligation;
            NotifyStateChanged();
        }
    }

    public void DeleteObrigation(string id)
    {
        var obligation = _obligations.FirstOrDefault(o => o.Id == id);
        if (obligation != null)
        {
            _obligations.Remove(obligation);
            NotifyStateChanged();
        }
    }
    
    public void MarkAsDone(string id)
    {
        var ob = _obligations.FirstOrDefault(o => o.Id == id);
        if (ob != null)
        {
            ob.Status = ObrigationStatus.Done;
            NotifyStateChanged();
        }
    }

    private void GenerateRecurrences(TaxObrigation source)
    {
        // Generate for next 12 months/occurrences based on recurrence type
        // This is a simplified logic. In a real app, you might want to ask user how many to generate
        // or have a background job.
        
        DateTime currentObjDate = source.DueDate;
        
        int occurrencesToGenerate = 12; // Default for monthly
        if (source.Recurrence == RecurrenceType.Weekly) occurrencesToGenerate = 52;
        if (source.Recurrence == RecurrenceType.Yearly) occurrencesToGenerate = 5;

        for (int i = 1; i < occurrencesToGenerate; i++)
        {
            DateTime nextDate = source.Recurrence switch
            {
                RecurrenceType.Daily => currentObjDate.AddDays(i),
                RecurrenceType.Weekly => currentObjDate.AddDays(i * 7),
                RecurrenceType.Fortnightly => currentObjDate.AddDays(i * 15),
                RecurrenceType.Monthly => currentObjDate.AddMonths(i),
                RecurrenceType.Semiannual => currentObjDate.AddMonths(i * 6),
                RecurrenceType.Yearly => currentObjDate.AddYears(i),
                _ => currentObjDate
            };
            
            var newOb = new TaxObrigation
            {
                Name = source.Name,
                DueDate = nextDate,
                Status = ObrigationStatus.ToDo,
                Value = source.Value,
                Recurrence = source.Recurrence,
                Description = source.Description,
                Observation = source.Observation
                // Do not copy AttachmentPath for new instances usually, or maybe? Let's not for now.
            };
            
            _obligations.Add(newOb);
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
