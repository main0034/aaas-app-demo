# Notes — Change20261010ItemSummary

Every statement from the request, marked tested or not tested.

## Tested

1. `GET /items/summary` returns 200 — *Empty_database_returns_all_zeros*
2. Response has correct JSON shape (open, overdue, dueSoon, done, byPriority) — *Empty_database_returns_all_zeros*
3. Open count is items not marked done — *Open_count_is_items_not_done*
4. Done count is items marked done — *Open_count_is_items_not_done*
5. Item with due date before today is overdue — *Item_due_before_today_is_overdue*
6. Item due today is due soon, not overdue (boundary: today inclusive for due soon) — *Item_due_today_is_due_soon_not_overdue*
7. Item due today+7 is due soon (upper boundary included) — *Item_due_seven_days_from_today_is_due_soon*
8. Item due today+8 is not due soon (upper boundary excluded) — *Item_due_eight_days_from_today_is_not_due_soon*
9. Open items without a due date are neither overdue nor due soon — *Item_without_due_date_is_neither_overdue_nor_due_soon*
10. An overdue item is not also counted as due soon — *Overdue_item_is_not_counted_as_due_soon*
11. Done items are excluded from open, overdue, and due soon counts — *Done_item_is_excluded_from_open_overdue_and_due_soon*
12. byPriority counts open items at each priority 1–5 — *By_priority_counts_open_items_at_each_priority*
13. byPriority "none" counts open items without priority — *By_priority_none_counts_open_items_without_priority*
14. Every priority key (1–5 and "none") appears even when 0 — *Every_priority_key_is_present_even_when_zero*
15. Done items are not counted in byPriority — *By_priority_excludes_done_items*
16. q=term narrows open count — *Search_narrows_open_count*
17. q=term narrows done count — *Search_narrows_done_count*
18. q=term narrows overdue count — *Search_narrows_overdue_count*
19. q=term narrows due soon count — *Search_narrows_due_soon_count*
20. q=term narrows byPriority counts — *Search_narrows_by_priority*
21. Matching is case-insensitive — *Search_is_case_insensitive*
22. Matching applies to the note field — *Search_matches_note_field*
23. Empty q means no narrowing — *Empty_q_means_no_narrowing*
24. Whitespace-only q means no narrowing — *Whitespace_q_means_no_narrowing*
25. q of exactly 100 characters returns 200 — *Q_of_exactly_100_characters_is_accepted*
26. q longer than 100 characters returns 400 — *Q_longer_than_100_characters_returns_400*
27. Nothing is stored or changed by asking for the summary — *Summary_does_not_modify_any_item*

## Not tested

None. All statements in the request are unambiguous and testable.

---

### Borderline readings noted (resolved as unambiguous)

- "due today or on one of the next 7 days, so the last day counted is today + 7" — the parenthetical
  makes the upper bound explicit; today+7 is in, today+8 is out. Both boundaries are tested.

- "the same matching GET /items?q= already does" — the existing search matches title or note,
  case-insensitively, trimming the term. The tests confirm case-insensitivity and note matching.
  The trim-before-matching behaviour for `q` (whitespace-only = no narrowing) is tested via
  *Whitespace_q_means_no_narrowing*.
