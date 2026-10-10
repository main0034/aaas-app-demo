# Acceptance notes — Change20261010Projects

## Statement coverage

### POST /projects

| Statement | Status | Tests |
|---|---|---|
| Returns 201 with `{id, name}` | tested | `Create_project_returns_201_with_id_and_name` |
| Name stored trimmed | tested | `Create_project_stores_name_trimmed` |
| Name minimum 1 char after trimming | tested | `Create_project_name_one_char_after_trimming_is_accepted` |
| Name maximum 100 chars after trimming | tested | `Create_project_name_100_chars_after_trimming_is_accepted` |
| 0 chars after trimming → 400 | tested | `Create_project_empty_name_after_trimming_returns_400` |
| 101 chars after trimming → 400 | tested | `Create_project_name_101_chars_returns_400` |
| Duplicate name same case → 409 | tested | `Create_project_duplicate_name_same_case_returns_409` |
| Duplicate name different case → 409 | tested | `Create_project_duplicate_name_different_case_returns_409` |
| After 409, no project created | tested | `After_rejected_duplicate_name_no_project_created` |

### PUT /items/{id}/project

| Statement | Status | Tests |
|---|---|---|
| Assign returns 200 with item | tested | `Assign_item_to_project_returns_200_with_item` |
| Null removes item from project, returns 200 | tested | `Remove_item_from_project_with_null_returns_200_with_item` |
| Unknown item → 404 | tested | `Assign_item_unknown_item_returns_404` |
| Unknown project → 400 | tested | `Assign_item_unknown_project_returns_400` |
| Item moved to another project leaves the old one | tested | `Move_item_to_different_project_removes_it_from_old_project` |

### GET /projects

| Statement | Status | Tests |
|---|---|---|
| Returns 200 sorted by name, case-insensitive | tested | `Get_projects_sorted_by_name_case_insensitive` |
| total, done, percentDone fields present and correct | tested | `Get_projects_total_done_and_percentDone_are_correct` |
| percentDone is rounded down (floor), not rounded nearest | tested | `Get_projects_percentDone_rounds_down_not_nearest` |
| Project with no items: percentDone = 0 | tested | `Get_projects_no_items_is_zero_percent` |

### GET /projects/{id}

| Statement | Status | Tests |
|---|---|---|
| Unknown project → 404 | tested | `Get_project_unknown_returns_404` |
| Returns same fields as GET /projects plus items list | tested | `Get_project_returns_stats_and_items_in_item_shape` |
| Items have same shape as GET /items (id, title, isDone checked) | tested | `Get_project_returns_stats_and_items_in_item_shape` |
| Open items first, then done items; newest-first within each group | tested | `Get_project_open_items_before_done_newest_first_within_group` |

### DELETE /projects/{id}

| Statement | Status | Tests |
|---|---|---|
| Empty project → 204 | tested | `Delete_project_with_no_items_returns_204` |
| Project with items → 409 | tested | `Delete_project_with_items_returns_409` |
| Project with only done items → 409 | tested | `Delete_project_with_only_done_items_returns_409` |
| Unknown project → 404 | tested | `Delete_project_unknown_returns_404` |
| After 409, project still exists | tested | `After_delete_rejected_project_still_exists` |

## Untested statements

**"Existing items and their data must survive the change."**

Not testable black-box: each acceptance test starts with an empty database created fresh for that run. Migration correctness (that existing rows survive a schema change) cannot be asserted through the HTTP interface in a clean-slate test.

## Ambiguities

None that affected test design. One near-ambiguity was resolved by direct reading:

**"A project can be deleted only when it has no items" — does "no items" mean no items at all, or no open items?**

Reading A: no items at all (done items still count).
Reading B: no open items (done items do not count as blocking).

The statement says "no items", not "no open items". Reading A was taken as the correct reading and is tested by `Delete_project_with_only_done_items_returns_409`.
