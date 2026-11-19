namespace MLCalc.Web.Tests;

/// <summary>How the server serves the Blazor client's files.</summary>
public class ClientFilesTests
{
	/// <summary>
	/// Files without a hash in their name must be checked on every load: otherwise a phone's browser
	/// can show an old page after a deploy. (The test server runs in Development, where every file
	/// is sent this way; the published image is checked by hand.)
	/// </summary>
	[Theory]
	[InlineData("/")]
	[InlineData("/some/other/path")]
	[InlineData("/index.html")]
	[InlineData("/css/app.css")]
	[InlineData("/js/mlcalc.js")]
	[InlineData("/_framework/blazor.webassembly.js")]
	public async Task FilesWithoutAHashAreCheckedOnEveryLoad(string path)
	{
		await using var server = new WebTestServer();
		using var response = await server.CreateClient().GetAsync(path);
		response.EnsureSuccessStatusCode();
		Assert.True(response.Headers.CacheControl?.NoCache, $"{path}: Cache-Control is '{response.Headers.CacheControl}'");
	}
}
