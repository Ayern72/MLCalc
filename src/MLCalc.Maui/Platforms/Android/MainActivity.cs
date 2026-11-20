using Android.App;
using Android.Content.PM;
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
		// The page is white, also in dark mode, so the system bars need dark icons to be seen.
		if (Window is { } window && WindowCompat.GetInsetsController(window, window.DecorView) is { } bars)
		{
			bars.AppearanceLightStatusBars = true;
			bars.AppearanceLightNavigationBars = true;
		}
	}
}
