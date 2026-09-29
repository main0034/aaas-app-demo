namespace App.Data;

public static class ItemQueryExtensions
{
    public static IQueryable<Item> WhereOpen(this IQueryable<Item> source) =>
        source.Where(i => !i.IsDone);

    public static IQueryable<Item> WhereSearch(this IQueryable<Item> source, string term)
    {
        var lower = term.ToLower();
        return source.Where(i =>
            i.Title.ToLower().Contains(lower) ||
            (i.Note != null && i.Note.ToLower().Contains(lower)));
    }

    public static IQueryable<Item> WhereOverdue(this IQueryable<Item> source, DateOnly today) =>
        source.Where(i => !i.IsDone && i.DueDate != null && i.DueDate < today);
}
