# Git Workflow Best Practices

1. **Use short-lived feature branches.** Create a branch per feature or fix, keep it focused, and merge or delete it promptly to avoid long-running divergence from the main branch.

2. **Write meaningful commit messages.** Use the imperative mood and a concise subject line (under 72 characters). A clear message explains *why* the change was made, not just what changed.

3. **Keep commits small and atomic.** Each commit should represent one logical change. Smaller commits are easier to review, bisect, and revert if something goes wrong.

4. **Rebase before merging to keep history linear.** Run `git rebase main` on your feature branch before opening a pull request so the history stays clean and merge commits are avoided.

5. **Always review your diff before committing.** Run `git diff --staged` to confirm you are committing exactly what you intend — this catches accidentally staged files, debug output, or unfinished changes.
