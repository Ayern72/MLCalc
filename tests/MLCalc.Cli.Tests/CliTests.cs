namespace MLCalc.Cli.Tests;

public sealed class CliTests : IDisposable
{
	readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("mlcalc-cli");

	public void Dispose() => directory.Delete(recursive: true);

	string FilePath(string name) => Path.Combine(directory.FullName, name);

	static (int Status, string Output, string Error) Run(string input, params string[] args) =>
		Run(input, inputRedirected: false, args);

	static (int Status, string Output, string Error) Run(string input, bool inputRedirected, params string[] args)
	{
		var output = new StringWriter();
		var error = new StringWriter();
		var app = new CliApp(new StringReader(input), output, error, isConsole: false);
		int status = app.Run(args, inputRedirected);
		return (status, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
	}

	[Fact]
	public void EvaluatesExpressions()
	{
		var (status, output, error) = Run("", "-e", "A = [1 2; 3 4];", "-e", "A * [1; 1]");
		Assert.Equal(CliApp.Success, status);
		Assert.Equal("ans =\n  3\n  7\n", output);
		Assert.Empty(error);
	}

	[Fact]
	public void RunsScriptAndReportsErrorsWithFileAndLine()
	{
		File.WriteAllText(FilePath("bad.m"), "x = 1\ny = nope;\nz = 3");
		var (status, output, error) = Run("", FilePath("bad.m"));
		Assert.Equal(CliApp.Failure, status);
		Assert.Equal("x = 1\n", output);
		Assert.Equal("bad.m:2:5: Unknown function or variable 'nope'.\n", error);
	}

	[Fact]
	public void SyntaxErrorsFail()
	{
		var (status, _, error) = Run("", "-e", "1 + * 2");
		Assert.Equal(CliApp.Failure, status);
		Assert.StartsWith("-e:1:5:", error);
	}

	[Fact]
	public void MissingScriptFails()
	{
		var (status, _, error) = Run("", "missing.m");
		Assert.Equal(CliApp.Failure, status);
		Assert.Contains("cannot find", error);
	}

	[Fact]
	public void ReadsPipedInputAsScript()
	{
		var (status, output, _) = Run("x = 2;\nx^10", inputRedirected: true);
		Assert.Equal(CliApp.Success, status);
		Assert.Equal("ans = 1024\n", output);
	}

	[Fact]
	public void SavesSvg()
	{
		string svg = FilePath("plot.svg");
		var (status, _, error) = Run("", "-e", "xrange(0, 6); fplot(\"x\", \"sin(x)\"); plot([1 2 3], [0 0.5 0], +)", "--svg", svg, "--size", "640x480");
		Assert.Equal(CliApp.Success, status);
		Assert.Contains("Saved", error);
		string text = File.ReadAllText(svg);
		Assert.StartsWith("<svg", text);
		Assert.Contains("width=\"640\"", text);
		Assert.Equal(2, text.Split("<path").Length - 1);
	}

	[Fact]
	public void SavefigCommandWritesSvgRelativeToWorkingDirectory()
	{
		File.WriteAllText(FilePath("draw.m"), "dot(1, 1)\nsavefig(\"out.svg\")");
		var (status, output, _) = Run("", FilePath("draw.m"));
		Assert.Equal(CliApp.Success, status);
		Assert.Contains("Saved", output);
		Assert.True(File.Exists(FilePath("out.svg")));
	}

	[Fact]
	public void ReplEvaluatesLinesAndExits()
	{
		var (status, output, _) = Run("x = 3\nx * 2\nexit\nnot reached\n");
		Assert.Equal(CliApp.Success, status);
		Assert.Contains(">> x = 3\n>> ans = 6\n>> ", output);
		Assert.DoesNotContain("not reached", output);
	}

	[Fact]
	public void ReplContinuesUnfinishedInput()
	{
		var (_, output, _) = Run("k = 0;\nwhile k < 2 do\nbegin\nk = k + 1\nend\n");
		Assert.Contains(">> >> ... ... ... k = 1\nk = 2\n", output);
	}

	[Fact]
	public void ReplReportsErrorsAndKeepsGoing()
	{
		var (status, output, error) = Run("1/0\n2 + 2\n");
		Assert.Equal(CliApp.Success, status);
		Assert.Equal("1:1: Division by zero.\n", error);
		Assert.Contains("ans = 4", output);
	}

	[Fact]
	public void ReplHintsAboutSavefigOnce()
	{
		var (_, output, _) = Run("dot(1,1)\ndot(2,2)\n");
		Assert.Single(output.Split("savefig").Skip(1));
	}

	[Fact]
	public void InteractiveAfterScript()
	{
		var (_, output, _) = Run("y\n", "-e", "y = 5;", "-i");
		Assert.Contains(">> y = 5", output);
	}

	[Theory]
	[InlineData("--bogus")]
	[InlineData("--svg")]
	[InlineData("--size", "big")]
	public void UsageErrors(params string[] args)
	{
		Assert.Equal(CliApp.UsageError, Run("", args).Status);
	}

	[Fact]
	public void HelpAndVersion()
	{
		Assert.Contains("Usage: mlcalc", Run("", "--help").Output);
		Assert.StartsWith("mlcalc ", Run("", "--version").Output);
	}
}
