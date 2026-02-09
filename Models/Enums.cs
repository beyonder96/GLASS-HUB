namespace GlassHub.Models;

/// <summary>
/// Define o status de pagamento de uma despesa ou fatura.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Pagamento pendente / A vencer.
    /// </summary>
    PENDING,

    /// <summary>
    /// Pagamento atrasado.
    /// </summary>
    OVERDUE,

    /// <summary>
    /// Pagamento realizado.
    /// </summary>
    PAID
}
