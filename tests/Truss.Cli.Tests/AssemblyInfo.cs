using Xunit;

// The CLI under test owns process-global state: Capture swaps Console.Out,
// and the end-to-end suite pins environment variables for its child
// processes. Parallel test classes race all of it; a run writing into a
// writer another test just disposed dies with an exit code of -1 and no
// message. These tests are serial because the subject is a process.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
