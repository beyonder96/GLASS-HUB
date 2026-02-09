namespace GlassHub.Models;

/// <summary>
/// Representa uma parcela de uma fatura.
/// </summary>
public class Installment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Número da parcela (ex: "01/12").
    /// </summary>
    public string Number { get; set; } = string.Empty;
    
    /// <summary>
    /// Data de Vencimento.
    /// </summary>
    public DateTime DueDate { get; set; }
    
    /// <summary>
    /// Valor da parcela.
    /// </summary>
    public decimal Value { get; set; }
    
    /// <summary>
    /// Status atual do pagamento.
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
}
