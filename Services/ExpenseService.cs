using GlassHub.Models;

namespace GlassHub.Services;

/// <summary>
/// Serviço responsável pelo gerenciamento de despesas (Faturas e Manuais).
/// Mantém o estado da aplicação e notifica componentes sobre mudanças.
/// </summary>
public class ExpenseService : IExpenseService
{
    // Evento para notificar componentes de alterações no estado
    public event Action? OnChange;
    
    // Armazenamento em memória (Simulando Banco de Dados)
    public List<Invoice> Invoices { get; private set; } = new();
    public List<ManualExpense> ManualExpenses { get; private set; } = new();

    #region Create Operations

    /// <summary>
    /// Adiciona uma nova fatura (XML importado).
    /// </summary>
    public void AddInvoice(Invoice invoice)
    {
        Invoices.Add(invoice);
        NotifyStateChanged();
    }

    /// <summary>
    /// Adiciona múltiplas faturas de uma vez.
    /// </summary>
    public void AddInvoices(IEnumerable<Invoice> invoices)
    {
        Invoices.AddRange(invoices);
        NotifyStateChanged();
    }

    /// <summary>
    /// Adiciona uma despesa manual.
    /// </summary>
    public void AddManualExpense(ManualExpense expense)
    {
        ManualExpenses.Add(expense);
        NotifyStateChanged();
    }

    #endregion

    #region Delete Operations

    public void RemoveInvoice(string id)
    {
        var invoice = Invoices.FirstOrDefault(i => i.Id == id);
        if (invoice != null)
        {
            Invoices.Remove(invoice);
            NotifyStateChanged();
        }
    }

    public void RemoveManualExpense(string id)
    {
        var expense = ManualExpenses.FirstOrDefault(e => e.Id == id);
        if (expense != null)
        {
            ManualExpenses.Remove(expense);
            NotifyStateChanged();
        }
    }

    #endregion

    #region Update Operations

    public void UpdateManualExpense(ManualExpense expense)
    {
         var index = ManualExpenses.FindIndex(e => e.Id == expense.Id);
         if (index != -1)
         {
             ManualExpenses[index] = expense;
             NotifyStateChanged();
         }
    }

    public void ToggleManualExpenseStatus(string id)
    {
        var expense = ManualExpenses.FirstOrDefault(e => e.Id == id);
        if (expense != null)
        {
            expense.Status = expense.Status == PaymentStatus.PAID ? PaymentStatus.PENDING : PaymentStatus.PAID;
            NotifyStateChanged();
        }
    }

    public void ToggleInstallmentStatus(string invoiceId, string installmentId)
    {
        var invoice = Invoices.FirstOrDefault(i => i.Id == invoiceId);
        if (invoice != null)
        {
            var installment = invoice.Installments.FirstOrDefault(i => i.Id == installmentId);
            if (installment != null)
            {
                installment.Status = installment.Status == PaymentStatus.PAID ? PaymentStatus.PENDING : PaymentStatus.PAID;
                NotifyStateChanged();
            }
        }
    }

    #endregion

    #region Calculations & Analyzers

    /// <summary>
    /// Calcula o total a pagar (apenas Pendente + Vencido).
    /// </summary>
    public decimal GetTotalPayable()
    {
        var invoiceTotal = Invoices.SelectMany(i => i.Installments)
            .Where(inst => inst.Status != PaymentStatus.PAID)
            .Sum(inst => inst.Value);
            
        var manualTotal = ManualExpenses
            .Where(e => e.Status != PaymentStatus.PAID)
            .Sum(e => e.Value);
            
        return invoiceTotal + manualTotal;
    }

    /// <summary>
    /// Calcula o total atrasado.
    /// </summary>
    public decimal GetTotalOverdue()
    {
        var today = DateTime.Today;
        
        var invoiceOverdue = Invoices.SelectMany(i => i.Installments)
            .Where(inst => inst.Status == PaymentStatus.OVERDUE || (inst.Status == PaymentStatus.PENDING && inst.DueDate < today))
            .Sum(inst => inst.Value);
            
        var manualOverdue = ManualExpenses
            .Where(e => e.Status == PaymentStatus.OVERDUE || (e.Status == PaymentStatus.PENDING && e.DueDate < today))
            .Sum(e => e.Value);
            
        return invoiceOverdue + manualOverdue;
    }

    /// <summary>
    /// Calcula o total já pago.
    /// </summary>
    public decimal GetTotalPaid()
    {
         var invoicePaid = Invoices.SelectMany(i => i.Installments)
            .Where(inst => inst.Status == PaymentStatus.PAID)
            .Sum(inst => inst.Value);
            
        var manualPaid = ManualExpenses
            .Where(e => e.Status == PaymentStatus.PAID)
            .Sum(e => e.Value);
            
        return invoicePaid + manualPaid;
    }
    
    /// <summary>
    /// Conta quantos pagamentos vencem nos próximos N dias.
    /// </summary>
    public int GetUpcomingCount(int days)
    {
        var today = DateTime.Today;
        var limit = today.AddDays(days);
        
        var invoiceCount = Invoices.SelectMany(i => i.Installments)
            .Count(inst => inst.Status == PaymentStatus.PENDING && inst.DueDate >= today && inst.DueDate <= limit);
            
        var manualCount = ManualExpenses
            .Count(e => e.Status == PaymentStatus.PENDING && e.DueDate >= today && e.DueDate <= limit);
            
        return invoiceCount + manualCount;
    }

    #endregion

    private void NotifyStateChanged() => OnChange?.Invoke();
}
