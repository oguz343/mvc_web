# TeacherController Split Plan

`TeacherController.cs` currently owns routing, Firestore reads/writes, ownership checks, view-model mapping, and legacy normalization helpers in one large file. Split it only after characterization coverage is in place, because the controller preserves several legacy Firestore shapes and mojibake-compatible key paths.

## Current Responsibility Groups

- Page actions: dashboard, assignments, create/edit/delete assignment, submissions, evaluation, announcements.
- Teacher context: current session profile, teacher profile fallback lookup, assigned lessons.
- Ownership and security: assignment ownership, submission ownership, teacher identity matching.
- Data loading: teacher lessons, assignments, submissions, announcements.
- Write fan-out: assignment updates/deletes and submission evaluation across legacy collections.
- Mapping: assignment, lesson option, and submission view models.
- Legacy helpers: `GetText`, `FirstNonEmpty`, `OnlyDigits`, `NormalizeClassName`, `NormalizeKey`, date parsing, status parsing.

## Safe Extraction Order

1. Add tests around helper behavior before moving helpers:
   - `OnlyDigits`
   - `NormalizeClassName`
   - `NormalizeKey`
   - `GetText` fallback order
   - evaluated status normalization
2. Extract pure mapping helpers into a static mapper:
   - `BuildAssignmentViewModel`
   - `BuildLessonOptions`
   - `BuildSubmissionViewModels`
3. Extract teacher identity/ownership checks into a small policy service:
   - `AssignmentBelongsToTeacher`
   - `SubmissionBelongsToTeacher`
   - teacher id/number/name matching
4. Extract Firestore reads into a query service:
   - `LoadTeacherProfile`
   - `LoadTeacherLessons`
   - `LoadTeacherAssignments`
   - `LoadTeacherSubmissions`
   - `LoadTeacherAnnouncements`
5. Extract write fan-out last:
   - `UpdateAssignmentEverywhere`
   - `UpdateSubmissionEverywhere`

## Guardrails

- Do not remove legacy collection support yet.
- Do not change canonical submission id format: `normalize(assignmentId) + "_" + onlyDigits(studentNo)`.
- Do not normalize visible Turkish UI text as part of this refactor.
- Keep mojibake-compatible normalization cases until data migration is complete.
- Keep each extraction behavior-preserving and backed by build plus targeted tests.
