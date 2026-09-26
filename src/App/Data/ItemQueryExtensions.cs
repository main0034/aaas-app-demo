namespace App.Data;

public static class ItemQueryExtensions
{
    public static IQueryable<Item> WhereOpen(this IQueryable<Item> source) =>
        source.Where(i => !i.IsDone);
}
