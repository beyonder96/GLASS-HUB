using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GlassHub.Models;
using GlassHub.Services.Fiscal;

namespace GlassHub.Services
{
    public class ParseResult
    {
        public Invoice? Invoice { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSkipped { get; set; }
        public bool MissingDuplicates { get; set; }
        public string? RecipientTaxId { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public bool IsValid => !ValidationErrors.Any() && string.IsNullOrEmpty(ErrorMessage);
    }

    public static class XmlParser
    {
        private static readonly string[] RejectedKeywords = { "GARANTIA", "DEVOLUCAO", "CONSERTO", "REMESSA" };

        public static async Task<ParseResult> ParseAsync(Stream xmlStream, string fileName, InvoicePurpose purpose = InvoicePurpose.REVENDA)
        {
            try
            {
                var settings = new System.Xml.XmlReaderSettings 
                { 
                    DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                    XmlResolver = null,
                    Async = true
                };

                using var reader = System.Xml.XmlReader.Create(xmlStream, settings);
                var doc = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
                if (doc.Root == null) return new ParseResult { ErrorMessage = "XML inválido ou vazio." };

                var validationErrors = FiscalValidationService.Validate(doc, purpose);

                XElement root = doc.Root;

                string GetValue(XElement? parent, string tagName)
                {
                    return parent?.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == tagName)?.Value ?? string.Empty;
                }
                
                decimal GetDecimal(XElement? parent, string tagName)
                {
                    var val = GetValue(parent, tagName);
                    if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
                    return 0;
                }

                // 1. Nature of Operation (Filtering)
                var natOp = GetValue(root, "natOp").ToUpper();
                bool isSkipped = false;
                if (RejectedKeywords.Any(k => natOp.Contains(k)))
                {
                    isSkipped = true;
                }

                // 2. Access Key
                var chNFe = GetValue(root, "chNFe");
                if (string.IsNullOrEmpty(chNFe))
                {
                    chNFe = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe")?.Attribute("Id")?.Value;
                    if (chNFe?.StartsWith("NFe") == true) chNFe = chNFe.Substring(3);
                }

                // 3. Number & Series
                var nNF = GetValue(root, "nNF");
                var serie = GetValue(root, "serie");
                if (string.IsNullOrEmpty(nNF)) nNF = GetValue(root, "nCFe");
                if (string.IsNullOrEmpty(nNF)) nNF = GetValue(root, "nFat");
                if (string.IsNullOrEmpty(nNF)) nNF = "S/N";

                // 4. Issuer
                var emit = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "emit");
                var issuerName = GetValue(emit, "xNome");
                var issuerCnpj = GetValue(emit, "CNPJ");
                if (string.IsNullOrEmpty(issuerCnpj)) issuerCnpj = GetValue(emit, "CPF");
                if (string.IsNullOrEmpty(issuerName)) issuerName = "Consumidor / Desconhecido";

                // 5. Recipient
                var dest = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "dest");
                var destName = GetValue(dest, "xNome");
                var destCnpj = GetValue(dest, "CNPJ");
                if (string.IsNullOrEmpty(destCnpj)) destCnpj = GetValue(dest, "CPF");
                var destUf = GetValue(dest?.Descendants().FirstOrDefault(e => e.Name.LocalName == "enderDest"), "UF");

                // 6. Date
                var dhEmi = GetValue(root, "dhEmi");
                if (string.IsNullOrEmpty(dhEmi)) dhEmi = GetValue(root, "dEmi");
                
                DateTime issueDate = DateTime.Now;
                if (DateTime.TryParse(dhEmi, out var d)) issueDate = d;

                // 7. Totals (ICMSTot)
                var icmsTot = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "ICMSTot");
                
                // 8. Items
                var items = new List<InvoiceItem>();
                var dets = root.Descendants().Where(e => e.Name.LocalName == "det");
                
