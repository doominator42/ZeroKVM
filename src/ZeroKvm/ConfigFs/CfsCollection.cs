using System.Collections;

namespace ZeroKvm.ConfigFs;

internal class CfsCollection<T> : ICollection<T>
    where T : CfsBase
{
    private readonly List<T> _items = new();

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(T item)
    {
        item.EnsureCreated();
        _items.Add(item);
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        item.EnsureDeleted();
        _items.RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        T item = _items[index];
        item.EnsureDeleted();
        _items.RemoveAt(index);
    }

    public void Clear()
    {
        List<T> items = _items;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            items[i].EnsureDeleted();
            items.RemoveAt(i);
        }
    }

    public bool Contains(T item) => _items.Contains(item);

    public int IndexOf(T item)
    {
        List<T> items = _items;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == item)
            {
                return i;
            }
        }

        return -1;
    }

    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
}
