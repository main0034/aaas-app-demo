# Edit an item's title, note and priority

## What and why

We want to be able to go back and change an item's title, note, or priority after it has been saved, and to clear the note or priority if they are no longer needed.

## Rules

- The title is required: 1 to 200 characters after trimming spaces. A blank or missing title is refused, and nothing changes.
- The note is optional. Sending no note (or null) clears it. Up to 2000 characters.
- The priority is optional. Sending no priority (or null) clears it. If given, it must be 1, 2, 3, 4, or 5.
- The item's done status and due date are not affected by this change.
- If the item does not exist, the request is refused with not found.
- If the input is invalid, the request is refused and nothing changes.
- Existing items keep all their current data unless this route changes them.

## Interface (fixed - build exactly this)

- `PUT /items/{id}` with body `{"title": "Buy paint", "note": "Gloss only", "priority": 2}` returns `200` with the updated item in the same shape `GET /items` returns them. `note` and `priority` may be `null` or omitted to clear them. Unknown item: `404`. Invalid input (title blank, title over 200 characters, note over 2000 characters, priority outside 1–5): `400`.

## Examples

- An item has title "Buy piant", note "any colour", priority 3. Send title "Buy paint", note "Gloss only", priority 2 — the item comes back with the corrected title, updated note, and new priority.
- An item has priority 1. Send title "Call dentist", no priority — the item comes back with no priority set.
- An item has a note. Send title "Call dentist", no note — the item comes back with the note cleared.
- An item is marked done with a due date. Edit the title only — done status and due date are unchanged.
- Send a blank title — refused, and the item is unchanged.
