using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using NFe.Classes;
using NFe.Utils.NFe;
using DFe.Utils;
using GlassHub.Models;
using GlassHub.Models.Fiscal;

namespace GlassHub.Services.Fiscal
{
    public static class FiscalValidationService
    {
        public static List<string> Validate(XDocument doc, InvoicePurpose purpose = InvoicePurpose.REVENDA)
        {
            var errors = new List<string>();
            var root = doc.Root;
            if (root == null) return new List<string> { "XML vazio." };

            try 
            {
                var xmlString = doc.ToString();
                var nfeProc = FuncoesXml.XmlStringParaClasse<nfeProc>(xmlString);
                
                if (nfeProc != null)
                {
                    ValidateWithZeus(nfeProc, errors);
                }
            }
            catch { }

            // 1. Integridade e Chave de Acesso
            ValidateAccessKey(root, errors);
            ValidateSignatureAndDigest(root, errors);
            
            // 2. Validações Totais (vNF = vProd - vDesc + vST + vFrete + vSeg + vOutro)
            ValidateTotalsDeep(root, errors);

            // 3. Validações por Item e Finalidade
            ValidateItemsDeep(root, errors, purpose);

            // 4. Datas e Prazos
            ValidateDates(root, errors);

            return errors.Distinct().ToList();
        }

        public static List<string> ValidateInvoiceLogic(Invoice invoice)
        {
            var errors = new List<string>();
            
            // Math Validation: Items vs Header
            decimal sumItems = invoice.Items.Sum(i => i.TotalValue);
            if (Math.Abs(sumItems - invoice.ProductsValue) > 0.05m)
            {
                errors.Add($"Divergência no Total de Produtos: Soma dos itens ({sumItems:C}) difere do total do cabeçalho ({invoice.ProductsValue:C}).");
            }

            // SEFAZ vNF Formula: vProd - vDesc + vIPI + vST + vFrete + vSeg + vOutro
            decimal calculatedVnf = invoice.ProductsValue - invoice.DiscountValue + invoice.IpiValue + invoice.IcmsStValue + invoice.FreightValue + invoice.InsuranceValue + invoice.OtherExpensesValue;
            if (Math.Abs(calculatedVnf - invoice.TotalValue) > 0.05m)
            {
                 errors.Add($"Divergência no Total da Nota (vNF): O valor {invoice.TotalValue:C} difere do cálculo SEFAZ {calculatedVnf:C} (Produtos - Desc + IPI + ST + Frete + Seg + Outros).");
            }

            // Tax Validation
            decimal sumIcms = invoice.Items.Sum(i => i.IcmsValue);
            if (Math.Abs(sumIcms - invoice.IcmsValue) > 0.05m)
            {
                errors.Add($"Divergência no ICMS: Soma dos itens ({sumIcms:C}) difere do total do cabeçalho ({invoice.IcmsValue:C}).");
            }
            
            decimal sumIpi = invoice.Items.Sum(i => i.IpiValue);
            if (Math.Abs(sumIpi - invoice.IpiValue) > 0.05m)
            {
                errors.Add($"Divergência no IPI: Soma dos itens ({sumIpi:C}) difere do total do cabeçalho ({invoice.IpiValue:C}).");
            }

            // Purpose Specific Rules
            if (invoice.Purpose == InvoicePurpose.CONSUMO)
            {
                if (invoice.IcmsValue > 0)
                {
                    errors.Add("Atenção (Consumo): O ICMS destacado é um 'custo escondido' e geralmente não gera crédito para itens de consumo.");
                }

                if (invoice.IpiValue > 0)
                {
                    errors.Add($"Atenção (Consumo): O IPI ({invoice.IpiValue:C}) integra o custo da mercadoria e a base de cálculo do ICMS na entrada.");
                }

                if (invoice.IcmsStValue > 0)
                {
                    errors.Add($"Alerta ST (Consumo): ICMS-ST de {invoice.IcmsStValue:C} detectado. O fornecedor já reteve o imposto, o que torna o item mais caro.");
                }

                // DIFAL Check (Interstate Consumption)
                if (!string.IsNullOrEmpty(invoice.RecipientState) && !string.IsNullOrEmpty(invoice.AccessKey) && invoice.AccessKey.Length >= 2)
                {
                    string emitUfCode = invoice.AccessKey.Substring(0, 2);
                    // Simplificação: comparando código UF da chave com sigla do destinatário
                    // Em um sistema real, teríamos um mapa de códigos UF (Ex: 35 = SP, 31 = MG)
                    // Por agora, vamos usar uma lógica baseada em CFOP já presente no ValidateItemsDeep, 
                    // mas aqui alertamos especificamente sobre o cálculo do DIFAL "por fora".
                    
                    if (invoice.Items.Any(i => i.Cfop.StartsWith("6")))
                    {
                        errors.Add("DIFAL de Entrada: Operação interestadual p/ consumo detectada. Você deve calcular e pagar o diferencial de alíquota ao seu estado.");
                    }
                }
            }

            return errors;
        }

