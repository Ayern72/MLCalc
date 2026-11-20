using MLCalc.Web.Ui;

namespace MLCalc.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();
		builder.Services.AddMauiBlazorWebView();
#if WEBVIEW_DEBUGGING
		// chrome://inspect on the PC shows the page while the phone is connected (see the project file).
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif
		// The workspace lives as long as the app; each page (a new one when Android makes the window
		// anew) has its own session in it.
		builder.Services.AddSingleton<LocalWorkspace>();
		builder.Services.AddScoped<IMLCalcSession, LocalSession>();
		return builder.Build();
	}
}
