using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace MLCalc.Maui;

// Turning or folding the phone, or a keyboard coming and going, keeps the activity, and so the page
// and its console.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
	ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout
		| ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		SetBarIcons(Resources?.Configuration);
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		SetBarIcons(newConfig);
	}

	/// <summary>
	/// Dark icons on the light page, light ones in dark mode, where the page is dark too (it follows
	/// the system's setting, as in a browser).
	/// </summary>
	void SetBarIcons(Configuration? configuration)
	{
		bool dark = configuration is not null && (configuration.UiMode & UiMode.NightMask) == UiMode.NightYes;
		if (Window is { } window && WindowCompat.GetInsetsController(window, window.DecorView) is { } bars)
		{
			bars.AppearanceLightStatusBars = !dark;
			bars.AppearanceLightNavigationBars = !dark;
		}
	}
}
