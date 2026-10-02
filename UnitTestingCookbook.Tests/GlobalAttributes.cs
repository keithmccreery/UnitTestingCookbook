using NUnit.Framework;

// Sets the worker-pool size available to whatever tests are explicitly marked [Parallelizable] (see
// ParallelProcessingSafeTests) - it has no effect on anything else, since nothing runs in parallel unless a
// fixture or test opts in with [Parallelizable] itself. Every other fixture in this assembly keeps running
// exactly as sequentially as before.
// begin-snippet: GlobalAttributes_LevelOfParallelism
[assembly: LevelOfParallelism(4)]
// end-snippet
