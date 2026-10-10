# A calendar of what is due

## What and why

We want to see, day by day, how many open items are due over a stretch of time, so a
busy week is visible before it arrives.

## Rules

- The calendar covers every day from a start date to an end date, both included,
  in date order. A day with nothing due is still listed, with 0.
- Only open items count. Done items and items without a due date do not.
- The start date may not be after the end date, and the calendar covers at most 31
  days (start and end included). Anything else is refused, and nothing changes.
- If no dates are given, the calendar is today and the 6 days after it. If only
  one is given, it is refused.
- "Today" is the current date in UTC.
- Asking for the calendar stores and changes nothing.

## Interface (fixed - build exactly this)

- `GET /items/calendar?from=2026-10-12&to=2026-10-14` returns `200` with
  `[{"date": "2026-10-12", "due": 2}, {"date": "2026-10-13", "due": 0}, {"date": "2026-10-14", "due": 1}]`.
- `GET /items/calendar` returns the 7 days starting today, in the same shape.
- Dates are `YYYY-MM-DD`. A date that is not one, `from` after `to`, a span of more
  than 31 days, or only one of `from` and `to`: `400`.

## Examples

- Items due today (open), today (done) and tomorrow (open); `GET /items/calendar`
  returns 7 days: today 1, tomorrow 1, the other five 0.
- `from=2026-10-01&to=2026-10-31` is accepted (31 days); `to=2026-11-01` is refused.

## Not part of this change

- No listing of the items themselves; `GET /items/upcoming` is a separate request.
