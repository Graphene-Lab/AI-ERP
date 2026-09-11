namespace ErpAgentApi;

using ErpCore;

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
}
