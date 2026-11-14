using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MLCalc.Wpf;

/// <summary>
/// A bounded queue between the interpreter, which writes output on a worker thread, and the
/// console, which shows it on the UI thread. A program can produce output much faster than the
/// console can display it (millions of lines per second), so the queue holds at most
/// <see cref="Capacity"/> lines and <see cref="MaxCharacters"/> characters; further lines are
/// dropped and counted, and the console says how many were not shown. Without the limits, a
/// printing loop could fill gigabytes of memory. Both limits are needed: one line can be a whole
/// matrix of hundreds of kilobytes.
/// </summary>
public sealed class ConsoleOutputBuffer(int capacity, long maxCharacters = long.MaxValue)
{
	/// <summary>A queued "clear the console" request (<c>cls</c>); null cannot be a normal entry.</summary>
	static readonly ConsoleEntry ClearMarker = new(ConsoleEntryKind.Info, "");

	readonly ConcurrentQueue<ConsoleEntry> queue = new();
	int count;
	long characters;
	long dropped;

	public int Capacity { get; } = capacity;

	public long MaxCharacters { get; } = maxCharacters;

	/// <summary>Queues an entry, or drops it when the queue is full. Safe to call from any thread.</summary>
	public void Add(ConsoleEntry entry)
	{
		int size = entry.Text.Length;
		bool full = Interlocked.Increment(ref count) > Capacity;
		full |= Interlocked.Add(ref characters, size) > MaxCharacters;
		if (full)
		{
			Interlocked.Decrement(ref count);
			Interlocked.Add(ref characters, -size);
			Interlocked.Increment(ref dropped);
			return;
		}
		queue.Enqueue(entry);
	}

	/// <summary>
	/// Queues a request to clear the console. Everything queued before it would be cleared anyway,
	/// so it is discarded now; that also keeps a loop that calls <c>cls</c> from filling the queue.
	/// </summary>
	public void Clear()
	{
		while (queue.TryDequeue(out var entry))
			Release(entry);
		Interlocked.Exchange(ref dropped, 0);
		Interlocked.Increment(ref count);
		queue.Enqueue(ClearMarker);
	}

	/// <summary>
	/// Takes what is queued, at most <see cref="Capacity"/> entries: the writer keeps adding while
	/// this runs, and without the limit one call could collect an unbounded batch. Anything left is
	/// taken by the next call. Called on the UI thread.
	/// </summary>
	public (bool Cleared, List<ConsoleEntry> Entries, long Dropped) Drain()
	{
		bool cleared = false;
		var entries = new List<ConsoleEntry>();
		for (int taken = 0; taken < Capacity && queue.TryDequeue(out var entry); taken++)
		{
			Release(entry);
			if (ReferenceEquals(entry, ClearMarker))
			{
				cleared = true;
				entries.Clear();
			}
			else
				entries.Add(entry);
		}
		return (cleared, entries, Interlocked.Exchange(ref dropped, 0));
	}

	void Release(ConsoleEntry entry)
	{
		Interlocked.Decrement(ref count);
		Interlocked.Add(ref characters, -entry.Text.Length);
	}
}

/// <summary>
/// The console's lines, keeping at most <see cref="MaxCount"/> lines and about
/// <see cref="MaxCharacters"/> characters (the newest line is always kept). Lines are added in
/// batches: a small batch raises one notification per line (which keeps the selection), a large one
/// replaces the display at once, and old lines are trimmed in one step instead of one at a time.
/// </summary>
public sealed class ConsoleEntries(int maxCount, long maxCharacters = long.MaxValue) : ObservableCollection<ConsoleEntry>
{
	/// <summary>Batches up to this size are added line by line.</summary>
	const int SmallBatch = 100;

	public int MaxCount { get; } = maxCount;

	public long MaxCharacters { get; } = maxCharacters;

	/// <summary>The total length of the lines' text.</summary>
	public long Characters { get; private set; }

	public void AddRange(IReadOnlyList<ConsoleEntry> entries)
	{
		if (entries.Count == 0)
			return;
		long added = 0;
		foreach (var entry in entries)
			added += entry.Text.Length;
		if (entries.Count <= SmallBatch && Count + entries.Count <= MaxCount && Characters + added <= MaxCharacters)
		{
			foreach (var entry in entries)
				Add(entry);
			return;
		}

		// Only the newest lines can be kept, so skip the rest of a huge batch up front.
		var items = (List<ConsoleEntry>)Items;
		int start = entries.Count - 1;
		long kept = entries[start].Text.Length;
		while (start > 0 && entries.Count - start < MaxCount && kept + entries[start - 1].Text.Length <= MaxCharacters)
			kept += entries[--start].Text.Length;
		for (int i = start; i < entries.Count; i++)
			items.Add(entries[i]);
		Characters += kept;

		// Then trim the oldest lines.
		int excess = 0;
		while (items.Count - excess > MaxCount || (Characters > MaxCharacters && items.Count - excess > 1))
			Characters -= items[excess++].Text.Length;
		if (excess > 0)
			items.RemoveRange(0, excess);

		OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
		OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	protected override void InsertItem(int index, ConsoleEntry item)
	{
		base.InsertItem(index, item);
		Characters += item.Text.Length;
	}

	protected override void SetItem(int index, ConsoleEntry item)
	{
		Characters += item.Text.Length - this[index].Text.Length;
		base.SetItem(index, item);
	}

	protected override void RemoveItem(int index)
	{
		Characters -= this[index].Text.Length;
		base.RemoveItem(index);
	}

	protected override void ClearItems()
	{
		base.ClearItems();
		Characters = 0;
	}
}