        private static void ValidateAccessKey(XElement root, List<string> errors)
        {
            var infNFe = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe");
            var id = infNFe?.Attribute("Id")?.Value ?? "";
            var chNFe = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "chNFe")?.Value;

            var key = chNFe ?? (id.StartsWith("NFe") ? id.Substring(3) : id);

            if (string.IsNullOrEmpty(key))
            {
                errors.Add("Chave de acesso não encontrada.");
                return;
            }

            if (key.Length != 44)
            {
                errors.Add($"Chave de acesso inválida (tamanho {key.Length}, esperado 44).");
                return;
            }

            // Validação de composição (UF + Data + CNPJ + Mod + Serie + Num + Cod + DV)
            // Extração para log ou verificação futura (simplificado aqui por ser string)
            var uf = key.Substring(0, 2);
            var cnpj = key.Substring(6, 14);
            var mod = key.Substring(20, 2);
            
            if (mod != "55" && mod != "65")
            {
                errors.Add($"Modelo de nota na chave ({mod}) é desconhecido (esperado 55 ou 65).");
            }
        }

        private static void ValidateTotalsDeep(XElement root, List<string> errors)
        {
            var icmsTot = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "ICMSTot");
            if (icmsTot == null) return;

            decimal vNF = GetDecimal(icmsTot, "vNF");
            decimal vProd = GetDecimal(icmsTot, "vProd");
            decimal vDesc = GetDecimal(icmsTot, "vDesc");
            decimal vIPI = GetDecimal(icmsTot, "vIPI");
            decimal vST = GetDecimal(icmsTot, "vST");
            decimal vFrete = GetDecimal(icmsTot, "vFrete");
            decimal vSeg = GetDecimal(icmsTot, "vSeg");
            decimal vOutro = GetDecimal(icmsTot, "vOutro");

            // Fórmula SEFAZ: vNF = vProd - vDesc + vIPI + vST + vFrete + vSeg + vOutro
            decimal calculatedVnf = vProd - vDesc + vIPI + vST + vFrete + vSeg + vOutro;
            
