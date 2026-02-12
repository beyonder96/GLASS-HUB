using GlassHub.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace GlassHub.Services;

public class CompanyService
{
    private readonly IJSRuntime _js;
    public event Action? OnChange;

    public Company? ActiveCompany { get; private set; }
    public List<Company> UserCompanies { get; private set; } = new();
    
    public bool IsLoading { get; private set; } = true;

    public CompanyService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        
        var savedCompaniesJson = await _js.InvokeAsync<string?>("localStorage.getItem", "userCompanies");
        if (!string.IsNullOrEmpty(savedCompaniesJson))
        {
            UserCompanies = JsonSerializer.Deserialize<List<Company>>(savedCompaniesJson) ?? new();
        }
        else
        {
            UserCompanies = new List<Company>();
            await SaveCompaniesToStorage();
        }

        var savedCompanyId = await _js.InvokeAsync<string?>("localStorage.getItem", "activeCompanyId");
        
        if (!string.IsNullOrEmpty(savedCompanyId))
        {
            ActiveCompany = UserCompanies.FirstOrDefault(c => c.Id == savedCompanyId);
        }

        // If only 1 company and none active, auto-select it
        if (ActiveCompany == null && UserCompanies.Count == 1)
        {
            await SelectCompany(UserCompanies[0]);
        }

        IsLoading = false;
        NotifyStateChanged();
    }

    public async Task AddCompany(string name, string cnpj, string color, string? parentId = null)
    {
        var newCompany = new Company { 
            Name = name, 
            TaxId = cnpj, 
            ThemeColor = color,
            ParentCompanyId = parentId
        };
        
        UserCompanies.Add(newCompany);
        await SaveCompaniesToStorage();
        NotifyStateChanged();
    }

    public async Task DeleteCompany(Company company)
    {
        UserCompanies.Remove(company);
        
        // If active company is deleted, clear active
        if (ActiveCompany?.Id == company.Id)
        {
            await ClearActiveCompany();
        }

        // Also delete or unlink branches? For now, let's just make them independent (optional decision)
        // Or we could cascade delete. Let's keep it simple: orphans become Matriz automatically due to IsHeadOffice logic.

        await SaveCompaniesToStorage();
        NotifyStateChanged();
    }

    private async Task SaveCompaniesToStorage()
    {
        var json = JsonSerializer.Serialize(UserCompanies);
        await _js.InvokeVoidAsync("localStorage.setItem", "userCompanies", json);
    }

    public async Task SelectCompany(Company company)
    {
        ActiveCompany = company;
        await _js.InvokeVoidAsync("localStorage.setItem", "activeCompanyId", company.Id);
        NotifyStateChanged();
    }

    public async Task ClearActiveCompany()
    {
        ActiveCompany = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", "activeCompanyId");
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
