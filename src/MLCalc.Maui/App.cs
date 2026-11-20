using Microsoft.AspNetCore.Components.WebView.Maui;

namespace MLCalc.Maui;

/// <summary>One window with the page in a web view, without XAML.</summary>
public sealed class App : Microsoft.Maui.Controls.Application
{
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var view = new BlazorWebView { HostPage = "wwwroot/index.html" };
		view.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Main) });
		var page = new AppPage
		{
			Content = view,
			// Inside the system bars, which the app would otherwise draw under, and above the keyboard,
			// which makes the page smaller rather than covering the input box, as in a browser.
			SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.All),
		};
		// Behind the system bars: the colour of the page's toolbar (app.css, --panel).
		page.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Color.FromArgb("#1b2028"));
		return new Window(page) { Title = "MLCalc" };
	}

	/// <summary>The page, which keeps the app (and its console) when Back is pressed.</summary>
	sealed class AppPage : ContentPage
	{
		protected override bool OnBackButtonPressed()
		{
#if ANDROID
			// Like Home: the app goes to the background and comes back as it was. Closing it would
			// lose the console, which the desktop app keeps until its window is closed.
			Platform.CurrentActivity?.MoveTaskToBack(true);
			return true;
#else
			return base.OnBackButtonPressed();
#endif
		}
	}
}
