using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MLCalc.Wpf;

/// <summary>
/// A bounded queue between the interpreter, which writes output on a worker thread, and the
/// console, which shows it on the UI thread. A program can produce output much faster than the
/// console can display it (millions of lines per second), so the queue holds at most
/// <see cref="Capacity"/> lines; further lines are dropped and counted, and the console says how
/// many were not shown. Without the limit, a printing loop could fill gigabytes of memory.
/// </summary>
public sealed class ConsoleOutputBuffer(int capacity)
{
	/// <summary>A queued "clear the console" request (<c>cls</c>); null cannot be a normal entry.</summary>
	static readonly ConsoleEntry ClearMarker = new(ConsoleEntryKind.Info, "");

	readonly ConcurrentQueue<ConsoleEntry> queue = new();
	int count;
	long dropped;

	public int Capacity { get; } = capacity;

	/// <summary>Queues an entry, or drops it when the queue is full. Safe to call from any thread.</summary>
	public void Add(ConsoleEntry entry)
	{
		if (Interlocked.Increment(ref count) > Capacity)
		{
			Interlocked.Decrement(ref count);
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
		while (queue.TryDequeue(out _))
			Interlocked.Decrement(ref count);
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
			Interlocked.Decrement(ref count);
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
}

/// <summary>
/// The console's lines, keeping at most <see cref="MaxCount"/>. Lines are added in batches: a small
/// batch raises one notification per line (which keeps the selection), a large one replaces the
/// display at once, and old lines are trimmed in one step instead of one at a time.
/// </summary>
public sealed class ConsoleEntries(int maxCount) : ObservableCollection<ConsoleEntry>
{
	/// <summary>Batches up to this size are added line by line.</summary>
	const int SmallBatch = 100;

	public int MaxCount { get; } = maxCount;

	public void AddRange(IReadOnlyList<ConsoleEntry> entries)
	{
		if (entries.Count == 0)
			return;
		if (entries.Count <= SmallBatch && Count + entries.Count <= MaxCount)
		{
			foreach (var entry in entries)
				Add(entry);
			return;
		}

		// Only the last MaxCount lines can be kept, so skip the rest of a huge batch up front.
		var items = (List<ConsoleEntry>)Items;
		int skip = System.Math.Max(0, entries.Count - MaxCount);
		for (int i = skip; i < entries.Count; i++)
			items.Add(entries[i]);
		int excess = items.Count - MaxCount;
		if (excess > 0)
			items.RemoveRange(0, excess);

		OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
		OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}
}
