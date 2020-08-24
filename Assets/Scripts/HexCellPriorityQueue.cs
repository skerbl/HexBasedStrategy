using System.Collections.Generic;

/// <summary>
/// Since there's no standard implementatio of a Priority Queue in
/// System.Collections, a custom one is required.
/// </summary>
public class HexCellPriorityQueue
{
	private List<HexCell> list = new List<HexCell>();
	private int count = 0;
	private int minimum = int.MaxValue;

	public int Count
	{
		get
		{
			return count;
		}
	}

	public void Enqueue(HexCell cell)
	{
		count += 1;
		int priority = cell.SearchPriority;
		if (priority < minimum)
		{
			minimum = priority;
		}

		while (priority >= list.Count)
		{
			// Pad the list with dummy elements
			list.Add(null);
		}

		cell.NextWithSamePriority = list[priority];
		list[priority] = cell;
	}

	public HexCell Dequeue()
	{
		count -= 1;
		for (; minimum < list.Count; minimum++)
		{
			HexCell cell = list[minimum];
			if (cell != null)
			{
				list[minimum] = cell.NextWithSamePriority;
				return cell;
			}
		}
		return null;
	}

	public void Change(HexCell cell, int oldPriority)
	{
		HexCell current = list[oldPriority];
		HexCell next = current.NextWithSamePriority;
		if (current == cell)
		{
			// Simply cut the head from the list
			list[oldPriority] = next;
		}
		else
		{
			while (next != cell)
			{
				current = next;
				next = current.NextWithSamePriority;
			}

			// Skip the cell to remove it from the list
			current.NextWithSamePriority = cell.NextWithSamePriority;
		}

		// Add it to the queue again, decrement the counter because Enqueue() will increment it
		Enqueue(cell);
		count -= 1;
	}

	public void Clear()
	{
		count = 0;
		list.Clear();
		minimum = int.MaxValue;
	}
}