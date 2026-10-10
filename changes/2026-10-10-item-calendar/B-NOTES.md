# Notes — Item Calendar (Change20261010ItemCalendarB)

## Statements from the request and their test status

### Tested

1. "The calendar covers every day from a start date to an end date, both included, in date order."
   - *tested* by `Calendar_lists_every_day_in_range_in_order` (verifies exact date sequence)
   - *tested* by `Start_and_end_dates_are_both_included_in_range` (verifies boundary dates are counted)

2. "A day with nothing due is still listed, with 0."
   - *tested* by `Day_with_no_due_items_is_listed_with_zero`
   - *tested* by `Open_items_due_on_a_date_are_counted` (middle day of a 3-day range has 0)

3. "Only open items count. Done items and items without a due date do not."
   - *tested* by `Done_item_is_not_counted`
   - *tested* by `Item_without_due_date_is_not_counted`

4. "Items due outside the requested range are not counted."
   (implied by the per-day semantics)
   - *tested* by `Item_due_outside_range_is_not_counted`

5. "The start date may not be after the end date ... Anything else is refused."
   - *tested* by `From_after_to_returns_400`

6. "The calendar covers at most 31 days (start and end included). Anything else is refused."
   - *tested* by `Span_of_exactly_31_days_is_accepted` (boundary in)
   - *tested* by `Span_of_32_days_returns_400` (boundary out, exact dates from the example)
   - *tested* by `Single_day_span_is_accepted` (minimum — 1 day)

7. "If no dates are given, the calendar is today and the 6 days after it."
   - *tested* by `No_dates_returns_seven_days_starting_today` (7 entries, first=today, last=today+6)
   - *tested* by `Default_calendar_counts_open_items_due_today_and_tomorrow_correctly`
     (the example: open today=1, done today not counted, open tomorrow=1, rest=0)

8. "If only one is given, it is refused."
   - *tested* by `Only_from_provided_returns_400`
   - *tested* by `Only_to_provided_returns_400`

9. "'Today' is the current date in UTC."
   - *tested* by `No_dates_returns_seven_days_starting_today` (uses `DateTime.UtcNow`)

10. "A date that is not one ... : 400."
    - *tested* by `Invalid_from_date_format_returns_400`
    - *tested* by `Invalid_to_date_format_returns_400`

11. "Asking for the calendar stores and changes nothing."
    - *tested* by `Getting_calendar_does_not_change_items`

12. Response shape: each element has a `date` (YYYY-MM-DD string) and `due` (integer) field.
    - *tested* by `Calendar_lists_every_day_in_range_in_order` (reads `date` property as string)
    - *tested* by all count assertions (read `due` property via `DueOnDate` helper)

### Not tested

None. All unambiguous statements in the request have at least one test.

## Total tests

17 tests.
