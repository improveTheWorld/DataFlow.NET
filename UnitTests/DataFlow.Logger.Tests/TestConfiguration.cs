using Xunit;

// Disable parallel execution of tests because iLogger uses global static state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
