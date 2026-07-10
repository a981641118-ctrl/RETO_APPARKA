using ApparkaTrainingFlow;
using ApparkaTrainingFlow.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<QrValidationService>();
builder.Services.AddScoped<TrainingStateService>();

await builder.Build().RunAsync();
