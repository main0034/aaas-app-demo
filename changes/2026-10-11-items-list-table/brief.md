# Items list as a table

## What and why

On the Items page the list of items should become a table with columns for title, priority, due date, and a snippet of the note, so the key information is visible at a glance without opening each item.

## Rules

- The table has four columns: Title, Priority, Due date, Note.
- The Title column shows each item's title as a link to its page, exactly as the current list does.
- If an item is marked done, the word "done" appears next to its title in the Title column, as the current list shows it.
- The Priority column shows the item's priority number (1–5). If the item has no priority, it shows "—".
- The Due date column shows the item's due date in the format YYYY-MM-DD. If the item has no due date, it shows "—".
- The Note column shows up to 60 characters of the note. If the note is longer than 60 characters, show the first 60 characters followed by "…". If the item has no note, it shows "—".
- The order and content of the rows are unchanged from the current list.
- No data is stored or changed by viewing this page.

## Interface (fixed - build exactly this)

- `GET /items` returns `200` with a list of items, each with id, title, note, priority, dueDate, and isDone. This route already exists and is unchanged.

## Screen

On the "Items" page, the list of items is replaced by a table. Each row is one item. The columns are Title, Priority, Due date, and Note, with the header labels shown at the top of the table. The "Add item" form above the table is unchanged.

## Examples

- An item with title "Buy paint", priority 2, due date 2026-10-15, and note "Get the glossy kind from the hardware store on Main Street" (57 characters): the row shows "Buy paint" as a link, "2", "2026-10-15", and the full note (57 characters, under the limit).
- An item with title "Call dentist", no priority, no due date, no note: the row shows "Call dentist" as a link, "—", "—", "—".
- An item with a note that is 100 characters long: the Note column shows the first 60 characters followed by "…".
- An item marked done with title "Renew passport": the Title column shows "Renew passport - done".

## Not part of this change

- The order items appear in is not changed.
- No new columns beyond Title, Priority, Due date, and Note.
