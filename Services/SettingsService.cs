using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace GlassHub.Services
{
    public class SettingsService
    {
        private readonly IJSRuntime _jsRuntime;

        public string UserName { get; private set; } = "Convidado";
        public string Email { get; private set; } = "joao@glasshub.com";
        public string? UserAvatar { get; private set; }
        public decimal DailyBuffer { get; private set; } = 0;
        public string HolidayStrategy { get; private set; } = "POSTPONE";
        public bool IsDarkMode { get; private set; } = true;

        public event Action? OnChange;

        public SettingsService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var savedName = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "user_name");
                var savedEmail = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "user_email");
                var savedAvatar = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "user_avatar");
                var savedBuffer = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "user_buffer");
                var savedStrategy = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "holiday_strategy");
                var savedTheme = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "theme");

                if (!string.IsNullOrEmpty(savedName)) UserName = savedName;
                if (!string.IsNullOrEmpty(savedEmail)) Email = savedEmail;
                if (!string.IsNullOrEmpty(savedAvatar)) UserAvatar = savedAvatar;
                if (!string.IsNullOrEmpty(savedBuffer) && decimal.TryParse(savedBuffer, out var b)) DailyBuffer = b;
                if (!string.IsNullOrEmpty(savedStrategy)) HolidayStrategy = savedStrategy;
                if (!string.IsNullOrEmpty(savedTheme)) IsDarkMode = savedTheme == "dark";
                
                await ApplyTheme();
                NotifyStateChanged();
            }
            catch { /* Ignore prerender errors */ }
        }

        public async Task SaveSettingsAsync(string name, string email, string? avatar, decimal buffer, string strategy)
        {
            UserName = name;
            Email = email;
            UserAvatar = avatar;
            DailyBuffer = buffer;
            HolidayStrategy = strategy;

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_name", UserName);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_email", Email);
            if (!string.IsNullOrEmpty(UserAvatar))
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_avatar", UserAvatar);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_buffer", DailyBuffer.ToString());
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "holiday_strategy", HolidayStrategy);

            NotifyStateChanged();
        }

        public async Task ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", IsDarkMode ? "dark" : "light");
            await ApplyTheme();
            NotifyStateChanged();
        }

        private async Task ApplyTheme()
        {
            await _jsRuntime.InvokeVoidAsync("setTheme", IsDarkMode);
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
