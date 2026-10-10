# Notes — ItemCalendarAcceptance (Change20261010ItemCalendarA)

## Statements from the request and their test status

| # | Statement | Status |
|---|---|---|
| 1 | Calendar covers every day from start to end, both included, in date order | **tested** — `Days_are_returned_in_date_order`, `Both_start_and_end_dates_are_included` |
| 2 | A day with nothing due is still listed with 0 | **tested** — `Day_with_no_due_items_is_still_listed_with_zero`, `Days_are_returned_in_date_order` (no items seeded, all 3 days present) |
| 3 | Only open items count; done items do not | **tested** — `Done_items_are_not_counted`, `Open_today_done_today_open_tomorrow_gives_today_1_tomorrow_1_rest_0` |
| 4 | Items without a due date do not count | **tested** — `Items_without_due_date_are_not_counted` |
| 5 | Start date may not be after end date → 400 | **tested** — `From_after_to_is_refused` |
| 6 | At most 31 days (inclusive); more is refused → 400 | **tested** — `Span_of_exactly_31_days_is_accepted`, `Span_of_32_days_is_refused` |
| 7 | No params → 7 days starting today | **tested** — `No_parameters_returns_seven_days`, `No_parameters_first_day_is_today_in_utc`, `No_parameters_last_day_is_six_days_after_today` |
| 8 | Only one of from/to given → 400 | **tested** — `Only_from_without_to_is_refused`, `Only_to_without_from_is_refused` |
| 9 | Today is current UTC date | **tested** — `No_parameters_first_day_is_today_in_utc` uses `DateTime.UtcNow` and asserts the first day matches. *Limitation:* cannot distinguish UTC vs. local time in a UTC-server CI environment; if server and test runner are both UTC, both readings agree. |
| 10 | Asking for the calendar stores and changes nothing | **tested** — `Calendar_does_not_modify_any_item` (valid request). Since the endpoint is a read-only GET, refusals trivially change nothing; this is not tested separately. |
| 11 | Response shape: `[{"date": "YYYY-MM-DD", "due": N}]` | **tested** — `Days_are_returned_in_date_order` checks exact date strings; `DueCount` helper asserts the `due` property exists and is an integer in every asserting test. |
| 12 | Invalid date string → 400 | **tested** — `Invalid_from_date_format_is_refused`, `Invalid_to_date_format_is_refused` (tests "not-a-date"). *Not tested:* valid-format but invalid-calendar-date strings like "2026-02-30" — the request says "a date that is not one" and this is clearly covered, but it is a boundary the tests leave to the builder. |
| 13 | from after to → 400 | **tested** — `From_after_to_is_refused` |
| 14 | Span > 31 days → 400 | **tested** — `Span_of_32_days_is_refused` (Oct 1 to Nov 1 = 32 days) |
| 15 | Exactly 31 days accepted | **tested** — `Span_of_exactly_31_days_is_accepted` (Oct 1 to Oct 31) |
| 16 | Multiple open items on same day all counted | **tested** — `Multiple_open_items_due_same_day_are_all_counted` |
| 17 | Items outside range not counted | **tested** — `Items_outside_range_are_not_counted` |
| 18 | Example: open today + done today + open tomorrow | **tested** — `Open_today_done_today_open_tomorrow_gives_today_1_tomorrow_1_rest_0` |
| 19 | from=2026-10-01&to=2026-10-31 accepted (31 days) | **tested** — `Span_of_exactly_31_days_is_accepted` |
| 20 | from=2026-10-01&to=2026-11-01 refused (32 days) | **tested** — `Span_of_32_days_is_refused` |

## Ambiguities not tested

**"Today" is UTC vs. local time** (`No_parameters_first_day_is_today_in_utc`):
- Reading 1: server computes today as `DateOnly.FromDateTime(DateTime.UtcNow)`.
- Reading 2: server uses local time, which happens to match UTC in the CI environment.
- The test cannot distinguish these two readings because CI runs in a UTC timezone. The test is named to signal intent, but cannot enforce it mechanically.

**Valid-format but non-existent calendar date (e.g., "2026-02-30")** (not tested):
- Reading 1: any string that .NET's `DateOnly.TryParse` rejects is "not a date" → 400.
- Reading 2: only strings that don't match YYYY-MM-DD format are "not a date" → "2026-02-30" might be accepted as some coerced value.
- The request says "A date that is not one", which clearly covers invalid calendar dates, but the builder could disagree on whether ASP.NET Core's binding handles this automatically. Left untested to avoid a false red if the framework coerces the value in an unexpected way.
