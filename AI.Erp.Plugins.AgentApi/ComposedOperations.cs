namespace ErpAgentApi;

using ErpApi;
using ErpModels;
using ErpEql;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Multi-step business operations executed deterministically on the server, so the agent can
/// complete a whole task with a single call instead of orchestrating many primitive ones.
/// Each operation reads what it needs with EQL and writes through <see cref="RecordManager"/>,
/// so ERP validation and permissions still apply. Money is decimal; dates are ISO (yyyy-MM-dd).
/// </summary>
public static class ComposedOperations
{
    // ──────────────────────────────────────────────
    //  Sales: place an order with line items
    // ──────────────────────────────────────────────

    public static object PlaceSalesOrder(Guid customerId, JArray lines, string orderDate, string requiredDate, string currency)
    {
        if (lines == null || lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");

        var recMan = new RecordManager();
        var order = new EntityRecord();
        order["id"] = Guid.NewGuid();
        order["order_number"] = NextNumber("SO");
        order["customer_id"] = customerId;
        order["order_date"] = ParseDate(orderDate) ?? DateTime.Today;
        if (!string.IsNullOrWhiteSpace(requiredDate)) order["required_date"] = ParseDate(requiredDate);
        order["status"] = "draft";
        order["currency"] = string.IsNullOrWhiteSpace(currency) ? "EUR" : currency;
        order["total"] = 0m;

        var created = recMan.CreateRecord("sales_order", order);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create sales order"));
        var orderId = Guid.Parse(created.Object.Data.First()["id"].ToString());

        decimal total = 0m;
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var disc = l.Value<decimal?>("discountPercent") ?? 0m;

            var product = FindProductBySku(sku);
            if (product == null) throw new InvalidOperationException($"Unknown product SKU '{sku}'.");

            var unitPrice = l.Value<decimal?>("unitPrice") ?? Dec(product["unit_price"]);
            var lineTotal = Round(qty * unitPrice * (1m - disc / 100m));
            total += lineTotal;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["sales_order_id"] = orderId;
            line["product_id"] = Guid.Parse(product["id"].ToString());
            line["quantity"] = qty;
            line["unit_price"] = unitPrice;
            line["discount_percent"] = disc;
            line["line_total"] = lineTotal;
            var lr = recMan.CreateRecord("sales_order_line", line);
            if (!lr.Success) throw new InvalidOperationException(Err(lr, "create order line"));

            outLines.Add(new
            {
                product_id = line["product_id"],
                sku,
                quantity = qty,
                unit_price = unitPrice,
                discount_percent = disc,
                line_total = lineTotal
            });
        }

        var upd = new EntityRecord();
        upd["id"] = orderId;
        upd["total"] = Round(total);
        var ur = recMan.UpdateRecord("sales_order", upd);
        if (!ur.Success) throw new InvalidOperationException(Err(ur, "update order total"));

        return new { order_id = orderId, order_number = order["order_number"], total = Round(total), lines = outLines };
    }

    // ──────────────────────────────────────────────
    //  Sales: turn an order into an invoice
    // ──────────────────────────────────────────────

    public static object InvoiceSalesOrder(Guid orderId, int dueDays)
    {
        var order = FindById("sales_order", orderId);
        if (order == null) throw new InvalidOperationException("Sales order not found.");
        var status = order["status"]?.ToString();
        if (status == "cancelled") throw new InvalidOperationException("Cannot invoice a cancelled order.");

        var recMan = new RecordManager();
        var amount = Dec(order["total"]);
        var issue = DateTime.Today;
        var inv = new EntityRecord();
        inv["id"] = Guid.NewGuid();
        inv["invoice_number"] = NextNumber("INV");
        inv["customer_id"] = Guid.Parse(order["customer_id"].ToString());
        inv["sales_order_id"] = orderId;
        inv["issue_date"] = issue;
        inv["due_date"] = issue.AddDays(dueDays <= 0 ? 30 : dueDays);
        inv["amount"] = amount;
        inv["status"] = "sent";

        var r = recMan.CreateRecord("invoice", inv);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create invoice"));
        var invoiceId = Guid.Parse(r.Object.Data.First()["id"].ToString());

        // Mark the order confirmed.
        var upd = new EntityRecord();
        upd["id"] = orderId;
        upd["status"] = "confirmed";
        recMan.UpdateRecord("sales_order", upd);

        return new
        {
            invoice_id = invoiceId,
            invoice_number = inv["invoice_number"],
            amount,
            issue_date = issue.ToString("yyyy-MM-dd"),
            due_date = ((DateTime)inv["due_date"]).ToString("yyyy-MM-dd"),
            status = "sent"
        };
    }

    // ──────────────────────────────────────────────
    //  Sales: record a payment against an invoice
    // ──────────────────────────────────────────────

    public static object RecordPayment(Guid invoiceId, decimal amount, string method, string paymentDate)
    {
        var invoice = FindById("invoice", invoiceId);
        if (invoice == null) throw new InvalidOperationException("Invoice not found.");

        var recMan = new RecordManager();
        var pay = new EntityRecord();
        pay["id"] = Guid.NewGuid();
        pay["payment_ref"] = NextNumber("PAY");
        pay["invoice_id"] = invoiceId;
        pay["amount"] = amount;
        pay["payment_date"] = ParseDate(paymentDate) ?? DateTime.Today;
        pay["method"] = string.IsNullOrWhiteSpace(method) ? "bank" : method;

        var r = recMan.CreateRecord("payment", pay);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create payment"));
        var paymentId = Guid.Parse(r.Object.Data.First()["id"].ToString());

