namespace GlassHub.Models;

public class InvoiceItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Ncm { get; set; } = string.Empty;
    public string Cfop { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    
    // Additional Values
    public decimal FreightValue { get; set; }
    public decimal InsuranceValue { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal OtherExpensesValue { get; set; }

    // Tax Details
    public string Cst { get; set; } = string.Empty; // Origin + CST (e.g. 000, 060)
    public string Csosn { get; set; } = string.Empty; // For Simples Nacional
    
    // ICMS
    public decimal IcmsBase { get; set; }
    public decimal IcmsRate { get; set; }
    public decimal IcmsValue { get; set; }
    
    // ICMS ST
    public decimal IcmsStBase { get; set; }
    public decimal IcmsStValue { get; set; }

    // IPI
    public decimal IpiBase { get; set; }
    public decimal IpiRate { get; set; }
    public decimal IpiValue { get; set; }

    // PIS
    public decimal PisBase { get; set; }
    public decimal PisRate { get; set; }
    public decimal PisValue { get; set; }

    // COFINS
    public decimal CofinsBase { get; set; }
    public decimal CofinsRate { get; set; }
    public decimal CofinsValue { get; set; }

    /// <summary>
    /// Custo efetivo unitário (considerando impostos não recuperáveis e despesas).
    /// </summary>
    public decimal EffectiveUnitCost { get; set; }
}
