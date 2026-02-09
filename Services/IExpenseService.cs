using System;
using System.Collections.Generic;
using GlassHub.Models;

namespace GlassHub.Services
{
    public interface IExpenseService
    {
        event Action? OnChange;
        List<Invoice> Invoices { get; }
        List<ManualExpense> ManualExpenses { get; }
        
        void AddInvoice(Invoice invoice);
        void AddInvoices(IEnumerable<Invoice> invoices);
        void AddManualExpense(ManualExpense expense);
        void RemoveInvoice(string id);
        void RemoveManualExpense(string id);
        void UpdateManualExpense(ManualExpense expense);
        void ToggleManualExpenseStatus(string id);
        void ToggleInstallmentStatus(string invoiceId, string installmentId);
        
        // Dashboard Stats
        decimal GetTotalPayable();
        decimal GetTotalOverdue();
        decimal GetTotalPaid();
        int GetUpcomingCount(int days);
    }
}
