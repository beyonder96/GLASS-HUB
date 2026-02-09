using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GlassHub.Models;

namespace GlassHub.Services
{
    public static class XmlParser
    {
        public static async Task<Invoice?> ParseAsync(Stream xmlStream, string fileName)
        {
            try
            {
                var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, CancellationToken.None);
                if (doc.Root == null) return null;

                // Helper to get value ignoring namespaces usually, but XDocument handles namespaces.
                // Since NFe has namespaces (http://www.portalfiscal.inf.br/nfe), we should be careful.
                // Easy way: use local name check.
                
                string GetValue(XElement? parent, string tagName)
                {
                    return parent?.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == tagName)?.Value ?? string.Empty;
                }

                XElement? root = doc.Root; 
                
                // 1. Number
                var nNF = GetValue(root, "nNF");
                if (string.IsNullOrEmpty(nNF)) nNF = GetValue(root, "nCFe");
                if (string.IsNullOrEmpty(nNF)) nNF = GetValue(root, "nFat");
                if (string.IsNullOrEmpty(nNF)) nNF = "S/N";

                // 2. Issuer
                var emit = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "emit");
                var issuerName = GetValue(emit, "xNome");
                if (string.IsNullOrEmpty(issuerName)) issuerName = "Consumidor / Desconhecido";

                // 3. Date
                var dhEmi = GetValue(root, "dhEmi");
                if (string.IsNullOrEmpty(dhEmi)) dhEmi = GetValue(root, "dEmi"); // Old format YYYY-MM-DD
                
                DateTime issueDate = DateTime.Now;
                if (DateTime.TryParse(dhEmi, out var d)) issueDate = d;

                // 4. Value
                decimal totalValue = 0;
                var icmsTot = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "ICMSTot");
                var vNF = GetValue(icmsTot, "vNF");
                
                if (string.IsNullOrEmpty(vNF)) vNF = GetValue(root, "vLiq");
                if (string.IsNullOrEmpty(vNF)) vNF = GetValue(root, "vCFe");
                if (string.IsNullOrEmpty(vNF)) vNF = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "vNF")?.Value ?? "";

                if (decimal.TryParse(vNF, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                {
                    totalValue = v;
                }

                var invoiceId = $"{new string(nNF.Where(char.IsDigit).ToArray())}-{Guid.NewGuid()}";

                // 5. Installments
                var installments = new List<Installment>();
                var dups = root.Descendants().Where(e => e.Name.LocalName == "dup").ToList();

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
                                     Id = $"{invoiceId}-{nDup}",
                                     Number = nDup,
                                     DueDate = dueDate,
                                     Value = valDup,
                                     Status = status
                                 });
                             }
                        }
                    }
                }

                // Fallback
                if (!installments.Any())
                {
                     var status = issueDate.Date < DateTime.Today ? PaymentStatus.OVERDUE : PaymentStatus.PENDING;
                     installments.Add(new Installment
                     {
                         Id = $"{invoiceId}-single",
                         Number = "001",
                         DueDate = issueDate,
                         Value = totalValue,
                         Status = status
                     });
                }
                
                if (totalValue <= 0 && !installments.Any()) return null;

                return new Invoice
                {
                    Id = invoiceId,
                    Number = nNF,
                    IssuerName = issuerName,
                    IssueDate = issueDate,
                    TotalValue = totalValue,
                    FileName = fileName,
                    Installments = installments
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                return null;
            }
        }
    }
}
