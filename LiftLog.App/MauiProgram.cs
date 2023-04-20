﻿using Android.Renderscripts;
using Fluxor;
using Microsoft.Extensions.Logging;
using LiftLog.App.Services;
using LiftLog.Lib.Services;
using LiftLog.Lib.Store;
using LiftLog.Ui.Services;
using LiftLog.Ui.Store.CurrentSession;
using LiftLog.Ui.Store.Program;
using LiftLog.WebUi.Services;

namespace LiftLog.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        
        builder.Services.AddFluxor(
            o =>
                o.ScanAssemblies(typeof(Program).Assembly)
                    .AddMiddleware<PersistSessionMiddleware>()
                    .AddMiddleware<PersistProgramMiddleware>()
                    .UseReduxDevTools()
        );
        builder.Services.AddScoped<IProgressStore, LocalStorageProgressStore>();
        builder.Services.AddScoped<IProgramStore, LocalStorageProgramStore>();
        builder.Services.AddScoped<SessionService>();

        builder.Services.AddScoped<INotificationService, WebNotificationService>();


        return builder.Build();
    }
}