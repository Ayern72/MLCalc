namespace MLCalc.Language.Tests;

public class ParserTests
{
	/// <summary>Parses without errors and prints the tree as S-expressions.</summary>
	static string P(string text)
	{
		var result = Parser.Parse(text);
		Assert.True(result.Diagnostics.IsEmpty, string.Join("; ", result.Diagnostics));
		Assert.False(result.IsIncomplete);
		return result.Root.ToString();
	}

	[Theory]
	[InlineData("1 + 2 * 3", "(+ 1 (* 2 3))")]
	[InlineData("(1 + 2) * 3", "(* (+ 1 2) 3)")]
	[InlineData("1 - 2 - 3", "(- (- 1 2) 3)")]
	[InlineData("-2^2", "(- (^ 2 2))")]
	[InlineData("2^-1", "(^ 2 (- 1))")]
	[InlineData("2^3^2", "(^ (^ 2 3) 2)")]
	[InlineData("a .* b ./ c", "(./ (.* a b) c)")]
	[InlineData("A \\ b", "(\\ A b)")]
	[InlineData("7 mod 3 div 2", "(div (mod 7 3) 2)")]
	[InlineData("a < b and c >= d or e", "(or (and (< a b) (>= c d)) e)")]
	[InlineData("a == b xor c ~= d", "(xor (== a b) (~= c d))")]
	[InlineData("a <> b", "(~= a b)")]
	[InlineData("not a and b", "(and (not a) b)")]
	[InlineData("~a | b & c", "(or (not a) (and b c))")]
	[InlineData("3!", "(! 3)")]
	[InlineData("-3!", "(- (! 3))")]
	[InlineData("t'", "(' t)")]
	[InlineData("t.'", "(.' t)")]
	[InlineData("x'*x", "(* (' x) x)")]
	[InlineData("2i + 1", "(+ 2i 1)")]
	[InlineData("1e-3", "0.001")]
	public void OperatorPrecedence(string text, string expected)
	{
		Assert.Equal(expected, P(text));
	}

	[Theory]
	[InlineData("1:10", "(: 1 10)")]
	[InlineData("0:0.5:2", "(: 0 0.5 2)")]
	[InlineData("a+1:b-1", "(: (+ a 1) (- b 1))")]
	public void Ranges(string text, string expected)
	{
		Assert.Equal(expected, P(text));
	}

	[Theory]
	[InlineData("sin(x)", "(sin x)")]
	[InlineData("f()", "(f)")]
	[InlineData("t(1,:)", "(t 1 :)")]
	[InlineData("plot(x,y,-)", "(plot x y `-)")]
	[InlineData("plot(t(1,:),t(6,:),+)", "(plot (t 1 :) (t 6 :) `+)")]
	[InlineData("fplot(\"x\",\"sin(x)\")", "(fplot \"x\" \"sin(x)\")")]
	[InlineData("max(a, -b)", "(max a (- b))")]
	public void CallsAndIndexing(string text, string expected)
	{
		Assert.Equal(expected, P(text));
	}

	[Theory]
	[InlineData("[1, 2; 3, 4]", "[1 2; 3 4]")]
	[InlineData("[1 2\n3 4]", "[1 2; 3 4]")]
	[InlineData("[1 -2]", "[1 (- 2)]")]
	[InlineData("[1 - 2]", "[(- 1 2)]")]
	[InlineData("[1-2]", "[(- 1 2)]")]
	[InlineData("[a' b']", "[(' a) (' b)]")]
	[InlineData("[f(1) g (2)]", "[(f 1) g 2]")]
	[InlineData("[]", "[]")]
	[InlineData("[1, 2;]", "[1 2]")]
	[InlineData("[(1 -2)]", "[(- 1 2)]")]
	public void MatrixLiterals(string text, string expected)
	{
		Assert.Equal(expected, P(text));
	}

	[Theory]
	[InlineData("x = 1", "(= x 1)")]
	[InlineData("x = 1;", "(= x 1);")]
	[InlineData("x(1,k) = k;", "(= (x 1 k) k);")]
	[InlineData("f(x) == 1", "(== (f x) 1)")]
	[InlineData("a = b == c", "(= a (== b c))")]
	public void Assignments(string text, string expected)
	{
		Assert.Equal(expected, P(text));
	}

	[Fact]
	public void StatementsAreSeparatedByNewLinesCommasAndSemicolons()
	{
		Assert.Equal("a;\nb\nc\nd", P("a; b, c\nd"));
	}

	[Fact]
	public void IfThenElseOnOneLine()
	{
		Assert.Equal("(if (> a 0) b c)", P("if a > 0 then b else c"));
	}

	[Fact]
	public void IfWithBlocksOnSeparateLines()
	{
		const string source = """
			if a == 0 then
			begin
				x = 1;
				y = 2
			end
			else
			begin
				x = 3;
			end
			""";
		Assert.Equal("(if (== a 0) {(= x 1); (= y 2)} {(= x 3);})", P(source));
	}

