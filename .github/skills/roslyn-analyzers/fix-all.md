# Designing a correct FixAll provider

Detail for the [roslyn-analyzers](SKILL.md) skill. Read this when a
`CodeFixProvider` supports more than one diagnostic per document, project, or
solution.

## Choose the provider from the edit shape

`WellKnownFixAllProviders.BatchFixer` invokes the ordinary code fix independently
for each diagnostic against a fork of the original document, then merges the
resulting text changes. Because each fix is computed independently, a
high-cardinality run can materialize one changed document or solution per
diagnostic and schedule many computations concurrently. Use it only when the
changes cannot conflict **and** the diagnostic count is predictably bounded.

Before selecting it, estimate total diagnostics, diagnostics per document, the
largest affected document, and total affected source bytes. Non-overlapping edits
are necessary for `BatchFixer`, but they are not sufficient to make a
high-cardinality workload safe.

Do not use `BatchFixer` without further analysis when fixes can:

- rewrite nested or overlapping syntax;
- insert text at the same position;
- run formatting, simplification, or other cleanup that expands the effective
  text-change spans;
- depend on edits made for another diagnostic; or
- replace an ancestor and one of its descendants.

Two zero-length insertions at one position are still ambiguous. A conflict can
discard all text changes produced by one independently computed fix, not merely
the overlapping hunk.

For document-local fixes, prefer `FixAllProvider.Create` or derive from
`DocumentBasedFixAllProvider`. Roslyn explicitly recommends the document-based
provider instead of `BatchFixer` when each diagnostic affects only its originating
document. It filters diagnostics by scope, buckets them by document, processes
projects serially, and processes documents within a project in parallel. The
default factory overload supports document, project, and solution scopes. Pass an
explicit `supportedFixAllScopes` array to include containing-member or
containing-type scope.

Use a custom provider when the fix changes project or solution metadata, adds,
removes, or renames documents, or otherwise cannot be expressed as independent
document callbacks.

| Edit shape | Provider |
| --- | --- |
| One coherent document transform | `FixAllProvider.Create` |
| One `SyntaxEditor` transform using all diagnostics in a document | `FixAllProvider.Create` |
| Diagnostics carry complete `TextChange` replacements | `FixAllProvider.Create` |
| Low-cardinality independent actions with cross-document effects | `BatchFixer`, after conflict and resource review |
| Project/solution metadata or document add/remove/rename | Custom `FixAllProvider` |

## Apply one coherent document edit

Use one callback and one coherent edit for all diagnostics in a document. For
syntax edits, use one `SyntaxEditor`, resolve diagnostics against a single syntax
root, and schedule replacements in an order that preserves node identity:

1. Filter diagnostics to the selected `CodeActionEquivalenceKey` when the provider
   offers multiple actions for one diagnostic.
2. Resolve and validate every target against the same original root.
3. Order nested edits inner-to-outer. For equal starts, process shorter spans first.
4. Track equivalent annotations or recompute from the editor's current tree when a
   later edit depends on an earlier replacement. Do not retain stale syntax-node
   references across replacements.
5. Run simplification or formatting once on the combined result rather than once
   per diagnostic.

Collection operations can invalidate syntax identity too. In particular, repeated
`SeparatedSyntaxList.Remove(node)` calls against nodes retained from an earlier
list can silently miss later removals. Compute the final list once, use indices or
stable keys, or rebuild it from the retained elements.

When diagnostics already carry complete replacements, collect one `TextChange`
per diagnostic, sort by source position, and call `SourceText.WithChanges` once.
That API validates that changes are ordered and non-overlapping.

`FixAllProvider.Create` applies the document callback once per affected document
for the scopes supplied to the factory. The default overload supports document,
project, and solution; containing-member and containing-type require the overload
that accepts `supportedFixAllScopes`. Return only the changed document content;
use a custom provider when other project or solution changes must survive.

## Keep action selection stable

Every registered `CodeAction` needs a stable, descriptive `equivalenceKey`. A
custom FixAll provider must apply only the action represented by
`FixAllContext.CodeActionEquivalenceKey`; it must not collapse several semantic
choices into one bulk edit.

If one action preserves semantics and another intentionally changes them, give
them distinct titles and keys. Include `(may change semantics)` in the title of
the semantic-changing action.

## Prove FixAll rather than only the single fix

The FixAll test must contain multiple applicable diagnostics and assert the final
combined source. Cover the conflict shapes the implementation permits:

- sibling edits;
- nested edits;
- same-position insertions;
- edits whose cleanup spans can overlap; and
- multiple actions filtered by equivalence key.

Assert the expected number of FixAll iterations or otherwise instrument the
provider so the test proves it used the intended bulk path. Include a positive
control with at least two independent occurrences: mutating the fixture or
provider to process only one diagnostic must fail the test. A passing single-item
fixture does not distinguish FixAll from an ordinary code fix.
