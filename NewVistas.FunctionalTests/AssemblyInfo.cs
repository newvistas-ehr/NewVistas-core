using NUnit.Framework;

// Run test fixtures in parallel — safe because each fixture uses unique grain IDs (Guid.NewGuid).
[assembly: Parallelizable(ParallelScope.Fixtures)]
