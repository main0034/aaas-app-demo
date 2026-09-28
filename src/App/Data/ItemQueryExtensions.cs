namespace App.Data;

public static class ItemQueryExtensions
{
    public static IQueryable<Item> WhereOpen(this IQueryable<Item> source) =>
        source.Where(i => !i.IsDone);

    public static IQueryable<Item> WhereSearch(this IQueryable<Item> source, string term) =>
        source.Where(i =>
            i.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (i.Note != null && i.Note.Contains(term, StringComparison.OrdinalIgnoreCase)));
}
