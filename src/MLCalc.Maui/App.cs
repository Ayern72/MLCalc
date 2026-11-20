using Microsoft.AspNetCore.Components.WebView.Maui;

namespace MLCalc.Maui;

/// <summary>One window with the page in a web view, without XAML.</summary>
public sealed class App : Microsoft.Maui.Controls.Application
{
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var view = new BlazorWebView { HostPage = "wwwroot/index.html" };
		view.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Main) });
		var page = new ContentPage
		{
			Content = view,
			BackgroundColor = Colors.White,
			// Inside the system bars, which the app would otherwise draw under, and above the keyboard,
			// which makes the page smaller rather than covering the input box, as in a browser.
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.All),
		};
		return new Window(page) { Title = "MLCalc" };
	}
}
