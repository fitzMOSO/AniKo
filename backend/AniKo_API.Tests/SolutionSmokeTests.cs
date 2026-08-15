namespace AniKo_API.Tests;

public class SolutionSmokeTests
{
    /// <summary>
    /// The .NET 10 Web SDK emits a public Program for top-level statements, so this
    /// passes out of the box. It is kept as a regression guard: WebApplicationFactory
    /// needs a public Program, and moving the entry point into a namespace or losing
    /// the Web SDK would break every integration test with an obscure generic-constraint
    /// error. This fails first, and clearly.
    /// </summary>
    [Fact]
    public void ApiAssemblyIsReferencedAndEntryPointIsPublic()
    {
        var programType = typeof(Program);

        Assert.Equal("Program", programType.Name);
        Assert.True(programType.IsPublic, "Program must be public so WebApplicationFactory can resolve it.");
    }
}
