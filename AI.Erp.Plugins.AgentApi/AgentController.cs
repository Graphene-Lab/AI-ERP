namespace ErpAgentApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

using ErpApi;
using ErpCore;
using ErpEql;
using ErpModels;

/// <summary>
/// JWT-secured REST surface over the ERP's own managers. Every operation runs through
/// RecordManager / EntityManager / EqlCommand, so the ERP's hooks, validation and the
/// authenticated user's per-entity permissions are enforced — the agent acts as a user,
/// never as a raw database connection.
/// </summary>
[Authorize]
[Route("api/v3.0/p/agent")]
public class AgentController : Controller
{
    // ──────────────────────────────────────────────
    //  Schema discovery
    // ──────────────────────────────────────────────

    /// <summary>List every entity (type) with its fields, so the agent can learn the data model before querying or writing.</summary>
    [HttpGet("schema")]
    public IActionResult Schema()
    {
        try
        {
            var response = new EntityManager().ReadEntities();
            if (!response.Success || response.Object == null)
                return Json(AgentResponse.Fail("Could not read the entity schema."));

            var entities = response.Object
                .Where(e => !e.System)
                .Select(MapEntity)
                .ToList();
            return Json(AgentResponse.Ok(entities));
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Schema read failed — {ex.Message}"));
        }
    }

    /// <summary>Read a single entity's fields by name.</summary>
    [HttpGet("schema/{entity}")]
    public IActionResult SchemaEntity(string entity)
    {
        try
        {
            var response = new EntityManager().ReadEntity(entity);
            if (!response.Success || response.Object == null)
                return Json(AgentResponse.Fail($"Entity '{entity}' not found."));
            return Json(AgentResponse.Ok(MapEntity(response.Object)));
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Schema read failed — {ex.Message}"));
        }
    }

    // ──────────────────────────────────────────────
    //  Query (EQL, read-only)
    // ──────────────────────────────────────────────

