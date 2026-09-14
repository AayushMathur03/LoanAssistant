using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;

namespace Loan.ContractTests;

[TestFixture]
public class McpContractTests
{
    private McpToolServer _mcpServer = null!;

    [SetUp]
    public void SetUp()
    {
        var identityService = new SyntheticIdentityService();
        var creditService = new SyntheticCreditService();
        var policyRetriever = new SyntheticPolicyRetriever();

        _mcpServer = new McpToolServer(identityService, creditService, policyRetriever);
    }

    [Test]
    public void GetApprovedTools_ShouldReturnOnlyApprovedScopedTools()
    {
        var tools = _mcpServer.GetApprovedTools().ToList();

        Assert.That(tools.Count, Is.EqualTo(3));
        Assert.That(tools.Select(t => t.Name), Is.EquivalentTo(new[] { "get_identity_status", "get_credit", "search_policy" }));
    }

    [Test]
    public async Task ExecuteToolAsync_GetIdentityStatus_ShouldReturnJsonResult()
    {
        var jsonArgs = "{\"syntheticId\":\"SYN-888777\"}";
        var response = await _mcpServer.ExecuteToolAsync("get_identity_status", jsonArgs);

        Assert.That(response.IsError, Is.False);
        Assert.That(response.ContentJson, Does.Contain("Alice Cooper"));
        Assert.That(response.ContentJson, Does.Contain("Verified"));
    }

    [Test]
    public async Task ExecuteToolAsync_UnapprovedTool_ShouldReturnError()
    {
        var jsonArgs = "{}";
        var response = await _mcpServer.ExecuteToolAsync("unapproved_database_dump", jsonArgs);

        Assert.That(response.IsError, Is.True);
        Assert.That(response.ContentJson, Does.Contain("not approved"));
    }
}