                foreach (var det in dets)
                {
                    var prod = det.Descendants().FirstOrDefault(e => e.Name.LocalName == "prod");
                    var imposto = det.Descendants().FirstOrDefault(e => e.Name.LocalName == "imposto");
                    
                    if (prod != null)
                    {
                        var item = new InvoiceItem
                        {
                            Code = GetValue(prod, "cProd"),
                            Name = GetValue(prod, "xProd"),
                            Ncm = GetValue(prod, "NCM"),
                            Cfop = GetValue(prod, "CFOP"),
                            Unit = GetValue(prod, "uCom"),
                            Quantity = GetDecimal(prod, "qCom"),
                            UnitPrice = GetDecimal(prod, "vUnCom"),
                            TotalValue = GetDecimal(prod, "vProd"),
                            FreightValue = GetDecimal(prod, "vFrete"),
                            InsuranceValue = GetDecimal(prod, "vSeg"),
                            DiscountValue = GetDecimal(prod, "vDesc"),
                            OtherExpensesValue = GetDecimal(prod, "vOutro")
                        };

                        // Taxes
                        if (imposto != null)
                        {
                            // ICMS
                            var icms = imposto.Descendants().FirstOrDefault(e => e.Name.LocalName.StartsWith("ICMS"));
                            if (icms != null)
                            {
                                var orig = GetValue(icms, "orig");
                                var cst = GetValue(icms, "CST");
                                var csosn = GetValue(icms, "CSOSN");

                                item.Cst = !string.IsNullOrEmpty(cst) ? (orig + cst) : "";
                                item.Csosn = !string.IsNullOrEmpty(csosn) ? (orig + csosn) : "";
                                
                                item.IcmsBase = GetDecimal(icms, "vBC");
                                item.IcmsRate = GetDecimal(icms, "pICMS");
                                item.IcmsValue = GetDecimal(icms, "vICMS");
                                item.IcmsStBase = GetDecimal(icms, "vBCST");
                                item.IcmsStValue = GetDecimal(icms, "vICMSST");
                            }

                            // IPI
                            var ipi = imposto.Descendants().FirstOrDefault(e => e.Name.LocalName == "IPI");
                            if (ipi != null)
                            {
                                var ipiTrib = ipi.Descendants().FirstOrDefault(e => e.Name.LocalName == "IPITrib");
                                if (ipiTrib != null)
                                {
                                    item.IpiBase = GetDecimal(ipiTrib, "vBC");
                                    item.IpiRate = GetDecimal(ipiTrib, "pIPI");
                                    item.IpiValue = GetDecimal(ipiTrib, "vIPI");
                                }
                            }

                            // PIS
                            var pis = imposto.Descendants().FirstOrDefault(e => e.Name.LocalName == "PIS");
                            if (pis != null)
                            {
                                var pisAliq = pis.Descendants().FirstOrDefault(e => e.Name.LocalName == "PISAliq") ?? pis.Descendants().FirstOrDefault(e => e.Name.LocalName == "PISQtde"); 
                                if (pisAliq != null)
                                {
                                    item.PisBase = GetDecimal(pisAliq, "vBC");
                                    item.PisRate = GetDecimal(pisAliq, "pPIS");
                                    item.PisValue = GetDecimal(pisAliq, "vPIS");
                                }
                            }

                            // COFINS
                            var cofins = imposto.Descendants().FirstOrDefault(e => e.Name.LocalName == "COFINS");
                            if (cofins != null)
                            {
                                var cofinsAliq = cofins.Descendants().FirstOrDefault(e => e.Name.LocalName == "COFINSAliq") ?? cofins.Descendants().FirstOrDefault(e => e.Name.LocalName == "COFINSQtde");
                                if (cofinsAliq != null)
                                {
                                    item.CofinsBase = GetDecimal(cofinsAliq, "vBC");
                                    item.CofinsRate = GetDecimal(cofinsAliq, "pCOFINS");
                                    item.CofinsValue = GetDecimal(cofinsAliq, "vCOFINS");
                                }
                            }
                        }
                        
                        // Calculate Effective Unit Cost (Consumption rules)
                        if (item.Quantity > 0)
                        {
                            // In consumption, IPI and ST are costs. 
                            // Formula: (vProd + vIPI + vICMSST + vFrete + vSeg + vOutro - vDesc) / qCom
                            decimal totalCost = item.TotalValue + item.IpiValue + item.IcmsStValue + item.FreightValue + item.InsuranceValue + item.OtherExpensesValue - item.DiscountValue;
                            item.EffectiveUnitCost = totalCost / item.Quantity;
                        }
                        
                        items.Add(item);
                    }
                }

