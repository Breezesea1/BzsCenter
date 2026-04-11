namespace BzsOIDC.Idp.E2ETests.Infrastructure;

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

    [Fact]
    public void FullE2EJob_RunsOnWorkflowDispatchAndMainPush()
    {
        var workflowPath = ResolveWorkflowPath();
        var workflow = File.ReadAllText(workflowPath);

        var jobStart = workflow.IndexOf("  e2e-full:", StringComparison.Ordinal);
        Assert.True(jobStart >= 0, "The CI workflow must define the full E2E job.");

        var nextJobStart = workflow.IndexOf("  container-images:", jobStart, StringComparison.Ordinal);
        Assert.True(nextJobStart > jobStart, "The full E2E job should appear before the container image job.");

        var jobBlock = workflow[jobStart..nextJobStart];

        Assert.Contains("github.event_name == 'workflow_dispatch'", jobBlock, StringComparison.Ordinal);
        Assert.Contains("github.event_name == 'push'", jobBlock, StringComparison.Ordinal);
        Assert.Contains("github.ref == 'refs/heads/main'", jobBlock, StringComparison.Ordinal);
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
