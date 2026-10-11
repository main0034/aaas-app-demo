# Acceptance test notes — Change20261011ItemEditB

## Statements from the request and their test status

### Tested

| Statement | Tests |
|---|---|
| Title required: 1–200 characters after trimming spaces (lower bound) | `Title_of_1_character_is_accepted` |
| Title required: 1–200 characters after trimming spaces (upper bound) | `Title_of_200_characters_is_accepted` |
| Title of 201 characters refused | `Title_of_201_characters_is_refused` |
| Trimming is applied (202 chars total, 200 after trimming, accepted) | `Title_with_spaces_at_200_characters_after_trimming_is_accepted` |
| Blank title (empty string) refused | `Blank_title_returns_400` |
| Spaces-only title refused (blank after trimming) | `Spaces_only_title_returns_400` |
| Null title refused (missing title) | `Null_title_returns_400` |
| After blank title, item is unchanged | `After_blank_title_item_is_unchanged` |
| Note optional: sending null clears it | `Sending_null_note_clears_it` |
| Note optional: omitting clears it | `Omitting_note_clears_it` |
| Note up to 2000 characters accepted | `Note_of_2000_characters_is_accepted` |
| Note of 2001 characters refused | `Note_of_2001_characters_is_refused` |
| Priority optional: sending null clears it | `Sending_null_priority_clears_it` |
| Priority optional: omitting clears it | `Omitting_priority_clears_it` |
| Priority 1 accepted (lower bound) | `Priority_1_is_accepted` |
| Priority 5 accepted (upper bound) | `Priority_5_is_accepted` |
| Priority 0 refused (below lower bound) | `Priority_0_is_refused` |
| Priority 6 refused (above upper bound) | `Priority_6_is_refused` |
| After invalid priority, item is unchanged | `After_invalid_priority_item_is_unchanged` |
| Done status not affected by edit | `Done_status_and_due_date_are_not_affected_by_edit` |
| Due date not affected by edit | `Done_status_and_due_date_are_not_affected_by_edit` |
| Unknown item returns 404 | `Unknown_item_returns_404` |
| Returns 200 with updated fields | `Updating_title_note_and_priority_returns_200_with_updated_fields` |
| Response has id matching the edited item (same shape as GET /items) | `Response_has_id_matching_the_edited_item` |

### Not tested (ambiguous)

1. **Whether the stored title is the trimmed value or as-sent.**
   The request says "1 to 200 characters after trimming spaces" but does not say the title is
   stored trimmed. Two readings: (1) trimming is applied before storing, so a title sent as
   `" Buy paint "` is saved as `"Buy paint"`; (2) trimming is applied for validation only, and
   the value is stored as received. Not tested: what value the GET response returns after a PUT
   with a title that has leading/trailing spaces.

2. **"Missing title" — absent field vs. null.**
   The request says "A blank or missing title is refused." Two readings: (1) "missing" means
   the `title` key is absent from the JSON body; (2) "missing" means `title` is either null or
   absent. The `Null_title_returns_400` test covers the null case. A body with the `title` key
   entirely omitted is not tested separately, as JSON validators typically treat omitted and
   null the same for required fields.
