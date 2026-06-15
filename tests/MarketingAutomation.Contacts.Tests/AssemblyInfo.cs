// SQLite in-memory DBs are bound to a single connection; disabling parallelization
// keeps the EF provider's behavior deterministic across these context-heavy tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
