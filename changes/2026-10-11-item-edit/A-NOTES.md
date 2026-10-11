# Acceptance test notes — Change20261011ItemEditA

## Statements and their test coverage

| # | Statement | Status |
|---|-----------|--------|
| 1 | Title required: 1 to 200 chars after trimming | *tested* — `Blank_title_returns_400`, `Title_over_200_chars_returns_400`, `Title_of_1_char_is_accepted`, `Title_of_200_chars_is_accepted`, `Title_valid_only_after_trimming_is_accepted` |
| 2 | Blank or missing title is refused; nothing changes | *tested* — `Blank_title_returns_400`, `After_blank_title_item_is_unchanged` |
| 3 | Note is optional; null or omitted clears it | *tested* — `Null_note_clears_it`, `Omitting_note_clears_it` |
| 4 | Note up to 2000 chars | *tested* — `Note_of_2000_chars_is_accepted`, `Note_over_2000_chars_returns_400` |
| 5 | Priority is optional; null or omitted clears it | *tested* — `Null_priority_clears_it`, `Omitting_priority_clears_it` |
| 6 | Priority, if given, must be 1–5 | *tested* — `Priority_0_returns_400`, `Priority_6_returns_400`, `Priority_1_is_accepted`, `Priority_5_is_accepted` |
| 7 | Done status is not affected | *tested* — `Done_status_not_affected_by_edit` |
| 8 | Due date is not affected | *tested* — `Due_date_not_affected_by_edit` |
| 9 | Unknown item → 404 | *tested* — `Unknown_item_returns_404` |
| 10 | Invalid input → 400; nothing changes | *tested* — `Blank_title_returns_400`, `Title_over_200_chars_returns_400`, `Note_over_2000_chars_returns_400`, `Priority_0_returns_400`, `Priority_6_returns_400`, `After_blank_title_item_is_unchanged` |
| 11 | Returns 200 with updated item in same shape as GET /items | *tested* — `Corrected_title_note_and_priority_are_returned` (id, title, note, priority checked) |

## Not tested (ambiguous readings)

**Whether the title is stored trimmed or only validated after trimming.**

- Reading A: the server trims the title and stores the trimmed value (e.g. " xxxx" → stored as "xxxx").
- Reading B: the server trims only for validation but stores the value as sent (e.g. " xxxx" → stored as " xxxx").

`Title_valid_only_after_trimming_is_accepted` only asserts the request succeeds (HTTP 200), not what value is stored, because both readings are consistent with the request wording "1 to 200 characters after trimming spaces."
