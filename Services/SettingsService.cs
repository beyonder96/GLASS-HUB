using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace GlassHub.Services
{
    public class SettingsService
    {
        private readonly IJSRuntime _jsRuntime;

        public string UserName { get; private set; } = "Convidado";
        public string? UserAvatar { get; private set; }
        public decimal DailyBuffer { get; private set; } = 0;
        public string HolidayStrategy { get; private set; } = "POSTPONE";

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
                var savedAvatar = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "user_avatar");
                var savedBuffer = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "user_buffer");
                var savedStrategy = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "holiday_strategy");

                if (!string.IsNullOrEmpty(savedName)) UserName = savedName;
                if (!string.IsNullOrEmpty(savedAvatar)) UserAvatar = savedAvatar;
                if (!string.IsNullOrEmpty(savedBuffer) && decimal.TryParse(savedBuffer, out var b)) DailyBuffer = b;
                if (!string.IsNullOrEmpty(savedStrategy)) HolidayStrategy = savedStrategy;
                
                NotifyStateChanged();
            }
            catch { /* Ignore prerender errors */ }
        }

        public async Task SaveSettingsAsync(string name, string? avatar, decimal buffer, string strategy)
        {
            UserName = name;
            UserAvatar = avatar;
            DailyBuffer = buffer;
            HolidayStrategy = strategy;

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_name", UserName);
            if (!string.IsNullOrEmpty(UserAvatar))
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_avatar", UserAvatar);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_buffer", DailyBuffer.ToString());
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "holiday_strategy", HolidayStrategy);

            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
