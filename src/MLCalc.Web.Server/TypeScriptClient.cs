using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace MLCalc.Web.Server;

/// <summary>
/// Serves the TypeScript client at <c>/ts/</c>: a second client of the same hub, built by Vite into
/// the folder <see cref="WebOptions.TypeScriptClient"/>. The Blazor client stays at <c>/</c>.
/// </summary>
static class TypeScriptClient
{
	/// <summary>The path the client is served at.</summary>
	public const string Path = "/ts";

	/// <summary>
	/// Adds the client's files: those under <c>assets/</c> have a hash in their name (Vite gives them
	/// one) and are cached for good; the others, index.html above all, are checked on every load.
	/// Paths without a file name get index.html. Without the folder, <c>/ts/</c> says how to build it.
	/// </summary>
	public static void MapTypeScriptClient(this WebApplication app)
	{
		var options = app.Services.GetRequiredService<IOptions<WebOptions>>().Value;
		string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(app.Environment.ContentRootPath, options.TypeScriptClient));

		// The page's files are referred to relative to /ts/, so the slash is needed. (Not a route:
		// routes match /ts/ as well.)
		app.Use((context, next) =>
		{
			if (context.Request.Path.Value == Path)
			{
				context.Response.Redirect(Path + "/" + context.Request.QueryString);
				return Task.CompletedTask;
			}
			return next(context);
		});
		if (!Directory.Exists(folder))
		{
			app.MapGet(Path + "/{**path}", () => Results.Text(
				"The TypeScript client is not built here. Run npm run build in src/MLCalc.Web.TypeScript.", statusCode: StatusCodes.Status404NotFound));
			return;
		}

		var files = new PhysicalFileProvider(folder);
		app.UseStaticFiles(new StaticFileOptions
		{
			RequestPath = Path,
			FileProvider = files,
			OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
				context.Context.Request.Path.StartsWithSegments(Path + "/assets") ? "public, max-age=31536000, immutable" : "no-cache",
		});
		app.MapFallbackToFile(Path + "/{*path:nonfile}", "index.html", new StaticFileOptions
		{
			FileProvider = files,
			OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
		});
	}
}
