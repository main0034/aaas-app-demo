# Notes — Change20261011ItemsListTableA: Items list as a table

## Statements from the request and their test status

### Tested

| Statement | Test(s) |
|---|---|
| `GET /items` returns 200 | `Get_items_returns_200_with_all_required_fields` |
| Response includes id, title, note, priority, dueDate, isDone fields | `Get_items_returns_200_with_all_required_fields` |
| Items with all optional fields set return their correct values | `Item_with_all_optional_fields_set_returns_their_values` |
| Items with no optional fields return null for note, priority, dueDate | `Item_with_no_optional_fields_returns_null_for_note_priority_and_dueDate` |
| Done items have isDone=true; open items have isDone=false | `Done_item_has_isDone_true_and_open_item_has_isDone_false` |
| No data is stored or changed by viewing the list | `Viewing_the_list_does_not_change_any_item` |
| The full note text is returned by the API (screen does the truncation, not the route) | `Full_note_text_is_returned_untruncated` |
| The order of items is unchanged (newest first) | `Items_are_returned_newest_first` |

### Not tested — screen-only rules

These rules appear in the **Rules** section but describe what the user sees in the HTML table,
not what the route returns. The builder tests the page itself; these are not testable via
the `GET /items` route.

- **"The table has four columns: Title, Priority, Due date, Note."**
  Screen-only. The route returns JSON; the four-column table is rendered by the Razor page.

- **"The Title column shows each item's title as a link to its page."**
  Screen-only. Links are HTML, not part of the JSON response.

- **"If an item is marked done, the word 'done' appears next to its title in the Title column."**
  Screen-only. The route returns `isDone: true`; the "done" text is rendered by the page.

- **"The Priority column shows the item's priority number (1–5). If the item has no priority, it shows '—'."**
  Screen-only. The route returns `priority: null`; the "—" substitution is rendered by the page.
  The route behavior (returning null for absent priority) is tested in
  `Item_with_no_optional_fields_returns_null_for_note_priority_and_dueDate`.

- **"The Due date column shows the item's due date in the format YYYY-MM-DD. If the item has no due date, it shows '—'."**
  Screen-only. The "—" substitution is rendered by the page. The route's date format is
  verified as a side effect of `Item_with_all_optional_fields_set_returns_their_values`
  (exact "2026-10-20" string comparison).

- **"The Note column shows up to 60 characters of the note. If the note is longer than 60 characters, show the first 60 characters followed by '…'. If the item has no note, it shows '—'."**
  Screen-only. The 60-character truncation and "…" suffix are rendered by the page, not
  enforced by the route. The route must return the full note so the page can truncate it;
  this is verified by `Full_note_text_is_returned_untruncated`. The "—" for absent note is
  screen rendering; the route returning null is tested in
  `Item_with_no_optional_fields_returns_null_for_note_priority_and_dueDate`.
