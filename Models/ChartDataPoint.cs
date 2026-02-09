namespace GlassHub.Models;

/// <summary>
/// Representa um ponto de dados para gráficos.
/// </summary>
public class ChartDataPoint
{
    /// <summary>
    /// Rótulo do eixo X (ex: Data ou Categoria).
    /// </summary>
    public string X { get; set; } = string.Empty;

    /// <summary>
    /// Valor do eixo Y.
    /// </summary>
    public decimal Y { get; set; }

    /// <summary>
    /// Cor opcional para o ponto de dados.
    /// </summary>
    public string? FillColor { get; set; }
}
