using System.Net;

namespace MLCalc.Web.Tests;

/// <summary>How the server serves the two clients' files: Blazor at /, TypeScript at /ts/.</summary>
public sealed class ClientFilesTests : IDisposable
{
	/// <summary>A stand-in for the built TypeScript client: an index.html and one hashed asset.</summary>
	readonly string typeScriptClient = Directory.CreateTempSubdirectory("mlcalc-ts-").FullName;

	public ClientFilesTests()
	{
		File.WriteAllText(Path.Combine(typeScriptClient, "index.html"), "<!doctype html><title>MLCalc TypeScript</title>");
		Directory.CreateDirectory(Path.Combine(typeScriptClient, "assets"));
		File.WriteAllText(Path.Combine(typeScriptClient, "assets", "index-AbC123xy.js"), "console.log('mlcalc');");
	}

	public void Dispose() => Directory.Delete(typeScriptClient, recursive: true);

	WebTestServer ServerWithTypeScriptClient() => new(options => options.TypeScriptClient = typeScriptClient);

	/// <summary>
	/// Files without a hash in their name must be checked on every load: otherwise a phone's browser
	/// can show an old page after a deploy. (The test server runs in Development, where every Blazor
	/// file is sent this way; the published image is checked by hand.)
	/// </summary>
	[Theory]
	[InlineData("/")]
	[InlineData("/some/other/path")]
	[InlineData("/index.html")]
	[InlineData("/_content/MLCalc.Web.Ui/css/app.css")]
	[InlineData("/_content/MLCalc.Web.Ui/js/mlcalc.js")]
	[InlineData("/_framework/blazor.webassembly.js")]
	public async Task FilesWithoutAHashAreCheckedOnEveryLoad(string path)
	{
		await using var server = new WebTestServer();
		using var response = await server.CreateClient().GetAsync(path);
		response.EnsureSuccessStatusCode();
		Assert.True(response.Headers.CacheControl?.NoCache, $"{path}: Cache-Control is '{response.Headers.CacheControl}'");
	}

	[Theory]
	[InlineData("/ts/")]
	[InlineData("/ts/index.html")]
	[InlineData("/ts/some/other/path")]
	public async Task TheTypeScriptPageIsCheckedOnEveryLoad(string path)
	{
		await using var server = ServerWithTypeScriptClient();
		using var response = await server.CreateClient().GetAsync(path);
		response.EnsureSuccessStatusCode();
		Assert.Contains("MLCalc TypeScript", await response.Content.ReadAsStringAsync());
		Assert.True(response.Headers.CacheControl?.NoCache, $"{path}: Cache-Control is '{response.Headers.CacheControl}'");
	}

	[Fact]
	public async Task TheTypeScriptAssetsAreKept()
	{
		await using var server = ServerWithTypeScriptClient();
		using var response = await server.CreateClient().GetAsync("/ts/assets/index-AbC123xy.js");
		response.EnsureSuccessStatusCode();
		Assert.Equal("console.log('mlcalc');", await response.Content.ReadAsStringAsync());
		Assert.True(response.Headers.CacheControl?.MaxAge >= TimeSpan.FromDays(365), $"Cache-Control is '{response.Headers.CacheControl}'");
	}

	[Fact]
	public async Task AMissingTypeScriptFileIsNotFound()
	{
		await using var server = ServerWithTypeScriptClient();
		using var response = await server.CreateClient().GetAsync("/ts/assets/missing.js");
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	/// <summary>Always to /ts/ itself: nothing from the request, such as a query string, goes into the redirect.</summary>
	[Theory]
	[InlineData("/ts")]
	[InlineData("/ts?next=//attacker.example/")]
	[InlineData("/ts?%0d%0aLocation:%20https://attacker.example/")]
	public async Task TheTypeScriptPathWithoutASlashIsRedirected(string path)
	{
		await using var server = ServerWithTypeScriptClient();
		var client = server.CreateClient(new() { AllowAutoRedirect = false });
		using var response = await client.GetAsync(path);
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/ts/", response.Headers.Location?.OriginalString);
	}

	[Fact]
	public async Task WithoutTheTypeScriptClientItsPathSaysHowToBuildIt()
	{
		await using var server = new WebTestServer(options => options.TypeScriptClient = Path.Combine(typeScriptClient, "not-built"));
		using var response = await server.CreateClient().GetAsync("/ts/");
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		Assert.Contains("npm run build", await response.Content.ReadAsStringAsync());
	}
}
