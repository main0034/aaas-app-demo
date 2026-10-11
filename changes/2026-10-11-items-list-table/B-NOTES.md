# Notes — Change20261011ItemsListTableB

## Statements from the request

### Tested

1. **GET /items returns 200 with a list of items.**
   Tested in: `Get_returns_200`

2. **Each item in the response includes the fields note, priority, dueDate, and isDone.**
   Tested in: `Response_includes_note_priority_due_date_and_is_done_fields`

3. **The note field is returned in full — not truncated at the 60-character display limit.**
   The request says to show only 60 chars in the Note column, but the Interface says the route
   is unchanged. Truncation must happen in the screen layer. If a builder truncates at the API
   level instead, this test catches it.
   Tested in: `Note_is_returned_in_full_not_truncated_at_display_limit`

4. **The note field is null when the item has no note.**
   Tested in: `Note_is_null_when_not_set`

5. **The priority field is returned as a number when set.**
   Tested in: `Priority_is_returned_as_a_number`

6. **The priority field is null when not set.**
   Tested in: `Priority_is_null_when_not_set`

7. **The dueDate field is returned as a YYYY-MM-DD string when set.**
   Tested in: `Due_date_is_returned_in_yyyy_mm_dd_format`

8. **The dueDate field is null when not set.**
   Tested in: `Due_date_is_null_when_not_set`

9. **isDone is false for open items.**
   Tested in: `Is_done_is_false_for_open_items`

10. **isDone is true for done items.**
    Tested in: `Is_done_is_true_for_done_items`

### Not tested

These statements are in the **Screen** section or are screen display rules; they describe how
the Blazor component renders the data, not what the API returns:

- **"The table has four columns: Title, Priority, Due date, Note."** — screen layout only.

- **"The Title column shows each item's title as a link to its page."** — screen rendering only.

- **"If an item is marked done, the word 'done' appears next to its title in the Title column."**
  The API isDone field is tested (statements 9–10). The display text is a screen rule.

- **"The Priority column shows '—' when the item has no priority."** — screen rendering;
  the API returns null (statement 6 covers the API side).

- **"The Due date column shows the item's due date in the format YYYY-MM-DD."** — statement 7
  covers the API format. The column display is a screen rule.

- **"The Due date column shows '—' when no due date."** — screen rendering; API returns null
  (statement 8 covers the API side).

- **"The Note column shows up to 60 characters … followed by '…'."** — screen truncation rule.
  Statement 3 verifies the API does NOT truncate.

- **"The Note column shows '—' when no note."** — screen rendering; API returns null
  (statement 4 covers the API side).

- **"The order and content of the rows are unchanged from the current list."** — the Interface
  says the route is unchanged; existing endpoint tests already cover its order.

- **"No data is stored or changed by viewing this page."** — this is a screen rule about the
  page not making write calls. GET /items is inherently non-mutating; there is no new API
  behavior to test here.
