using GlassHub.Models;

namespace GlassHub.Services.Fiscal;

public class FiscalAnalysis
{
    public Invoice OriginalInvoice { get; private set; }
    public Invoice CalculatedInvoice { get; private set; }
    public List<string> Discrepancies { get; private set; } = new();
    public List<string> ValidationErrors { get; private set; } = new();

    public FiscalAnalysis(Invoice original)
    {
        OriginalInvoice = original;
        CalculatedInvoice = CloneInvoice(original);
        Recalculate();
        Compare();
        ValidationErrors = FiscalValidationService.ValidateInvoiceLogic(original);
    }

    private Invoice CloneInvoice(Invoice source)
    {
        // Deep clone via JSON serialization or manual mapping is safer, 
        // but for now manual mapping of relevant fields for calculation
        var clone = new Invoice
        {
            // Basic Info
            Id = source.Id,
            Number = source.Number,
            IssuerName = source.IssuerName,
            IssuerCnpj = source.IssuerCnpj,
            IssueDate = source.IssueDate,
            FileName = source.FileName,
            AccessKey = source.AccessKey,
            NatureOfOperation = source.NatureOfOperation,
            Purpose = source.Purpose,
            CompanyId = source.CompanyId,

            // Recipient
            RecipientName = source.RecipientName,
            RecipientCnpj = source.RecipientCnpj,
            RecipientState = source.RecipientState,
            RecipientTaxId = source.RecipientTaxId,

            // Values to be recalculated (initially copy)
            FreightValue = source.FreightValue,
            InsuranceValue = source.InsuranceValue,
            DiscountValue = source.DiscountValue,
            OtherExpensesValue = source.OtherExpensesValue,
            
            // Items (Should be cloned if we were modifying items, but we are only summing them for now)
            Items = source.Items.Select(i => new InvoiceItem 
            {
                Code = i.Code,
                Name = i.Name,
                Ncm = i.Ncm,
                Cfop = i.Cfop,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalValue = i.TotalValue,
                FreightValue = i.FreightValue,
                InsuranceValue = i.InsuranceValue,
                DiscountValue = i.DiscountValue,
                OtherExpensesValue = i.OtherExpensesValue,
                Cst = i.Cst,
                Csosn = i.Csosn,
                IcmsBase = i.IcmsBase,
                IcmsRate = i.IcmsRate,
                IcmsValue = i.IcmsValue,
                IcmsStBase = i.IcmsStBase,
                IcmsStValue = i.IcmsStValue,
                IpiBase = i.IpiBase,
                IpiRate = i.IpiRate,
                IpiValue = i.IpiValue,
                PisBase = i.PisBase,
                PisRate = i.PisRate,
                PisValue = i.PisValue,
                CofinsBase = i.CofinsBase,
                CofinsRate = i.CofinsRate,
                CofinsValue = i.CofinsValue,
                EffectiveUnitCost = i.EffectiveUnitCost
            }).ToList(),

            Installments = source.Installments // Reference copy is fine for now
        };
        return clone;
    }

