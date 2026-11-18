namespace Heliograph.Pdf.Layout;

/// <summary>
/// The lists open at a point in the body, for the structure a screen reader follows: a list item goes in the open list
/// at its level, or in a new list inside the body of the item above it.
/// </summary>
internal sealed class ListNesting(StructNode root)
{
    // Each level's list and the body of its latest item, deepest last.
    private readonly Stack<(int Level, StructNode List, StructNode? Body)> _open = new();

    public Owner Item(int level)
    {
        while (_open.Count > 0 && _open.Peek().Level > level)
        {
            _open.Pop();
        }

        if (_open.Count == 0 || _open.Peek().Level < level)
        {
            var parent = _open.Count == 0 ? root : _open.Peek().Body ?? _open.Peek().List;
            _open.Push((level, parent.Add("L"), null));
        }

        var (at, list, _) = _open.Pop();
        var item = list.Add("LI");
        var label = item.Add("Lbl");
        var body = item.Add("LBody");
        _open.Push((at, list, body));
        return new Owner(body, label);
    }

    /// <summary>Something other than a list item closes every open list.</summary>
    public void Clear() => _open.Clear();
}
