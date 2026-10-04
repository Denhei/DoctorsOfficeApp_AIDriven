# Code Standards

## Naming
- **C#**: PascalCase for types, camelCase for locals, `_camelCase` for private fields.
- **TypeScript**: PascalCase for classes/components, camelCase for variables/functions, `I` prefix for interfaces.

## Formatting
- Use 4 spaces indentation.
- End files with a newline.
- XML documentation comments for public members (C#).
- TSLint/ESLint rules enforced via `npm run lint`.

## Git
- Commit messages follow Conventional Commits (`feat:`, `fix:`, `chore:`).
- Branch naming: `feature/<description>`.

## Testing
- Unit tests in `*.Tests` projects (xUnit) and `*.spec.ts` (Jest/Karma).
- Keep coverage >= 80% where feasible.