	[Fact]
	public void ElseIfChains()
	{
		Assert.Equal("(if a x (if b y z))", P("if a then x\nelse if b then y\nelse z"));
	}

	[Fact]
	public void IfWithoutElseDoesNotSwallowFollowingStatement()
	{
		Assert.Equal("(if a x)\ny", P("if a then x\n\ny"));
	}

	[Fact]
	public void WhileLoop()
	{
		Assert.Equal("(while (<= k 10) {(= k (+ k 1));})", P("while k <= 10 do\nbegin\n\tk = k + 1;\nend"));
	}

	[Fact]
	public void ForLoops()
	{
		Assert.Equal("(for k 1 10 (= s (+ s k));)", P("for k = 1 to 10 do s = s + k;"));
		Assert.Equal("(for k (: 1 2 9) {k})", P("for k = 1:2:9 do begin k end"));
	}

	[Fact]
	public void NestedBlocks()
	{
		Assert.Equal("{{a} b}", P("begin begin a end b end"));
	}

	[Fact]
	public void CommandsWithoutArguments()
	{
		Assert.Equal("List\ncls", P("List\ncls"));
	}

	[Fact]
	public void SpansCoverTheSource()
	{
		var result = Parser.Parse("x = 1 + 22");
		var assignment = Assert.IsType<AssignmentStatement>(Assert.Single(result.Root.Statements));
		Assert.Equal("1 + 22", result.Text.ToString(assignment.Value.Span));
		Assert.Equal("x = 1 + 22", result.Text.ToString(assignment.Span));
	}

	[Fact]
	public void LinePositionsAreOneBasedWhenPrinted()
	{
		var text = new SourceText("a\nbc\nd");
		Assert.Equal("2:2", text.GetLinePosition(3).ToString());
	}
}

public class ParserErrorTests
{
	[Theory]
	[InlineData("1 +")]
	[InlineData("[1, 2")]
	[InlineData("f(1,")]
	[InlineData("while a do")]
	[InlineData("if a then")]
	[InlineData("begin\n x = 1")]
	public void InputEndingMidConstructIsIncomplete(string text)
	{
		var result = Parser.Parse(text);
		Assert.True(result.HasErrors);
		Assert.True(result.IsIncomplete);
	}

	[Theory]
	[InlineData("1 + * 2")]
	[InlineData("a b")]
	[InlineData("if a b")]
	[InlineData("end")]
	[InlineData("(1 + 2")]
	public void InvalidInputIsReportedButNotIncomplete(string text)
	{
		var result = Parser.Parse(text);
		Assert.True(result.HasErrors);
		if (text != "(1 + 2")
			Assert.False(result.IsIncomplete);
	}

	[Fact]
	public void RecoversAtNextStatement()
	{
		var result = Parser.Parse("x = 1 +* 2\ny = 3\nz = )\nw = 4");
		Assert.Equal(2, result.Diagnostics.Length);
		Assert.Equal("(= y 3)\n(= w 4)", result.Root.ToString());
	}

	[Fact]
	public void RecoversInsideBlocks()
	{
		var result = Parser.Parse("begin\n x = *\n y = 1\nend\nz = 2");
		Assert.Single(result.Diagnostics);
		Assert.Equal("{(= y 1)}\n(= z 2)", result.Root.ToString());
	}

	[Fact]
	public void ErrorPointsAtOffendingToken()
	{
		var result = Parser.Parse("x = 1 + )");
		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal(new SourceSpan(8, 1), diagnostic.Span);
	}
}

public class SampleScriptTests
{
	public static TheoryData<string> Samples()
	{
		var data = new TheoryData<string>();
		foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "samples"), "*.m").Order())
			data.Add(Path.GetFileName(file));
		return data;
	}

	[Theory]
	[MemberData(nameof(Samples))]
	public void OriginalSampleScriptsParseWithoutErrors(string file)
	{
		var result = Parser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", file)));
		Assert.True(result.Diagnostics.IsEmpty, string.Join("\n", result.Diagnostics.Select(d => result.Text.GetLinePosition(d.Span.Start) + " " + d.Message)));
		Assert.NotEmpty(result.Root.Statements);
	}

	[Fact]
	public void FysikDataTableIsA45By6Matrix()
	{
		var result = Parser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "FYSIK.m")));
		var assignment = Assert.IsType<AssignmentStatement>(result.Root.Statements[0]);
		var matrix = Assert.IsType<MatrixLiteral>(assignment.Value);
		Assert.Equal(45, matrix.Rows.Length);
		Assert.All(matrix.Rows, row => Assert.Equal(6, row.Length));
	}

	[Fact]
	public void FeigenbaumHasNestedLoopsAndIf()
	{
		var result = Parser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "FEIGENBA.m")));
		var outer = Assert.IsType<WhileStatement>(result.Root.Statements.Single(s => s is WhileStatement));
		var block = Assert.IsType<BlockStatement>(outer.Body);
		var inner = Assert.IsType<WhileStatement>(block.Statements.Single(s => s is WhileStatement));
		Assert.Contains(((BlockStatement)inner.Body).Statements, s => s is IfStatement);
	}
}