    /// <summary>Run an EQL SELECT. Body: { "eql": "...", "parameters": [ { "name": "...", "value": "..." } ] }.</summary>
    [HttpPost("query")]
    public IActionResult Query([FromBody] JObject body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Value<string>("eql")))
            return Json(AgentResponse.Fail("A non-empty 'eql' is required."));

        try
        {
            var parameters = new List<EqlParameter>();
            if (body["parameters"] is JArray arr)
            {
                foreach (var p in arr)
                    parameters.Add(new EqlParameter(p.Value<string>("name"), p.Value<string>("value")));
            }

            var records = new EqlCommand(body.Value<string>("eql"), parameters).Execute();
            return Json(AgentResponse.Ok(new { records, total_count = records.TotalCount }));
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Query failed — {ex.Message}"));
        }
    }

    // ──────────────────────────────────────────────
    //  Record CRUD
    // ──────────────────────────────────────────────

    /// <summary>Create a record. Body: the field values as a JSON object (e.g. { "name": "Acme", "email": "…" }). Returns the created record including its id.</summary>
    [HttpPost("records/{entity}")]
    public IActionResult CreateRecord(string entity, [FromBody] EntityRecord record)
    {
        if (record == null)
            return Json(AgentResponse.Fail("A record body is required."));
        try
        {
            // RecordManager only inserts the id when it is present in the record; generate one
            // when the caller omits it so a plain field-values body inserts cleanly.
            if (!record.Properties.ContainsKey("id"))
                record["id"] = Guid.NewGuid();

            // Enforce required fields at the API boundary. The field defaults (e.g. "" for text)
            // would otherwise let a missing required value slip through as a blank row.
            var missing = MissingRequiredFields(entity, record);
            if (missing.Count > 0)
                return Json(AgentResponse.Fail(
                    $"Missing required field(s): {string.Join(", ", missing)}.", missing));

            var response = new RecordManager().CreateRecord(entity, record);
            return ToAgent(response, "record created");
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Create failed — {ex.Message}"));
        }
    }

    /// <summary>Update a record by id. Body: the fields to change as a JSON object. The id in the route is authoritative.</summary>
    [HttpPatch("records/{entity}/{id}")]
    public IActionResult UpdateRecord(string entity, Guid id, [FromBody] EntityRecord record)
    {
        if (record == null)
            return Json(AgentResponse.Fail("A record body is required."));
        try
        {
            record["id"] = id;
            var response = new RecordManager().UpdateRecord(entity, record);
            return ToAgent(response, "record updated");
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Update failed — {ex.Message}"));
        }
    }

    /// <summary>Delete a record by id.</summary>
    [HttpDelete("records/{entity}/{id}")]
    public IActionResult DeleteRecord(string entity, Guid id)
    {
        try
        {
            var response = new RecordManager().DeleteRecord(entity, id);
            return ToAgent(response, "record deleted");
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Delete failed — {ex.Message}"));
        }
    }

    // ──────────────────────────────────────────────
    //  Many-to-many relations
    // ──────────────────────────────────────────────

    /// <summary>Add or remove a many-to-many link. Body: { "relationId": "...", "originId": "...", "targetId": "...", "remove": false }.</summary>
    [HttpPost("relations")]
    public IActionResult Relation([FromBody] JObject body)
    {
        if (body == null)
            return Json(AgentResponse.Fail("A relation body is required."));
        try
        {
            var relationId = Guid.Parse(body.Value<string>("relationId"));
            var originId = Guid.Parse(body.Value<string>("originId"));
            var targetId = Guid.Parse(body.Value<string>("targetId"));
            var remove = body.Value<bool?>("remove") ?? false;

            var manager = new RecordManager();
            var response = remove
                ? manager.RemoveRelationManyToManyRecord(relationId, originId, targetId)
                : manager.CreateRelationManyToManyRecord(relationId, originId, targetId);
            return ToAgent(response, remove ? "relation removed" : "relation added");
        }
        catch (FormatException)
        {
            return Json(AgentResponse.Fail("relationId, originId and targetId must be valid GUIDs."));
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Relation operation failed — {ex.Message}"));
        }
    }

    // ──────────────────────────────────────────────
    //  Composed business operations (one call = many steps)
    // ──────────────────────────────────────────────

    /// <summary>Create a sales order with its line items in one call. Body: { "customer_id": "...", "lines": [ { "sku": "...", "quantity": 2, "discountPercent": 0 } ], "order_date": "yyyy-MM-dd", "required_date": "yyyy-MM-dd", "currency": "EUR" }. Prices are read from the product; totals are computed server-side.</summary>
    [HttpPost("composed/sales-order")]
    public IActionResult ComposedSalesOrder([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var customerId = Guid.Parse(Require(body, "customer_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.PlaceSalesOrder(customerId, lines,
                body.Value<string>("order_date"), body.Value<string>("required_date"), body.Value<string>("currency"));
        }, "sales order placed");
    }

    /// <summary>Turn an existing sales order into a sent invoice in one call. Body: { "order_id": "...", "due_days": 30 }.</summary>
    [HttpPost("composed/invoice")]
    public IActionResult ComposedInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var orderId = Guid.Parse(Require(body, "order_id"));
            var dueDays = body.Value<int?>("due_days") ?? 30;
            return ComposedOperations.InvoiceSalesOrder(orderId, dueDays);
        }, "invoice created");
    }

    /// <summary>Record a payment against an invoice and update its status (paid/partial) in one call. Body: { "invoice_id": "...", "amount": 100, "method": "bank", "payment_date": "yyyy-MM-dd" }.</summary>
    [HttpPost("composed/payment")]
    public IActionResult ComposedPayment([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var invoiceId = Guid.Parse(Require(body, "invoice_id"));
            var amount = body.Value<decimal>("amount");
            return ComposedOperations.RecordPayment(invoiceId, amount,
                body.Value<string>("method"), body.Value<string>("payment_date"));
        }, "payment recorded");
    }

    /// <summary>Create a purchase order with its line items in one call. Body: { "supplier_id": "...", "lines": [ { "sku": "...", "quantity": 5, "unitCost": 3.0 } ], "order_date": "yyyy-MM-dd", "expected_date": "yyyy-MM-dd" }.</summary>
    [HttpPost("composed/purchase-order")]
    public IActionResult ComposedPurchaseOrder([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var supplierId = Guid.Parse(Require(body, "supplier_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.PlacePurchaseOrder(supplierId, lines,
                body.Value<string>("order_date"), body.Value<string>("expected_date"));
        }, "purchase order placed");
    }

    /// <summary>Receive a purchase order: add each line's quantity to product stock and mark the order received. Body: { "purchase_order_id": "..." }.</summary>
    [HttpPost("composed/receive")]
    public IActionResult ComposedReceive([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var poId = Guid.Parse(Require(body, "purchase_order_id"));
            return ComposedOperations.ReceivePurchaseOrder(poId);
        }, "purchase order received");
    }

    private IActionResult Composed(Func<object> action, string verb)
    {
        try
        {
            return Json(AgentResponse.Ok(new { message = verb, result = action() }));
        }
        catch (FormatException)
        {
            return Json(AgentResponse.Fail("A required id is not a valid GUID."));
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail(ex.Message));
        }
    }

    private static string Require(JObject body, string field)
    {
        var v = body?.Value<string>(field);
        if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException($"'{field}' is required.");
        return v;
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    // Fields marked required in the entity that the incoming record does not supply with a
    // non-empty value. The id is skipped (auto-generated). Numbers and booleans count as
    // supplied when the key is present (0 / false are valid values).
    private List<string> MissingRequiredFields(string entity, EntityRecord record)
    {
        var missing = new List<string>();
        var resp = new EntityManager().ReadEntity(entity);
        if (resp == null || !resp.Success || resp.Object == null) return missing;
        foreach (var f in resp.Object.Fields ?? new List<Field>())
        {
            if (!f.Required) continue;
            if (string.Equals(f.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            if (!record.Properties.ContainsKey(f.Name)) { missing.Add(f.Name); continue; }
            object v = record[f.Name];
            if (v == null || string.IsNullOrWhiteSpace(v.ToString())) missing.Add(f.Name);
        }
        return missing;
    }

    private static object MapEntity(Entity entity) => new
    {
        name = entity.Name,
        label = entity.Label,
        labelPlural = entity.LabelPlural,
        fields = (entity.Fields ?? new List<Field>()).Select(f => new
        {
            name = f.Name,
            label = f.Label,
            type = f.GetFieldType().ToString(),
            required = f.Required
        }).ToList()
    };

    private IActionResult ToAgent(QueryResponse response, string verb)
    {
        if (response == null)
            return Json(AgentResponse.Fail($"{verb}: no response."));

        if (!response.Success)
        {
            var errors = response.Errors?.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m)).ToList()
                        ?? new List<string>();
            return Json(AgentResponse.Fail(response.Message ?? $"{verb} failed.", errors));
        }

        var record = response.Object?.Data?.FirstOrDefault();
        return Json(AgentResponse.Ok(new { message = verb, record }));
    }
}
