namespace GlassHub.Models;

/// <summary>
/// Representa uma fatura ou nota fiscal importada (XML).
/// </summary>
public class Invoice
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Número da Nota Fiscal.
    /// </summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// Nome do Emitente/Fornecedor.
    /// </summary>
    public string IssuerName { get; set; } = string.Empty;

    /// <summary>
    /// Data de Emissão da Nota.
    /// </summary>
    public DateTime IssueDate { get; set; }

    /// <summary>
    /// Valor Total da Nota.
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Nome do arquivo original importado.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Lista de parcelas duplicatas.
    /// </summary>
    public List<Installment> Installments { get; set; } = new();
}