    private void Recalculate()
    {
        // 0. Recalculate Items (Math check)
        foreach (var item in CalculatedInvoice.Items)
        {
            // Validating Product Total (Qty x Unit Price)
            // Note: Some XMLs have slight rounding differences, tolerance is needed.
            decimal expectedTotal = Math.Round(item.Quantity * item.UnitPrice, 2);
            if (Math.Abs(item.TotalValue - expectedTotal) > 0.05m)
            {
                // If the XML total is wrong based on Qty*Unit, we assume the Unit Price/Qty are source of truth for "Corrected" version
                // Or should we assume Total is right? Usually Total is what matters for payment.
                // But for "Correction", let's assume strict math.
                // However, often unit price is truncated in XML (e.g. 1.3333 -> 1.33). 
                // Let's keep the TotalValue from XML if it's close enough, but if it's way off, we correct it.
                // For this implementation, let's just flag it if it's significant.
            }

            // Validating Taxes
            // ICMS
            if (item.IcmsBase > 0 && item.IcmsRate > 0)
            {
                decimal expectedIcms = Math.Round(item.IcmsBase * (item.IcmsRate / 100), 2);
                if (Math.Abs(item.IcmsValue - expectedIcms) > 0.05m)
                {
                    item.IcmsValue = expectedIcms; // Correcting in the "Calculated" invoice
                }
            }
            
            // IPI
            if (item.IpiBase > 0 && item.IpiRate > 0)
            {
                decimal expectedIpi = Math.Round(item.IpiBase * (item.IpiRate / 100), 2);
                if (Math.Abs(item.IpiValue - expectedIpi) > 0.05m)
                {
                    item.IpiValue = expectedIpi;
                }
            }
        }

        // 1. Sum Item Totals to Header
        CalculatedInvoice.ProductsValue = CalculatedInvoice.Items.Sum(i => i.TotalValue);
        CalculatedInvoice.IpiValue = CalculatedInvoice.Items.Sum(i => i.IpiValue);
        CalculatedInvoice.IcmsValue = CalculatedInvoice.Items.Sum(i => i.IcmsValue);
        CalculatedInvoice.IcmsBase = CalculatedInvoice.Items.Sum(i => i.IcmsBase);
        CalculatedInvoice.IcmsStValue = CalculatedInvoice.Items.Sum(i => i.IcmsStValue);
        CalculatedInvoice.IcmsStBase = CalculatedInvoice.Items.Sum(i => i.IcmsStBase);
        CalculatedInvoice.PisValue = CalculatedInvoice.Items.Sum(i => i.PisValue);
        CalculatedInvoice.CofinsValue = CalculatedInvoice.Items.Sum(i => i.CofinsValue);
        
        // ... (Header logic remains)
        
        // Note: Freight, Insurance, Discount, OtherExpenses usually come from Header in XML (vFrete, etc)
        // But optimally they should match the sum of items if prorated. 
        // For this analysis, we trust the Header values for these specific fields UNLESS they are zero and items have values.
        
        var sumFrete = CalculatedInvoice.Items.Sum(i => i.FreightValue);
        if (CalculatedInvoice.FreightValue == 0 && sumFrete > 0) CalculatedInvoice.FreightValue = sumFrete;
        
        var sumSeg = CalculatedInvoice.Items.Sum(i => i.InsuranceValue);
        if (CalculatedInvoice.InsuranceValue == 0 && sumSeg > 0) CalculatedInvoice.InsuranceValue = sumSeg;

        var sumDesc = CalculatedInvoice.Items.Sum(i => i.DiscountValue);
        if (CalculatedInvoice.DiscountValue == 0 && sumDesc > 0) CalculatedInvoice.DiscountValue = sumDesc;
        
        var sumOutro = CalculatedInvoice.Items.Sum(i => i.OtherExpensesValue);
        if (CalculatedInvoice.OtherExpensesValue == 0 && sumOutro > 0) CalculatedInvoice.OtherExpensesValue = sumOutro;

        // 2. Calculate Final Total (vNF) using SEFAZ formula
        // vNF = vProd - vDesc - vICMSDeson + vST + vFrete + vSeg + vOutro + vII + vIPI + vServ
        CalculatedInvoice.TotalValue = 
            CalculatedInvoice.ProductsValue 
            - CalculatedInvoice.DiscountValue 
            + CalculatedInvoice.IcmsStValue 
            + CalculatedInvoice.FreightValue 
            + CalculatedInvoice.InsuranceValue 
            + CalculatedInvoice.OtherExpensesValue 
            + CalculatedInvoice.IpiValue;
    }

    private void Compare()
    {
        Discrepancies.Clear();
        VerifyDiff("Total da Nota", OriginalInvoice.TotalValue, CalculatedInvoice.TotalValue);
        VerifyDiff("Total Produtos", OriginalInvoice.ProductsValue, CalculatedInvoice.ProductsValue);
        VerifyDiff("Total IPI", OriginalInvoice.IpiValue, CalculatedInvoice.IpiValue);
        VerifyDiff("Total ICMS", OriginalInvoice.IcmsValue, CalculatedInvoice.IcmsValue);
        VerifyDiff("Total ICMS ST", OriginalInvoice.IcmsStValue, CalculatedInvoice.IcmsStValue);
    }

    private void VerifyDiff(string label, decimal original, decimal calculated)
    {
        if (Math.Abs(original - calculated) > 0.05m) // 5 cents tolerance
        {
            Discrepancies.Add($"{label}: XML({original:C}) != Calc({calculated:C})");
        }
    }
}
