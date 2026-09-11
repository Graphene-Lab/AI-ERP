namespace ErpAgentApi;

using System.Collections.Generic;

/// <summary>
/// Minimal, assembly-name-free response envelope shared by every Agent API endpoint.
/// Deliberately independent of the host's response model so the serialized payload never
/// embeds a fork/vendor assembly identity (a requirement of the dual-target design).
/// </summary>
public class AgentResponse
{
    public bool Success { get; set; }
    public object Data { get; set; }
    public string Error { get; set; }
    public List<string> Errors { get; set; } = new List<string>();

    public static AgentResponse Ok(object data) => new AgentResponse { Success = true, Data = data };

    public static AgentResponse Fail(string error, IEnumerable<string> errors = null)
        => new AgentResponse { Success = false, Error = error, Errors = errors != null ? new List<string>(errors) : new List<string>() };
}
