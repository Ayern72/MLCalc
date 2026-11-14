using System.Globalization;
using System.Runtime.CompilerServices;

namespace MLCalc.Tests;

/// <summary>
/// Runs every test under Swedish formatting: a decimal comma, a space as thousands separator and
/// U+2212 as the minus sign. Numbers written or read with the current culture by mistake then make
/// the tests fail on any machine, including CI, whose culture is English or invariant.
/// </summary>
/// <remarks>Compiled into each test project by tests/Directory.Build.props.</remarks>
static class TestCulture
{
	public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("sv-SE");

	[ModuleInitializer]
	internal static void Initialize()
	{
		CultureInfo.DefaultThreadCurrentCulture = Culture;
		CultureInfo.DefaultThreadCurrentUICulture = Culture;
		CultureInfo.CurrentCulture = Culture;
		CultureInfo.CurrentUICulture = Culture;
	}
}

public class TestCultureTests
{
	[Fact]
	public void TestsRunUnderACultureWithADecimalComma()
	{
		Assert.Equal(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
		Assert.Equal("1,5", 1.5.ToString(CultureInfo.CurrentCulture));
	}

	[Fact]
	public async Task ThreadPoolThreadsUseTheSameCulture()
	{
		string separator = await Task.Run(() => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
		Assert.Equal(",", separator);
	}
}
