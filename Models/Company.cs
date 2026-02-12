namespace GlassHub.Models;

/// <summary>
/// Representa uma empresa (cliente) no sistema.
/// </summary>
public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Nome da Empresa.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// CNPJ da Empresa (para validação de XML).
    /// </summary>
    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Cor hexadecimal para personalização da UI.
    /// </summary>
    public string ThemeColor { get; set; } = "#3b82f6"; // Default blue

    /// <summary>
    /// Logo da empresa (opcional).
    /// </summary>
    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID da Empresa Matriz (se houver).
    /// </summary>
    public string? ParentCompanyId { get; set; }

    /// <summary>
    /// Indica se é Matriz (ou empresa independente).
    /// </summary>
    public bool IsHeadOffice => string.IsNullOrEmpty(ParentCompanyId);
}

public class UserCompany
{
    public string UserId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Role { get; set; } = "viewer"; // owner, admin, viewer
}