                // 9. Invoice Object
                var invoice = new Invoice
                {
                    Id = !string.IsNullOrEmpty(chNFe) ? chNFe : $"{new string(nNF.Where(char.IsDigit).ToArray())}-{Guid.NewGuid()}",
                    Number = nNF,
                    Serie = serie,
                    IssuerName = issuerName,
                    IssuerCnpj = issuerCnpj,
                    IssueDate = issueDate,
                    FileName = fileName,
                    AccessKey = chNFe ?? "",
                    NatureOfOperation = natOp,
                    Purpose = purpose,
                    
                    // Recipient
                    RecipientName = destName,
                    RecipientCnpj = destCnpj,
                    RecipientTaxId = destCnpj,
                    RecipientState = destUf,
                    
                    // Totals (Header)
                    TotalValue = GetDecimal(icmsTot, "vNF"),
                    ProductsValue = GetDecimal(icmsTot, "vProd"),
                    FreightValue = GetDecimal(icmsTot, "vFrete"),
                    InsuranceValue = GetDecimal(icmsTot, "vSeg"),
                    DiscountValue = GetDecimal(icmsTot, "vDesc"),
                    OtherExpensesValue = GetDecimal(icmsTot, "vOutro"),
                    IpiValue = GetDecimal(icmsTot, "vIPI"),
                    PisValue = GetDecimal(icmsTot, "vPIS"),
                    CofinsValue = GetDecimal(icmsTot, "vCOFINS"),
                    IcmsBase = GetDecimal(icmsTot, "vBC"),
                    IcmsValue = GetDecimal(icmsTot, "vICMS"),
                    IcmsStBase = GetDecimal(icmsTot, "vBCST"),
                    IcmsStValue = GetDecimal(icmsTot, "vST"),
                    ImpostoAproximado = GetDecimal(icmsTot, "vTotTrib"),
                    
                    Items = items
                };

                // Fallback for Total Value if not found in ICMSTot
                if (invoice.TotalValue == 0)
                {
                     var vNF = GetValue(root, "vLiq"); // CFe
                     if (string.IsNullOrEmpty(vNF)) vNF = GetValue(root, "vCFe");
                     if (decimal.TryParse(vNF, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) invoice.TotalValue = v;
                }

                // 10. Installments
                var installments = new List<Installment>();
                var dups = root.Descendants().Where(e => e.Name.LocalName == "dup").ToList();
                bool missingDuplicates = false;

                if (dups.Any())
                {
                    foreach (var dup in dups)
                    {
                        var nDup = GetValue(dup, "nDup");
                        var dVenc = GetValue(dup, "dVenc");
                        var vDup = GetValue(dup, "vDup");

                        if (!string.IsNullOrEmpty(dVenc) && decimal.TryParse(vDup, NumberStyles.Any, CultureInfo.InvariantCulture, out var valDup))
                        {
                             if (DateTime.TryParse(dVenc, out var dueDate))
                             {
                                 var status = dueDate.Date < DateTime.Today ? PaymentStatus.OVERDUE : PaymentStatus.PENDING;
                                 installments.Add(new Installment
                                 {
                                     Id = $"{invoice.Id}-{nDup}",
                                     Number = nDup,
                                     DueDate = dueDate,
                                     Value = valDup,
                                     Status = status
                                 });
                             }
                        }
                    }
                }

                if (!installments.Any())
                {
                    missingDuplicates = true;
                    var status = issueDate.Date < DateTime.Today ? PaymentStatus.OVERDUE : PaymentStatus.PENDING;
                    installments.Add(new Installment
                    {
                        Id = $"{invoice.Id}-single",
                        Number = "001",
                        DueDate = issueDate,
                        Value = invoice.TotalValue,
                        Status = status
                    });
                }
                
                invoice.Installments = installments;

                if (invoice.TotalValue <= 0 && !installments.Any()) 
                    return new ParseResult { ErrorMessage = "Nota sem valor e sem parcelas." };

                return new ParseResult
                {
                    Invoice = invoice,
                    IsSkipped = isSkipped,
                    ErrorMessage = isSkipped ? $"Nota de {natOp} filtrada (Garantia/Devolução/Etc)." : null,
                    MissingDuplicates = missingDuplicates,
                    RecipientTaxId = destCnpj,
                    ValidationErrors = validationErrors
                };
            }
            catch (Exception ex)
            {
                return new ParseResult { ErrorMessage = $"Erro ao processar XML: {ex.Message}" };
            }
        }
    }
}
