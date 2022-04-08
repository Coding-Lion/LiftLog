using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Fluxor;
using SimpleGymTracker.Lib.Store;
using SimpleGymTracker.WebUi.Store.WorkoutSession;
using Blazored.LocalStorage;
using SimpleGymTracker.WebUi.Services;
using SimpleGymTracker.WebUi;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<WebApplication>("#app");

builder.Services.AddScoped(
    sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }
);

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddFluxor(
    o =>
        o.ScanAssemblies(typeof(Program).Assembly)
            .AddMiddleware<PersistSessionMiddleware>()
            .UseReduxDevTools()
);
builder.Services.AddScoped<IProgressStore, LocalStorageProgressStore>();

await builder.Build().RunAsync();
