namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class CiWorkflowArtifactTests
{
    [Fact]
    public void ReleaseBuildArtifactUpload_IncludesHiddenFilesForPlaywrightAssets()
    {
        var workflowPath = ResolveWorkflowPath();
        var workflow = File.ReadAllText(workflowPath);

        var uploadStepStart = workflow.IndexOf("- name: Upload Release build outputs for E2E jobs", StringComparison.Ordinal);
        Assert.True(uploadStepStart >= 0, "The CI workflow must define the Release build artifact upload step.");

        var nextJobStart = workflow.IndexOf("  e2e-smoke:", uploadStepStart, StringComparison.Ordinal);
        Assert.True(nextJobStart > uploadStepStart, "The Release build artifact upload step should appear before the E2E jobs.");

        var uploadStep = workflow[uploadStepStart..nextJobStart];

        Assert.Contains("include-hidden-files: true", uploadStep, StringComparison.Ordinal);
    }

    private static string ResolveWorkflowPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".github", "workflows", "ci.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate .github/workflows/ci.yml from the test output directory.");
    }
}
