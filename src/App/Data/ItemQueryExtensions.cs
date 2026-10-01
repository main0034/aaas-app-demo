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

    // Keeps only items with a priority at or above (numerically <=) maxPriority.
    // Unprioritised items are excluded because "priority 2 or better" has no
    // meaning for an item that has no priority at all.
    public static IQueryable<Item> WherePriorityAtMost(this IQueryable<Item> source, int maxPriority) =>
        source.Where(i => i.Priority != null && i.Priority <= maxPriority);

    // Priority 1 first, then 2…5, then unprioritised. Ties broken newest first.
    public static IOrderedQueryable<Item> OrderByPriority(this IQueryable<Item> source) =>
        source.OrderBy(i => i.Priority == null ? 1 : 0)
              .ThenBy(i => i.Priority)
              .ThenByDescending(i => i.Id);
}