        var paidTotal = SumPayments(invoiceId);
        var invAmount = Dec(invoice["amount"]);
        var newStatus = paidTotal >= invAmount ? "paid" : "partial";

        var upd = new EntityRecord();
        upd["id"] = invoiceId;
        upd["status"] = newStatus;
        recMan.UpdateRecord("invoice", upd);

        return new
        {
            payment_id = paymentId,
            payment_ref = pay["payment_ref"],
            invoice_status = newStatus,
            paid_total = Round(paidTotal),
            balance = Round(invAmount - paidTotal)
        };
    }

    // ──────────────────────────────────────────────
    //  Purchasing: place a purchase order with lines
    // ──────────────────────────────────────────────

    public static object PlacePurchaseOrder(Guid supplierId, JArray lines, string orderDate, string expectedDate)
    {
        if (lines == null || lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");

        var recMan = new RecordManager();
        var po = new EntityRecord();
        po["id"] = Guid.NewGuid();
        po["po_number"] = NextNumber("PO");
        po["supplier_id"] = supplierId;
        po["order_date"] = ParseDate(orderDate) ?? DateTime.Today;
        if (!string.IsNullOrWhiteSpace(expectedDate)) po["expected_date"] = ParseDate(expectedDate);
        po["status"] = "draft";
        po["total"] = 0m;

        var created = recMan.CreateRecord("purchase_order", po);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create purchase order"));
        var poId = Guid.Parse(created.Object.Data.First()["id"].ToString());

        decimal total = 0m;
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var product = FindProductBySku(sku);
            if (product == null) throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
            var unitCost = l.Value<decimal?>("unitCost") ?? Dec(product["unit_cost"]);
            var lineTotal = Round(qty * unitCost);
            total += lineTotal;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["purchase_order_id"] = poId;
            line["product_id"] = Guid.Parse(product["id"].ToString());
            line["quantity"] = qty;
            line["unit_cost"] = unitCost;
            line["line_total"] = lineTotal;
            var lr = recMan.CreateRecord("purchase_order_line", line);
            if (!lr.Success) throw new InvalidOperationException(Err(lr, "create PO line"));

            outLines.Add(new { product_id = line["product_id"], sku, quantity = qty, unit_cost = unitCost, line_total = lineTotal });
        }

        var upd = new EntityRecord();
        upd["id"] = poId;
        upd["total"] = Round(total);
        recMan.UpdateRecord("purchase_order", upd);

        return new { purchase_order_id = poId, po_number = po["po_number"], total = Round(total), lines = outLines };
    }

    // ──────────────────────────────────────────────
    //  Purchasing: receive a purchase order (stock in)
    // ──────────────────────────────────────────────

    public static object ReceivePurchaseOrder(Guid poId)
    {
        var po = FindById("purchase_order", poId);
        if (po == null) throw new InvalidOperationException("Purchase order not found.");
        if (po["status"]?.ToString() == "received")
            throw new InvalidOperationException("Purchase order already received.");

        var recMan = new RecordManager();
        var lines = new EqlCommand("SELECT product_id, quantity FROM purchase_order_line WHERE purchase_order_id = @id",
            new List<EqlParameter> { new EqlParameter("id", poId.ToString()) }).Execute();

        var stocked = new List<object>();
        foreach (var line in lines)
        {
            var productId = Guid.Parse(line["product_id"].ToString());
            var qty = Dec(line["quantity"]);
            var product = FindById("product", productId);
            if (product == null) continue;
            var newStock = Round(Dec(product["stock_quantity"]) + qty);
            var upd = new EntityRecord();
            upd["id"] = productId;
            upd["stock_quantity"] = newStock;
            recMan.UpdateRecord("product", upd);
            stocked.Add(new { sku = product["sku"], added = qty, new_stock = newStock });
        }

        var pu = new EntityRecord();
        pu["id"] = poId;
        pu["status"] = "received";
        recMan.UpdateRecord("purchase_order", pu);

        return new { purchase_order_id = poId, po_number = po["po_number"], status = "received", stocked };
    }

    // ──────────────────────────────────────────────
    //  Shared helpers
    // ──────────────────────────────────────────────

    private static EntityRecord FindById(string entity, Guid id)
    {
        var recs = new EqlCommand($"SELECT * FROM {entity} WHERE id = @id",
            new List<EqlParameter> { new EqlParameter("id", id.ToString()) }).Execute();
        return recs != null && recs.Count > 0 ? recs[0] : null;
    }

    private static EntityRecord FindProductBySku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var recs = new EqlCommand("SELECT * FROM product WHERE sku = @sku",
            new List<EqlParameter> { new EqlParameter("sku", sku) }).Execute();
        return recs != null && recs.Count > 0 ? recs[0] : null;
    }

    private static decimal SumPayments(Guid invoiceId)
    {
        var recs = new EqlCommand("SELECT amount FROM payment WHERE invoice_id = @id",
            new List<EqlParameter> { new EqlParameter("id", invoiceId.ToString()) }).Execute();
        if (recs == null) return 0m;
        decimal sum = 0m;
        foreach (var r in recs) sum += Dec(r["amount"]);
        return sum;
    }

    private static string NextNumber(string prefix)
        => prefix + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

    private static DateTime? ParseDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        return null;
    }

    private static decimal Dec(object v)
    {
        if (v == null) return 0m;
        if (v is decimal dec) return dec;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private static string Err(QueryResponse r, string what)
    {
        var msg = r?.Message ?? "";
        var first = r?.Errors?.FirstOrDefault()?.Message;
        return $"{what} failed: {msg}{(string.IsNullOrWhiteSpace(first) ? "" : " | " + first)}";
    }
}
