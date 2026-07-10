using System.Text.Json;
using Microsoft.JSInterop;

namespace ApparkaTrainingFlow.Services;

public sealed class LocalStorageService(IJSRuntime jsRuntime)
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task SetAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, _options);
        await jsRuntime.InvokeVoidAsync("appStorage.set", key, json);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var json = await jsRuntime.InvokeAsync<string?>("appStorage.get", key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public Task RemoveAsync(string key) => jsRuntime.InvokeVoidAsync("appStorage.remove", key).AsTask();
}
