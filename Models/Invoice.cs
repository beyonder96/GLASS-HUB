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
    /// Série da Nota Fiscal.
    /// </summary>
    public string Serie { get; set; } = string.Empty;

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
    /// Chave de Acesso da NF-e (44 dígitos).
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Natureza da Operação.
    /// </summary>
    public string NatureOfOperation { get; set; } = string.Empty;

    /// <summary>
    /// CNPJ do Destinatário (Tomador) para validação.
    /// </summary>
    public string RecipientTaxId { get; set; } = string.Empty;

    /// <summary>
    /// ID da empresa à qual esta nota pertence.
    /// </summary>
    public string? CompanyId { get; set; }

    /// <summary>
    /// Lista de parcelas duplicatas.
    /// </summary>
    public List<Installment> Installments { get; set; } = new();

    // --- New Fields for Detailed Analysis ---

    public string RecipientName { get; set; } = string.Empty;
    public string IssuerCnpj { get; set; } = string.Empty;
    public string RecipientCnpj { get; set; } = string.Empty;
    public string RecipientState { get; set; } = string.Empty;

    // Totals
    public decimal ProductsValue { get; set; }
    public decimal FreightValue { get; set; }
    public decimal InsuranceValue { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal OtherExpensesValue { get; set; }

    // Tax Totals
    public decimal IcmsBase { get; set; }
    public decimal IcmsValue { get; set; }
    public decimal IcmsStBase { get; set; }
    public decimal IcmsStValue { get; set; }
    public decimal IpiValue { get; set; }
    public decimal PisValue { get; set; }
    public decimal CofinsValue { get; set; }
    public decimal ImpostoAproximado { get; set; } // Lei da Transparência

    /// <summary>
    /// Finalidade da nota para o usuário (Revenda ou Consumo).
    /// </summary>
    public InvoicePurpose Purpose { get; set; } = InvoicePurpose.REVENDA;

    public List<InvoiceItem> Items { get; set; } = new();
}
