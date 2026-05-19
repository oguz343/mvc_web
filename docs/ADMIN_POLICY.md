# Admin Policy

- Web admin policy is intentionally single-account scoped.
- A web admin session is valid only when the normalized role is `admin` and the numeric user number is `0000`.
- Admin accounts with a number other than `0000` are not supported for admin pages.
- Admin pages should use `[AdminOnly]` for route protection. Extra `_session.IsAdmin(...)` checks are only needed when an action has a separate runtime authorization branch.
