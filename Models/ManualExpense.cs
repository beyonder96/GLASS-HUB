namespace GlassHub.Models;

/// <summary>
/// Representa uma despesa lançada manualmente no sistema.
/// </summary>
public class ManualExpense
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Descrição da despesa (ex: "Aluguel").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Categoria opcional (ex: "Fixo", "Variável").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Valor da despesa.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Data de vencimento.
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Status do pagamento.
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
}
