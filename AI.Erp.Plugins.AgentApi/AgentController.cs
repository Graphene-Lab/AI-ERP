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
    //  Setup / install (headless provisioning)
    // ──────────────────────────────────────────────

    /// <summary>Install/setup status: whether the ERP is provisioned (bootstrap applied), the
    /// non-system entity count, the per-entity seed record counts, and whether a reprovision
    /// would change anything (pending). Read-only — creates nothing.</summary>
    [HttpGet("setup-status")]
    public IActionResult SetupStatus()
    {
        try
        {
            return Json(AgentResponse.Ok(Bootstrap.Status(HttpContext.RequestServices)));
        }
        catch (Exception ex)
        {
            return Json(AgentResponse.Fail($"Setup status failed — {ex.Message}"));
        }
    }

    /// <summary>Re-apply bootstrap.json now (idempotent). Use after changing bootstrap.json to
    /// push new schema or seed without restarting the site. If nothing changed, returns
    /// "already applied". Body: {} (no fields required).</summary>
    [HttpPost("reprovision")]
    public IActionResult ComposedReprovision([FromBody] JObject body)
    {
        return Composed(() => Bootstrap.Reprovision(HttpContext.RequestServices), "reprovisioned");
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

    /// <summary>Receive goods against a purchase order: add each line's quantity to a warehouse's stock, record variance vs ordered, update the PO status. Body: { "purchase_order_id": "...", "lines": [ { "sku": "...", "quantity": 5, "unitCost": 3.0, "lot": "L1" } ], "warehouse_code": "WH1" }.</summary>
    [HttpPost("composed/receive")]
    public IActionResult ComposedReceive([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var poId = Guid.Parse(Require(body, "purchase_order_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.ReceivePurchaseOrder(poId, lines, body.Value<string>("warehouse_code"));
        }, "purchase order received");
    }

    /// <summary>Create a quote (preventivo) with lines and VAT-inclusive totals. Body: { "customer_id": "...", "lines": [ { "sku": "...", "quantity": 2, "unitPrice": 10, "discountPercent": 0 } ], "valid_until": "yyyy-MM-dd", "currency": "EUR" }.</summary>
    [HttpPost("composed/quote")]
    public IActionResult ComposedQuote([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var customerId = Guid.Parse(Require(body, "customer_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.CreateQuote(customerId, lines, body.Value<string>("valid_until"), body.Value<string>("currency"));
        }, "quote created");
    }

    /// <summary>Convert an accepted quote into a confirmed sales order, copying its lines. Body: { "quote_id": "..." }.</summary>
    [HttpPost("composed/quote-to-order")]
    public IActionResult ComposedQuoteToOrder([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var quoteId = Guid.Parse(Require(body, "quote_id"));
            return ComposedOperations.ConvertQuoteToOrder(quoteId);
        }, "quote converted to order");
    }

    /// <summary>Deliver a sales order (create a DDT): decrement stock, block over-delivery and negative stock. Body: { "order_id": "...", "lines": [ { "sku": "...", "quantity": 2, "lot": "L1" } ], "warehouse_code": "WH1" }.</summary>
    [HttpPost("composed/deliver")]
    public IActionResult ComposedDeliver([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var orderId = Guid.Parse(Require(body, "order_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.DeliverSalesOrder(orderId, lines, body.Value<string>("warehouse_code"));
        }, "sales order delivered");
    }

    /// <summary>Issue a credit note against a customer invoice. Body: { "invoice_id": "...", "amount": 50, "reason": "damaged goods" }.</summary>
    [HttpPost("composed/credit-note")]
    public IActionResult ComposedCreditNote([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var invoiceId = Guid.Parse(Require(body, "invoice_id"));
            var amount = body.Value<decimal>("amount");
            return ComposedOperations.CreateCreditNote(invoiceId, amount, body.Value<string>("reason"));
        }, "credit note issued");
    }

    /// <summary>Create a purchase request (richiesta d'acquisto). Body: { "lines": [ { "sku": "...", "quantity": 10, "estimatedCost": 4.0 } ], "requested_by": "warehouse" }.</summary>
    [HttpPost("composed/purchase-request")]
    public IActionResult ComposedPurchaseRequest([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var lines = body["lines"] as JArray;
            return ComposedOperations.CreatePurchaseRequest(lines, body.Value<string>("requested_by"));
        }, "purchase request created");
    }

    /// <summary>Convert an approved purchase request into a purchase order for a supplier. Body: { "request_id": "...", "supplier_id": "..." }.</summary>
    [HttpPost("composed/request-to-po")]
    public IActionResult ComposedRequestToPo([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var requestId = Guid.Parse(Require(body, "request_id"));
            var supplierId = Guid.Parse(Require(body, "supplier_id"));
            return ComposedOperations.ConvertRequestToPurchaseOrder(requestId, supplierId);
        }, "purchase request converted to order");
    }

    /// <summary>Register a supplier bill against a PO and run the triple-match check. Body: { "purchase_order_id": "...", "lines": [ { "sku": "...", "quantity": 5, "unitCost": 3.0 } ], "bill_date": "yyyy-MM-dd", "due_date": "yyyy-MM-dd" }.</summary>
    [HttpPost("composed/purchase-invoice")]
    public IActionResult ComposedPurchaseInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var poId = Guid.Parse(Require(body, "purchase_order_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.RegisterPurchaseInvoice(poId, lines, body.Value<string>("bill_date"), body.Value<string>("due_date"));
        }, "purchase invoice registered");
    }

    /// <summary>Pay a supplier bill. Body: { "bill_id": "...", "amount": 100, "method": "bank", "payment_date": "yyyy-MM-dd" }.</summary>
    [HttpPost("composed/supplier-payment")]
    public IActionResult ComposedSupplierPayment([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var billId = Guid.Parse(Require(body, "bill_id"));
            var amount = body.Value<decimal>("amount");
            return ComposedOperations.PaySupplierBill(billId, amount, body.Value<string>("method"), body.Value<string>("payment_date"));
        }, "supplier bill paid");
    }

    /// <summary>Issue a credit note from a supplier against a bill. Body: { "bill_id": "...", "amount": 50, "reason": "returned goods" }.</summary>
    [HttpPost("composed/supplier-credit-note")]
    public IActionResult ComposedSupplierCreditNote([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var billId = Guid.Parse(Require(body, "bill_id"));
            var amount = body.Value<decimal>("amount");
            return ComposedOperations.CreateSupplierCreditNote(billId, amount, body.Value<string>("reason"));
        }, "supplier credit note issued");
    }

    /// <summary>Transfer stock between two warehouses. Body: { "sku": "...", "from_warehouse": "WH1", "to_warehouse": "WH2", "quantity": 5 }.</summary>
    [HttpPost("composed/transfer-stock")]
    public IActionResult ComposedTransferStock([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var sku = Require(body, "sku");
            var from = Require(body, "from_warehouse");
            var to = Require(body, "to_warehouse");
            var quantity = body.Value<decimal>("quantity");
            return ComposedOperations.TransferStock(sku, from, to, quantity);
        }, "stock transferred");
    }

    /// <summary>Manually adjust a product's stock in a warehouse to a new quantity. Body: { "sku": "...", "warehouse_code": "WH1", "new_quantity": 100, "reason": "stock count" }.</summary>
    [HttpPost("composed/adjust-stock")]
    public IActionResult ComposedAdjustStock([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var sku = Require(body, "sku");
            var wh = Require(body, "warehouse_code");
            var newQty = body.Value<decimal>("new_quantity");
            return ComposedOperations.AdjustStockManual(sku, wh, newQty, body.Value<string>("reason"));
        }, "stock adjusted");
    }

    /// <summary>Create a customer with billing/shipping addresses and contacts in one call. Body: { "name": "...", "billing": { "line1","city","country","postal_code" }, "ship": { ... }, "contacts": [ { "name","role","email","phone" } ], "email": "...", "currency": "EUR" }.</summary>
    [HttpPost("composed/customer-profile")]
    public IActionResult ComposedCustomerProfile([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var name = Require(body, "name");
            var billing = body["billing"] as JObject;
            var ship = body["ship"] as JObject;
            var contacts = body["contacts"] as JArray;
            return ComposedOperations.CreateCustomerProfile(name, billing, ship, contacts, body.Value<string>("email"), body.Value<string>("currency"));
        }, "customer profile created");
    }

    /// <summary>Add barcodes and suppliers to an existing product. Body: { "sku": "...", "barcodes": [ { "code","barcode_type" } ], "suppliers": [ { "supplier_name","supplier_sku","lead_time_days","unit_cost","is_default" } ] }.</summary>
    [HttpPost("composed/enrich-product")]
    public IActionResult ComposedEnrichProduct([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var sku = Require(body, "sku");
            var barcodes = body["barcodes"] as JArray;
            var suppliers = body["suppliers"] as JArray;
            return ComposedOperations.EnrichProduct(sku, barcodes, suppliers);
        }, "product enriched");
    }

    /// <summary>Resolve the effective unit price for a SKU for a customer and quantity (price list + discount rules + currency). Body: { "sku": "...", "customer_name": "...", "quantity": 10 }.</summary>
    [HttpPost("composed/resolve-price")]
    public IActionResult ComposedResolvePrice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var sku = Require(body, "sku");
            var customerName = Require(body, "customer_name");
            var qty = body.Value<decimal>("quantity");
            return ComposedOperations.ResolvePrice(sku, customerName, qty);
        }, "price resolved");
    }

    /// <summary>Place a sales order by customer name with confirmation/planned-delivery dates, transport terms and a sales agent. Body: { "customer_name": "...", "lines": [ { "sku","quantity" } ], "confirmed_date": "yyyy-MM-dd", "planned_date": "yyyy-MM-dd", "transport_terms": "FCA", "agent_name": "Anna Verdi" }.</summary>
    [HttpPost("composed/order-with-terms")]
    public IActionResult ComposedOrderWithTerms([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var customerName = Require(body, "customer_name");
            var lines = body["lines"] as JArray;
            return ComposedOperations.CreateOrderWithTerms(customerName, lines,
                body.Value<string>("confirmed_date"), body.Value<string>("planned_date"),
                body.Value<string>("transport_terms"), body.Value<string>("agent_name"));
        }, "order with terms placed");
    }

    /// <summary>Deliver a sales order with transport details on the DDT. Body: { "order_id": "...", "lines": [ { "sku","quantity" } ], "warehouse_code": "WH1", "transport_cause": "vendita", "carrier": "DHL", "tracking_number": "TRK123" }.</summary>
    [HttpPost("composed/deliver-transport")]
    public IActionResult ComposedDeliverTransport([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var orderId = Guid.Parse(Require(body, "order_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.DeliverWithTransport(orderId, lines, body.Value<string>("warehouse_code"),
                body.Value<string>("transport_cause"), body.Value<string>("carrier"), body.Value<string>("tracking_number"));
        }, "sales order delivered with transport");
    }

    /// <summary>Consolidate the delivered-but-not-yet-invoiced lines of several sales orders (same customer) into one invoice. Body: { "order_ids": [ "guid", ... ], "due_days": 30 }.</summary>
    [HttpPost("composed/consolidate-invoice")]
    public IActionResult ComposedConsolidateInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var orderIds = body["order_ids"] as JArray;
            var dueDays = body.Value<int?>("due_days") ?? 30;
            return ComposedOperations.ConsolidateInvoice(orderIds, dueDays);
        }, "consolidated invoice created");
    }

    /// <summary>Run a cycle count in a warehouse and apply the variance to stock. Body: { "warehouse_code": "WH1", "lines": [ { "sku","counted_qty" } ] }.</summary>
    [HttpPost("composed/cycle-count")]
    public IActionResult ComposedCycleCount([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var warehouseCode = Require(body, "warehouse_code");
            var lines = body["lines"] as JArray;
            return ComposedOperations.RunCycleCount(warehouseCode, lines);
        }, "cycle count applied");
    }

    /// <summary>Process a customer return against a delivery (restock or scrap each line). Body: { "delivery_id": "...", "lines": [ { "sku","quantity","disposition" } ], "warehouse_code": "WH1" }.</summary>
    [HttpPost("composed/customer-return")]
    public IActionResult ComposedCustomerReturn([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var deliveryId = Guid.Parse(Require(body, "delivery_id"));
            var lines = body["lines"] as JArray;
            return ComposedOperations.ProcessCustomerReturn(deliveryId, lines, body.Value<string>("warehouse_code"));
        }, "customer return processed");
    }

    /// <summary>Create a sales invoice from scratch (no order) with discount, accessories/transport, stamp, withholding, reverse charge, tax type, currency and installments. Body: { "customer_name": "...", "lines": [ { "sku","quantity","unit_price","discount_percent","vatCode" } ], "document_discount": 5, "accessory_lines": [ { "description","amount","vatCode","type" } ], "stamp_tax": 2, "withholding_percent": 5, "reverse_charge": false, "tax_document_type": "taxable", "currency": "EUR", "installments": [ { "due_date","amount" } ] }.</summary>
    [HttpPost("composed/sales-invoice")]
    public IActionResult ComposedSalesInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var customerName = Require(body, "customer_name");
            var lines = body["lines"] as JArray;
            return ComposedOperations.CreateSalesInvoiceFromScratch(customerName, lines,
                body.Value<decimal?>("document_discount") ?? 0m,
                body["accessory_lines"] as JArray,
                body.Value<decimal?>("stamp_tax") ?? 0m,
                body.Value<decimal?>("withholding_percent") ?? 0m,
                body.Value<bool?>("reverse_charge") ?? false,
                body.Value<string>("tax_document_type"),
                body.Value<string>("currency"),
                body["installments"] as JArray);
        }, "sales invoice created");
    }

    /// <summary>Create a supplier bill (purchase invoice) from scratch with a document discount and stamp. Body: { "supplier_name": "...", "lines": [ { "sku","quantity","unit_cost" } ], "document_discount": 0, "stamp_tax": 0, "currency": "EUR" }.</summary>
    [HttpPost("composed/purchase-invoice-scratch")]
    public IActionResult ComposedPurchaseInvoiceScratch([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var supplierName = Require(body, "supplier_name");
            var lines = body["lines"] as JArray;
            return ComposedOperations.CreatePurchaseInvoiceFromScratch(supplierName, lines,
                body.Value<decimal?>("document_discount") ?? 0m,
                body.Value<decimal?>("stamp_tax") ?? 0m,
                body.Value<string>("currency"));
        }, "purchase invoice created");
    }

    /// <summary>Issue a debit note against a supplier. Body: { "supplier_name": "...", "bill_id": "...", "amount": 100, "reason": "..." }.</summary>
    [HttpPost("composed/debit-note")]
    public IActionResult ComposedDebitNote([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var supplierName = Require(body, "supplier_name");
            var amount = body.Value<decimal>("amount");
            return ComposedOperations.CreateDebitNote(supplierName, body.Value<string>("bill_id"), amount, body.Value<string>("reason"));
        }, "debit note issued");
    }

    /// <summary>Convert a proforma invoice into a real (immediate) invoice. Body: { "proforma_invoice_id": "..." }.</summary>
    [HttpPost("composed/proforma-to-invoice")]
    public IActionResult ComposedProformaToInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var id = Require(body, "proforma_invoice_id");
            return ComposedOperations.ConvertProformaToInvoice(id);
        }, "proforma converted to invoice");
    }

    /// <summary>Post an invoice. Body: { "invoice_id": "..." }.</summary>
    [HttpPost("composed/post-invoice")]
    public IActionResult ComposedPostInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var id = Require(body, "invoice_id");
            return ComposedOperations.PostInvoice(id);
        }, "invoice posted");
    }

    /// <summary>Storno (reverse) an invoice. Body: { "invoice_id": "..." }.</summary>
    [HttpPost("composed/storno-invoice")]
    public IActionResult ComposedStornoInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var id = Require(body, "invoice_id");
            return ComposedOperations.StornoInvoice(id);
        }, "invoice storned");
    }

    /// <summary>Duplicate an invoice into a new draft. Body: { "invoice_id": "..." }.</summary>
    [HttpPost("composed/duplicate-invoice")]
    public IActionResult ComposedDuplicateInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var id = Require(body, "invoice_id");
            return ComposedOperations.DuplicateInvoice(id);
        }, "invoice duplicated");
    }

    /// <summary>Cancel an (unposted) invoice. Body: { "invoice_id": "..." }.</summary>
    [HttpPost("composed/cancel-invoice")]
    public IActionResult ComposedCancelInvoice([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var id = Require(body, "invoice_id");
            return ComposedOperations.CancelInvoice(id);
        }, "invoice cancelled");
    }

    /// <summary>Collect cash across several invoices with an optional allowance. Body: { "invoice_ids": [ "guid", ... ] } or { "invoice_numbers": [ "INV-...", ... ] } (or both), "amount": 100, "method": "bank", "allowance": 10 }.</summary>
    [HttpPost("composed/record-collection")]
    public IActionResult ComposedRecordCollection([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var amount = body.Value<decimal>("amount");
            return ComposedOperations.RecordCollection(body["invoice_ids"] as JArray, body["invoice_numbers"] as JArray, amount, body.Value<string>("method"), body.Value<decimal?>("allowance") ?? 0m);
        }, "collection recorded");
    }

    /// <summary>Customer statement: open invoices with aging buckets. Body: { "customer_name": "..." }.</summary>
    [HttpPost("composed/customer-statement")]
    public IActionResult ComposedCustomerStatement([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var customerName = Require(body, "customer_name");
            return ComposedOperations.CustomerStatement(customerName);
        }, "customer statement");
    }

    /// <summary>Manager report: sales revenue grouped by customer over an optional issue-date range. Body: { "from_date": "yyyy-MM-dd", "to_date": "yyyy-MM-dd" } (both optional).</summary>
    [HttpPost("composed/sales-by-customer")]
    public IActionResult ComposedSalesByCustomer([FromBody] JObject body)
    {
        return Composed(() =>
        {
            return ComposedOperations.SalesByCustomer(body?.Value<string>("from_date"), body?.Value<string>("to_date"));
        }, "sales by customer");
    }

    /// <summary>Manager report: sales revenue grouped by product over an optional issue-date range. Body: { "from_date": "yyyy-MM-dd", "to_date": "yyyy-MM-dd" } (both optional).</summary>
    [HttpPost("composed/sales-by-product")]
    public IActionResult ComposedSalesByProduct([FromBody] JObject body)
    {
        return Composed(() =>
        {
            return ComposedOperations.SalesByProduct(body?.Value<string>("from_date"), body?.Value<string>("to_date"));
        }, "sales by product");
    }

    /// <summary>Manager report: purchase cost grouped by supplier over an optional bill-date range. Body: { "from_date": "yyyy-MM-dd", "to_date": "yyyy-MM-dd" } (both optional).</summary>
    [HttpPost("composed/purchases-by-supplier")]
    public IActionResult ComposedPurchasesBySupplier([FromBody] JObject body)
    {
        return Composed(() =>
        {
            return ComposedOperations.PurchasesBySupplier(body?.Value<string>("from_date"), body?.Value<string>("to_date"));
        }, "purchases by supplier");
    }

    /// <summary>Manager report: receivables aging (scadenzario) — open sales invoices bucketed by how overdue they are. Body: {} (no fields required).</summary>
    [HttpPost("composed/aging-receivables")]
    public IActionResult ComposedAgingReceivables([FromBody] JObject body)
    {
        return Composed(() =>
        {
            return ComposedOperations.AgingReceivables();
        }, "aging receivables");
    }

    /// <summary>Reserve stock of a SKU in a warehouse for an order. Body: { "sku": "...", "warehouse_code": "WH1", "quantity": 10, "order_id": "..." }.</summary>
    [HttpPost("composed/reserve-stock")]
    public IActionResult ComposedReserveStock([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var sku = Require(body, "sku");
            var wh = Require(body, "warehouse_code");
            var qty = body.Value<decimal>("quantity");
            return ComposedOperations.ReserveStock(sku, wh, qty, body.Value<string>("order_id"));
        }, "stock reserved");
    }

    /// <summary>Release reservations for a SKU in a warehouse. Body: { "sku": "...", "warehouse_code": "WH1", "quantity": 10 }.</summary>
    [HttpPost("composed/release-stock")]
    public IActionResult ComposedReleaseStock([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var sku = Require(body, "sku");
            var wh = Require(body, "warehouse_code");
            var qty = body.Value<decimal>("quantity");
            return ComposedOperations.ReleaseStock(sku, wh, qty);
        }, "stock released");
    }

    /// <summary>Process a supplier return (reduce stock). Body: { "supplier_name": "...", "receipt_id": "...", "lines": [ { "sku","quantity","reason" } ], "warehouse_code": "WH1" }.</summary>
    [HttpPost("composed/supplier-return")]
    public IActionResult ComposedSupplierReturn([FromBody] JObject body)
    {
        return Composed(() =>
        {
            var supplierName = Require(body, "supplier_name");
            var lines = body["lines"] as JArray;
            return ComposedOperations.ProcessSupplierReturn(supplierName, body.Value<string>("receipt_id"), lines, body.Value<string>("warehouse_code"));
        }, "supplier return processed");
    }

    private IActionResult Composed(Func<object> action, string verb)
    {
        try
        {
            // Run the whole composed operation in one database transaction so a failure in a
            // later step rolls back the earlier writes instead of leaving partial records
            // (orphaned headers, half-moved stock). RecordManager binds to DbContext.Current,
            // so once the context is in a transactional state every write inside the operation
            // joins it (their own BeginTransaction calls become savepoints).
            var ctx = ErpCore.Database.DbContext.Current;
            if (ctx == null)
                return Json(AgentResponse.Ok(new { message = verb, result = action() }));

            object result;
            using (var outer = ctx.CreateConnection())
            {
                outer.BeginTransaction();
                try
                {
                    result = action();
                    outer.CommitTransaction();
                }
                catch
                {
                    try { outer.RollbackTransaction(); } catch { }
                    throw;
                }
            }
            return Json(AgentResponse.Ok(new { message = verb, result }));
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