            if (Math.Abs(calculatedVnf - vNF) > 0.05m)
            {
                errors.Add($"Divergência nos Totais da Nota: vNF ({vNF:C}) != Cálculo SEFAZ ({calculatedVnf:C}).");
            }
        }

        private static void ValidateItemsDeep(XElement root, List<string> errors, InvoicePurpose purpose)
        {
            var items = root.Descendants().Where(e => e.Name.LocalName == "det").ToList();
            var dest = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "dest");
            var destUF = dest?.Element(dest.Name.Namespace + "enderDest")?.Element(dest.Name.Namespace + "UF")?.Value;
            var emit = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "emit");
            var emitUF = emit?.Element(emit.Name.Namespace + "enderEmit")?.Element(emit.Name.Namespace + "UF")?.Value;

            foreach (var item in items)
            {
                var nItem = item.Attribute("nItem")?.Value;
                var prod = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "prod");
                var cfop = prod?.Element(prod.Name.Namespace + "CFOP")?.Value;
                var ncm = prod?.Element(prod.Name.Namespace + "NCM")?.Value;

                // CFOP vs UF
                if (!string.IsNullOrEmpty(cfop) && !string.IsNullOrEmpty(emitUF) && !string.IsNullOrEmpty(destUF))
                {
                    bool isInternal = emitUF == destUF;
                    if (isInternal && !cfop.StartsWith("5"))
                        errors.Add($"Item {nItem}: CFOP {cfop} incompatível com operação interna (UF {emitUF}).");
                    else if (!isInternal && !cfop.StartsWith("6") && destUF != "EX")
                        errors.Add($"Item {nItem}: CFOP {cfop} incompatível com operação interestadual.");
                }

                // CFOP vs Purpose
                if (purpose == InvoicePurpose.REVENDA)
                {
                    if (cfop == "1556" || cfop == "2556")
                        errors.Add($"Item {nItem}: CFOP {cfop} indica Consumo, mas a finalidade é Revenda.");
                }

                if (string.IsNullOrEmpty(ncm) || ncm.Length != 8)
                {
                    errors.Add($"Item {nItem}: NCM {ncm} inválido (esperado 8 dígitos).");
                }
            }
        }

        private static void ValidateDates(XElement root, List<string> errors)
        {
            var ide = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "ide");
            if (ide == null) return;

            var dhEmiStr = ide.Element(ide.Name.Namespace + "dhEmi")?.Value ?? ide.Element(ide.Name.Namespace + "dEmi")?.Value;
            var dhSaiEntStr = ide.Element(ide.Name.Namespace + "dhSaiEnt")?.Value ?? ide.Element(ide.Name.Namespace + "dSaiEnt")?.Value;

            if (DateTime.TryParse(dhEmiStr, out DateTime dhEmi) && DateTime.TryParse(dhSaiEntStr, out DateTime dhSaiEnt))
            {
                if (dhSaiEnt < dhEmi)
                {
                    errors.Add("Atenção: Data de saída/entrada anterior à data de emissão.");
                }
            }
        }

        public static FiscalDocument ValidateXml(string xmlContent, string fileName, InvoicePurpose purpose = InvoicePurpose.REVENDA)
        {
            var doc = new FiscalDocument
            {
                XmlContent = xmlContent,
                FileName = fileName,
                Status = FiscalDocumentStatus.Valid
            };

            try
            {
                var xml = XDocument.Parse(xmlContent);
                
                // Basic Check: Root element
                if (xml.Root?.Name.LocalName != "nfeProc" && xml.Root?.Name.LocalName != "NFe")
                {
                    doc.Status = FiscalDocumentStatus.Invalid;
                    doc.ValidationErrors.Add("XML inválido: Elemento raiz deve ser 'nfeProc' ou 'NFe'.");
                    return doc;
                }

                // Reuse the robust Validate logic and append errors
                var additionalErrors = Validate(xml, purpose);
                if (additionalErrors.Any())
                {
                    doc.ValidationErrors.AddRange(additionalErrors);
                     doc.Status = FiscalDocumentStatus.Warning; 
                }

                // Extract Access Key
                var infNFe = xml.Descendants().FirstOrDefault(x => x.Name.LocalName == "infNFe");
                if (infNFe?.Attribute("Id") != null)
                {
                    doc.AccessKey = infNFe.Attribute("Id")?.Value.Replace("NFe", "") ?? "";
                }
                else
                {
                    doc.ValidationErrors.Add("Chave de acesso não encontrada.");
                    doc.Status = FiscalDocumentStatus.Invalid;
                }

                // Extract Date
                var dhEmi = xml.Descendants().FirstOrDefault(x => x.Name.LocalName == "dhEmi");
                if (dhEmi != null && DateTime.TryParse(dhEmi.Value, out var date))
                {
                    doc.IssueDate = date;
                }

                // Extract Total
                var vNF = xml.Descendants().FirstOrDefault(x => x.Name.LocalName == "vNF");
                if (vNF != null && decimal.TryParse(vNF.Value.Replace(".", ","), out var total))
                {
                    doc.TotalAmount = total;
                }

                if (doc.TotalAmount <= 0)
                {
                     doc.ValidationErrors.Add("Alerta: Valor total da nota é zero ou negativo.");
                }
                
                if (doc.ValidationErrors.Any(e => e.Contains("inválido") || e.Contains("Erro") || e.Contains("Divergência") || e.Contains("incompatível")))
                {
                    doc.Status = FiscalDocumentStatus.Invalid;
                }
            }
            catch (Exception ex)
            {
                doc.Status = FiscalDocumentStatus.Invalid;
                doc.ValidationErrors.Add($"Erro ao ler XML: {ex.Message}");
            }

            return doc;
        }

        private static void ValidateWithZeus(nfeProc nfe, List<string> errors)
        {
            // Validação de DigestValue via Zeus
            if (nfe.protNFe != null && nfe.NFe.Signature != null)
            {
                var protDigVal = nfe.protNFe.infProt.digVal;
                var sigDigVal = nfe.NFe.Signature.SignedInfo.Reference.DigestValue;
                if (protDigVal != sigDigVal)
                {
                    errors.Add("DigestValue do protocolo e da assinatura são diferentes (Integridade física comprometida).");
                }
            }
        }

        private static void ValidateSignatureAndDigest(XElement root, List<string> errors)
        {
            var signature = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "Signature");
            if (signature == null)
            {
                errors.Add("Aviso: Assinatura digital não encontrada no XML.");
            }

            var protNFe = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "protNFe");
            if (protNFe != null)
            {
                var digVal = protNFe.Descendants().FirstOrDefault(e => e.Name.LocalName == "digVal")?.Value;
                var infNFeDigVal = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "Reference")?
                                        .Descendants().FirstOrDefault(e => e.Name.LocalName == "DigestValue")?.Value;

                if (!string.IsNullOrEmpty(digVal) && !string.IsNullOrEmpty(infNFeDigVal) && digVal != infNFeDigVal)
                {
                    errors.Add("Divergência: O DigestValue do protocolo não coincide com o da assinatura.");
                }
            }
        }

        private static decimal GetDecimal(XElement? parent, string tagName)
        {
            var val = parent?.Descendants().FirstOrDefault(e => e.Name.LocalName == tagName)?.Value;
            if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal res))
                return res;
            return 0;
        }
        public static FiscalAnalysis Analyze(Invoice invoice)
        {
            return new FiscalAnalysis(invoice);
        }
    }
}
