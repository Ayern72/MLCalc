using System.Collections.Specialized;

namespace MLCalc.Wpf.Tests;

/// <summary>
/// The console between a fast-printing program and the UI: before these classes, every output line
/// was queued on the UI thread without limit, and 2 million lines took about 1.2 GB.
/// </summary>
public class ConsoleOutputBufferTests
{
	static ConsoleEntry Line(int k) => new(ConsoleEntryKind.Text, k.ToString(System.Globalization.CultureInfo.InvariantCulture));

	[Fact]
	public void KeepsAtMostCapacityLinesAndCountsTheRest()
	{
		var buffer = new ConsoleOutputBuffer(capacity: 100);
		for (int k = 0; k < 250; k++)
			buffer.Add(Line(k));
		var (cleared, entries, dropped) = buffer.Drain();
		Assert.False(cleared);
		Assert.Equal(Enumerable.Range(0, 100).Select(k => k.ToString()), entries.Select(e => e.Text));
		Assert.Equal(150, dropped);
	}

	[Fact]
	public void DrainingMakesRoomAgainAndResetsTheDropCount()
	{
		var buffer = new ConsoleOutputBuffer(capacity: 10);
		for (int k = 0; k < 20; k++)
			buffer.Add(Line(k));
		buffer.Drain();
		buffer.Add(Line(99));
		var (_, entries, dropped) = buffer.Drain();
		Assert.Equal("99", Assert.Single(entries).Text);
		Assert.Equal(0, dropped);
	}

	[Fact]
	public void KeepsAtMostMaxCharactersAndCountsTheRest()
	{
		// A 100×100 matrix prints as about 140,000 characters, so a line limit alone is not enough.
		var buffer = new ConsoleOutputBuffer(capacity: 100, maxCharacters: 3500);
		for (int k = 0; k < 10; k++)
			buffer.Add(new ConsoleEntry(ConsoleEntryKind.Text, new string('m', 1000)));
		var (_, entries, dropped) = buffer.Drain();
		Assert.Equal(3, entries.Count);
		Assert.Equal(7, dropped);
	}

	[Fact]
	public void DrainingAndClearingReleaseTheirCharacters()
	{
		var buffer = new ConsoleOutputBuffer(capacity: 100, maxCharacters: 2500);
		var line = new ConsoleEntry(ConsoleEntryKind.Text, new string('m', 1000));
		buffer.Add(line);
		buffer.Add(line);
		buffer.Drain();
		buffer.Add(line);
		buffer.Add(line);
		buffer.Clear();
		buffer.Add(line);
		buffer.Add(line);
		var (cleared, entries, dropped) = buffer.Drain();
		Assert.True(cleared);
		Assert.Equal(2, entries.Count);
		Assert.Equal(0, dropped);
	}

	[Fact]
	public void ClearDiscardsWhatIsQueuedBeforeIt()
	{
		var buffer = new ConsoleOutputBuffer(capacity: 10);
		for (int k = 0; k < 20; k++)
			buffer.Add(Line(k));
		buffer.Clear();
		buffer.Add(Line(42));
		var (cleared, entries, dropped) = buffer.Drain();
		Assert.True(cleared);
		Assert.Equal("42", Assert.Single(entries).Text);
		Assert.Equal(0, dropped);
	}

	[Fact]
	public void RepeatedClearsDoNotFillTheBuffer()
	{
		var buffer = new ConsoleOutputBuffer(capacity: 10);
		for (int k = 0; k < 100_000; k++)
			buffer.Clear();
		buffer.Add(Line(1));
		var (cleared, entries, dropped) = buffer.Drain();
		Assert.True(cleared);
		Assert.Single(entries);
		Assert.Equal(0, dropped);
	}

	[Fact]
	public async Task ConcurrentWriterAndReaderLoseNothingUncounted()
	{
		const int Total = 200_000;
		var buffer = new ConsoleOutputBuffer(capacity: 1000);
		long delivered = 0, dropped = 0;
		var writer = Task.Run(() =>
		{
			for (int k = 0; k < Total; k++)
				buffer.Add(Line(k));
		});
		while (!writer.IsCompleted)
		{
			var batch = buffer.Drain();
			delivered += batch.Entries.Count;
			dropped += batch.Dropped;
			Assert.InRange(batch.Entries.Count, 0, 1000);
		}
		await writer;
		var last = buffer.Drain();
		delivered += last.Entries.Count;
		dropped += last.Dropped;
		Assert.Equal(Total, delivered + dropped);
	}
}

