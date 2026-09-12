namespace ErpAgentApi;

using ErpCore;
using System;

/// <summary>
/// Agent API plugin — exposes the ERP's record, entity and EQL managers over a JWT-secured
/// REST surface so an external AI agent can drive the ERP with full control. The plugin adds
/// no parallel permission model: every operation runs through the ERP's own managers and
/// honours the authenticated user's per-entity permissions.
/// </summary>
public class AgentApiPlugin : ErpPlugin
{
    public override string Name { get; protected set; } = "agent";
    public override string Prefix { get; protected set; } = "agent";
    public override string Url { get; protected set; } = "/p/agent/";
    public override string Description { get; protected set; } =
        "REST API for AI-agent control of the ERP: schema discovery, record CRUD, many-to-many relations and EQL queries.";
    public override int Version { get; protected set; } = 20260911;

    /// <summary>
    /// Runs during host startup inside a system security scope (the host wraps plugin
    /// initialization in <c>SecurityContext.OpenSystemScope()</c>). This is where the
    /// headless first-run setup is applied: if a <c>bootstrap.json</c> is present and has
    /// not been applied yet, the business schema and seed data are created here, so the
    /// ERP is ready for the agent with no interactive wizard.
    /// </summary>
    public override void Initialize(IServiceProvider serviceProvider)
    {
        try
        {
            Bootstrap.Apply(serviceProvider);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Bootstrap] first-run setup failed: " + ex.Message);
        }
    }
}