public class ConsoleEntriesTests
{
	static IReadOnlyList<ConsoleEntry> Lines(int from, int count) =>
		Enumerable.Range(from, count).Select(k => new ConsoleEntry(ConsoleEntryKind.Text, k.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();

	[Fact]
	public void SmallBatchesAreAddedLineByLine()
	{
		var entries = new ConsoleEntries(maxCount: 1000);
		var actions = new List<NotifyCollectionChangedAction>();
		entries.CollectionChanged += (_, e) => actions.Add(e.Action);
		entries.AddRange(Lines(0, 3));
		Assert.Equal([NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add], actions);
		Assert.Equal(3, entries.Count);
	}

	[Fact]
	public void LargeBatchesReplaceTheDisplayOnceAndKeepTheNewestLines()
	{
		var entries = new ConsoleEntries(maxCount: 1000);
		entries.AddRange(Lines(0, 900));
		var actions = new List<NotifyCollectionChangedAction>();
		entries.CollectionChanged += (_, e) => actions.Add(e.Action);
		entries.AddRange(Lines(900, 500));
		Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
		Assert.Equal(1000, entries.Count);
		Assert.Equal("400", entries[0].Text);
		Assert.Equal("1399", entries[^1].Text);
	}

	[Fact]
	public void AHugeBatchKeepsOnlyItsLastLines()
	{
		var entries = new ConsoleEntries(maxCount: 100);
		entries.AddRange(Lines(0, 1_000_000));
		Assert.Equal(100, entries.Count);
		Assert.Equal("999900", entries[0].Text);
		Assert.Equal("999999", entries[^1].Text);
	}

	[Fact]
	public void SingleLinesBeyondTheLimitTrimTheOldest()
	{
		var entries = new ConsoleEntries(maxCount: 5);
		for (int k = 0; k < 8; k++)
			entries.AddRange(Lines(k, 1));
		Assert.Equal(["3", "4", "5", "6", "7"], entries.Select(e => e.Text));
	}

	static ConsoleEntry Long(char c, int length) => new(ConsoleEntryKind.Text, new string(c, length));

	[Fact]
	public void LongLinesAreTrimmedByCharacters()
	{
		// Each line is a printed matrix of 1000 characters; the limit allows 3 of them.
		var entries = new ConsoleEntries(maxCount: 1000, maxCharacters: 3500);
		foreach (var c in "abcde")
			entries.AddRange([Long(c, 1000)]);
		Assert.Equal("cde", string.Concat(entries.Select(e => e.Text[0])));
		Assert.Equal(3000, entries.Characters);
	}

	[Fact]
	public void ALargeBatchKeepsOnlyTheNewestLinesThatFit()
	{
		var entries = new ConsoleEntries(maxCount: 1000, maxCharacters: 3500);
		entries.AddRange([.. "abcdefghij".Select(c => Long(c, 1000))]);
		Assert.Equal("hij", string.Concat(entries.Select(e => e.Text[0])));
		Assert.Equal(3000, entries.Characters);
	}

	[Fact]
	public void ALineLongerThanTheLimitIsStillShownAlone()
	{
		var entries = new ConsoleEntries(maxCount: 1000, maxCharacters: 500);
		entries.AddRange(Lines(0, 3));
		entries.AddRange([Long('x', 2000)]);
		Assert.Equal('x', Assert.Single(entries).Text[0]);
		Assert.Equal(2000, entries.Characters);
	}

	[Fact]
	public void ClearingResetsTheCharacterCount()
	{
		var entries = new ConsoleEntries(maxCount: 1000, maxCharacters: 3500);
		entries.AddRange([Long('a', 1000)]);
		entries.Clear();
		Assert.Equal(0, entries.Characters);
		entries.AddRange([Long('b', 1000), Long('c', 1000), Long('d', 1000)]);
		Assert.Equal(3000, entries.Characters);
	}
}
