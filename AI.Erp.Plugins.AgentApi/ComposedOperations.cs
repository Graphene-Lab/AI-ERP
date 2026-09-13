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
///
/// The deep commercial cycle: quote → sales order → goods delivery (DDT) → invoice → payment,
/// with credit notes; purchase request → purchase order → goods receipt → supplier bill
/// (triple match) → supplier payment, with supplier credit notes; and a per-warehouse stock
/// ledger (stock_item authoritative + stock_movement audit) kept in sync with the denormalized
/// product.stock_quantity total.
/// </summary>
public static class ComposedOperations
{
    // ══════════════════════════════════════════════
    //  SALES
    // ══════════════════════════════════════════════

    /// <summary>Create a quote (preventivo) with lines; totals include VAT.</summary>
    public static object CreateQuote(Guid customerId, JArray lines, string validUntil, string currency)
    {
        if (lines == null || lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");
        var customer = FindById("customer", customerId) ?? throw new InvalidOperationException("Customer not found.");

        var recMan = new RecordManager();
        var quote = new EntityRecord();
        quote["id"] = Guid.NewGuid();
        quote["quote_number"] = NextNumber("QTE");
        quote["customer_id"] = customerId;
        quote["quote_date"] = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(validUntil)) quote["valid_until"] = ParseDate(validUntil);
        quote["status"] = "draft";
        quote["currency"] = string.IsNullOrWhiteSpace(currency) ? (customer["currency"]?.ToString() ?? "EUR") : currency;
        if (Has(customer, "payment_term_id") && customer["payment_term_id"] != null)
            quote["payment_term_id"] = Guid.Parse(customer["payment_term_id"].ToString());

        var created = recMan.CreateRecord("quote", quote);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create quote"));
        var quoteId = Id(created);

        decimal subtotal = 0m, vatTotal = 0m;
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var product = FindProductBySku(l.Value<string>("sku")) ?? throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var price = l.Value<decimal?>("unitPrice") ?? Dec(product["unit_price"]);
            var disc = l.Value<decimal?>("discountPercent") ?? 0m;
            var vatId = VatIdFor(l, product);
            var rate = VatRate(vatId);
            var lineTotal = Round(qty * price * (1m - disc / 100m));
            var vat = Round(lineTotal * rate / 100m);
            subtotal += lineTotal; vatTotal += vat;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["quote_id"] = quoteId;
            line["product_id"] = Guid.Parse(product["id"].ToString());
            line["quantity"] = qty;
            line["unit_price"] = price;
            line["discount_percent"] = disc;
            if (vatId != Guid.Empty) line["vat_code_id"] = vatId;
            line["line_total"] = lineTotal;
            var lr = recMan.CreateRecord("quote_line", line);
            if (!lr.Success) throw new InvalidOperationException(Err(lr, "create quote line"));

            outLines.Add(new { sku = product["sku"], quantity = qty, unit_price = price, discount_percent = disc, vat_rate = rate, line_total = lineTotal, vat_amount = vat });
        }

        var upd = new EntityRecord { ["id"] = quoteId, ["subtotal"] = Round(subtotal), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(subtotal + vatTotal) };
        recMan.UpdateRecord("quote", upd);

        return new { quote_id = quoteId, quote_number = quote["quote_number"], subtotal = Round(subtotal), vat_total = Round(vatTotal), grand_total = Round(subtotal + vatTotal), lines = outLines };
    }

    /// <summary>Convert an accepted quote into a confirmed sales order, copying its lines.</summary>
    public static object ConvertQuoteToOrder(Guid quoteId)
    {
        var quote = FindById("quote", quoteId) ?? throw new InvalidOperationException("Quote not found.");
        if (quote["status"]?.ToString() == "converted")
            throw new InvalidOperationException("Quote already converted.");

        var recMan = new RecordManager();
        var order = new EntityRecord();
        order["id"] = Guid.NewGuid();
        order["order_number"] = NextNumber("SO");
        order["customer_id"] = Guid.Parse(quote["customer_id"].ToString());
        order["quote_id"] = quoteId;
        order["order_date"] = DateTime.Today;
        order["status"] = "confirmed";
        order["currency"] = quote["currency"];
        if (Has(quote, "payment_term_id") && quote["payment_term_id"] != null)
            order["payment_term_id"] = Guid.Parse(quote["payment_term_id"].ToString());
        var created = recMan.CreateRecord("sales_order", order);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create order from quote"));
        var orderId = Id(created);

        var qLines = Query("SELECT * FROM quote_line WHERE quote_id = @id", quoteId);
        decimal subtotal = 0m, vatTotal = 0m;
        foreach (var ql in qLines)
        {
            var qty = Dec(ql["quantity"]);
            var price = Dec(ql["unit_price"]);
            var disc = Dec(ql["discount_percent"]);
            var lineTotal = Round(qty * price * (1m - disc / 100m));
            var vatId = GuidOrEmpty(ql, "vat_code_id");
            var vat = Round(lineTotal * VatRate(vatId) / 100m);
            subtotal += lineTotal; vatTotal += vat;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["sales_order_id"] = orderId;
            line["product_id"] = Guid.Parse(ql["product_id"].ToString());
            line["quantity"] = qty;
            line["unit_price"] = price;
            line["discount_percent"] = disc;
            if (vatId != Guid.Empty) line["vat_code_id"] = vatId;
            line["qty_delivered"] = 0m;
            line["qty_invoiced"] = 0m;
            line["line_total"] = lineTotal;
            recMan.CreateRecord("sales_order_line", line);
        }

        recMan.UpdateRecord("sales_order", new EntityRecord { ["id"] = orderId, ["total"] = Round(subtotal), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(subtotal + vatTotal) });
        recMan.UpdateRecord("quote", new EntityRecord { ["id"] = quoteId, ["status"] = "converted" });

        return new { order_id = orderId, order_number = order["order_number"], from_quote = quote["quote_number"], total = Round(subtotal), vat_total = Round(vatTotal), grand_total = Round(subtotal + vatTotal) };
    }

    /// <summary>Place a sales order with lines; totals include VAT. Uses the customer's default warehouse and payment term.</summary>
    public static object PlaceSalesOrder(Guid customerId, JArray lines, string orderDate, string requiredDate, string currency)
    {
        if (lines == null || lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");
        var customer = FindById("customer", customerId) ?? throw new InvalidOperationException("Customer not found.");
        if (Has(customer, "blocked") && customer["blocked"] is bool b && b)
            throw new InvalidOperationException("Customer is blocked; cannot place an order.");

        var recMan = new RecordManager();
        var order = new EntityRecord();
        order["id"] = Guid.NewGuid();
        order["order_number"] = NextNumber("SO");
        order["customer_id"] = customerId;
        order["order_date"] = ParseDate(orderDate) ?? DateTime.Today;
        if (!string.IsNullOrWhiteSpace(requiredDate)) order["required_date"] = ParseDate(requiredDate);
        order["status"] = "confirmed";
        order["currency"] = string.IsNullOrWhiteSpace(currency) ? (customer["currency"]?.ToString() ?? "EUR") : currency;
        if (Has(customer, "payment_term_id") && customer["payment_term_id"] != null)
            order["payment_term_id"] = Guid.Parse(customer["payment_term_id"].ToString());

        var created = recMan.CreateRecord("sales_order", order);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create sales order"));
        var orderId = Id(created);

        decimal subtotal = 0m, vatTotal = 0m;
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var product = FindProductBySku(l.Value<string>("sku")) ?? throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var price = l.Value<decimal?>("unitPrice") ?? Dec(product["unit_price"]);
            var disc = l.Value<decimal?>("discountPercent") ?? 0m;
            var vatId = VatIdFor(l, product);
            var rate = VatRate(vatId);
            var lineTotal = Round(qty * price * (1m - disc / 100m));
            var vat = Round(lineTotal * rate / 100m);
            subtotal += lineTotal; vatTotal += vat;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["sales_order_id"] = orderId;
            line["product_id"] = Guid.Parse(product["id"].ToString());
            line["quantity"] = qty;
            line["unit_price"] = price;
            line["discount_percent"] = disc;
            if (vatId != Guid.Empty) line["vat_code_id"] = vatId;
            line["qty_delivered"] = 0m;
            line["qty_invoiced"] = 0m;
            line["line_total"] = lineTotal;
            var lr = recMan.CreateRecord("sales_order_line", line);
            if (!lr.Success) throw new InvalidOperationException(Err(lr, "create order line"));

            outLines.Add(new { sku = product["sku"], quantity = qty, unit_price = price, discount_percent = disc, vat_rate = rate, line_total = lineTotal, vat_amount = vat });
        }

        recMan.UpdateRecord("sales_order", new EntityRecord { ["id"] = orderId, ["total"] = Round(subtotal), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(subtotal + vatTotal) });
        return new { order_id = orderId, order_number = order["order_number"], total = Round(subtotal), vat_total = Round(vatTotal), grand_total = Round(subtotal + vatTotal), lines = outLines };
    }

    /// <summary>Deliver a sales order (create a DDT): decrement stock, block over-delivery and negative stock.</summary>
    public static object DeliverSalesOrder(Guid orderId, JArray lines, string warehouseCode)
    {
        var r = DeliverCore(orderId, lines, warehouseCode, null, null, null);
        return new { delivery_id = r.deliveryId, delivery_number = r.deliveryNumber, warehouse = r.warehouseCode, delivered = r.delivered, order_status = r.orderStatus };
    }

    /// <summary>Deliver a sales order with transport details (cause, carrier, tracking) recorded on the DDT. Same validation and stock logic as DeliverSalesOrder.</summary>
    public static object DeliverWithTransport(Guid orderId, JArray lines, string warehouseCode, string transportCause, string carrier, string trackingNumber)
    {
        var r = DeliverCore(orderId, lines, warehouseCode, transportCause, carrier, trackingNumber);
        return new { delivery_id = r.deliveryId, delivery_number = r.deliveryNumber, warehouse = r.warehouseCode, delivered = r.delivered, order_status = r.orderStatus, transport_cause = transportCause ?? "", carrier = carrier ?? "", tracking_number = trackingNumber ?? "" };
    }

    // Shared delivery logic for DeliverSalesOrder and DeliverWithTransport. Transport fields
    // are written on the goods_delivery only when supplied, so the plain delivery is unchanged.
    private static (Guid deliveryId, string deliveryNumber, string warehouseCode, List<object> delivered, string orderStatus) DeliverCore(Guid orderId, JArray lines, string warehouseCode, string transportCause, string carrier, string trackingNumber)
    {
        var order = FindById("sales_order", orderId) ?? throw new InvalidOperationException("Sales order not found.");
        if (order["status"]?.ToString() == "cancelled") throw new InvalidOperationException("Cannot deliver a cancelled order.");
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one delivery line is required.");

        var recMan = new RecordManager();
        var warehouse = ResolveWarehouse(warehouseCode, order, "sales_order");

        // Validate every line BEFORE writing anything, so a rejected delivery (unknown SKU,
        // not on the order, over-delivery, or insufficient stock) leaves no orphan header or
        // partially-decremented stock.
        var prepared = new List<(EntityRecord oline, Guid productId, decimal qty, string lot, DateTime? expiry)>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var product = FindProductBySku(sku) ?? throw UnknownSku(l);
            var productId = Guid.Parse(product["id"].ToString());

            var oline = Query("SELECT * FROM sales_order_line WHERE sales_order_id = @id AND product_id = @p", orderId, ("p", productId)).FirstOrDefault()
                ?? throw new InvalidOperationException($"Product {sku} is not on this order.");
            var ordered = Dec(oline["quantity"]);
            var alreadyDelivered = Dec(oline["qty_delivered"]);
            if (alreadyDelivered + qty > ordered)
                throw new InvalidOperationException($"Cannot deliver {qty} of {sku}: only {Round(ordered - alreadyDelivered)} of {ordered} still pending.");
            if (!warehouse.allowNegative && StockQty(productId, warehouse.id) + 0.0001m < qty)
                throw new InvalidOperationException($"Insufficient stock: {qty} of {sku} required but only {Round(StockQty(productId, warehouse.id))} available in {warehouse.code} (negative stock not allowed).");

            prepared.Add((oline, productId, qty, l.Value<string>("lot"), ParseDate(l.Value<string>("expiry"))));
        }

        var delivery = new EntityRecord();
        delivery["id"] = Guid.NewGuid();
        delivery["delivery_number"] = NextNumber("DDT");
        delivery["sales_order_id"] = orderId;
        delivery["customer_id"] = Guid.Parse(order["customer_id"].ToString());
        delivery["warehouse_id"] = warehouse.id;
        delivery["delivery_date"] = DateTime.Today;
        delivery["status"] = "confirmed";
        if (!string.IsNullOrWhiteSpace(transportCause)) delivery["transport_cause"] = transportCause;
        if (!string.IsNullOrWhiteSpace(carrier)) delivery["carrier"] = carrier;
        if (!string.IsNullOrWhiteSpace(trackingNumber)) delivery["tracking_number"] = trackingNumber;
        var dr = recMan.CreateRecord("goods_delivery", delivery);
        if (!dr.Success) throw new InvalidOperationException(Err(dr, "create delivery"));
        var deliveryId = Id(dr);

        var delivered = new List<object>();
        foreach (var p in prepared)
        {
            AdjustStock(recMan, p.productId, warehouse.id, -p.qty, "issue", "goods_delivery", deliveryId, Dec(FindById("product", p.productId)["unit_cost"]), p.lot, p.expiry, warehouse.allowNegative);

            recMan.CreateRecord("goods_delivery_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["delivery_id"] = deliveryId,
                ["product_id"] = p.productId,
                ["quantity"] = p.qty,
                ["lot_number"] = p.lot ?? "",
                ["expiry_date"] = p.expiry ?? (object)null
            });

            recMan.UpdateRecord("sales_order_line", new EntityRecord { ["id"] = Guid.Parse(p.oline["id"].ToString()), ["qty_delivered"] = Round(Dec(p.oline["qty_delivered"]) + p.qty) });
            delivered.Add(new { sku = FindById("product", p.productId)["sku"], quantity = p.qty, lot_number = p.lot });
        }

        var status = ComputeDeliveryStatus(orderId);
        recMan.UpdateRecord("sales_order", new EntityRecord { ["id"] = orderId, ["status"] = status });
        return (deliveryId, delivery["delivery_number"].ToString(), warehouse.code, delivered, status);
    }

    /// <summary>Invoice the delivered-but-not-yet-invoiced quantity of a sales order, with VAT lines.</summary>
    public static object InvoiceSalesOrder(Guid orderId, int dueDays)
    {
        var order = FindById("sales_order", orderId) ?? throw new InvalidOperationException("Sales order not found.");
        var status = order["status"]?.ToString();
        if (status == "cancelled") throw new InvalidOperationException("Cannot invoice a cancelled order.");

        var recMan = new RecordManager();
        var oLines = Query("SELECT * FROM sales_order_line WHERE sales_order_id = @id", orderId);

        var inv = new EntityRecord();
        inv["id"] = Guid.NewGuid();
        inv["invoice_number"] = NextNumber("INV");
        inv["customer_id"] = Guid.Parse(order["customer_id"].ToString());
        inv["sales_order_id"] = orderId;
        inv["invoice_type"] = "immediate";
        inv["issue_date"] = DateTime.Today;
        inv["due_date"] = DateTime.Today.AddDays(dueDays <= 0 ? 30 : dueDays);
        inv["status"] = "sent";
        if (Has(order, "payment_term_id") && order["payment_term_id"] != null)
            inv["payment_term_id"] = Guid.Parse(order["payment_term_id"].ToString());
        var ir = recMan.CreateRecord("invoice", inv);
        if (!ir.Success) throw new InvalidOperationException(Err(ir, "create invoice"));
        var invoiceId = Id(ir);

        decimal amount = 0m, vatTotal = 0m;
        var invLines = new List<object>();
        foreach (var ol in oLines)
        {
            var qtyToInvoice = Round(Dec(ol["qty_delivered"]) - Dec(ol["qty_invoiced"]));
            if (qtyToInvoice <= 0m) continue;
            var price = Dec(ol["unit_price"]);
            var disc = Dec(ol["discount_percent"]);
            var lineTotal = Round(qtyToInvoice * price * (1m - disc / 100m));
            var vatId = GuidOrEmpty(ol, "vat_code_id");
            var vat = Round(lineTotal * VatRate(vatId) / 100m);
            amount += lineTotal; vatTotal += vat;

            recMan.CreateRecord("invoice_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["invoice_id"] = invoiceId,
                ["product_id"] = Guid.Parse(ol["product_id"].ToString()),
                ["quantity"] = qtyToInvoice,
                ["unit_price"] = price,
                ["discount_percent"] = disc,
                ["vat_code_id"] = vatId == Guid.Empty ? (object)null : vatId,
                ["line_total"] = lineTotal,
                ["vat_amount"] = vat
            });
            recMan.UpdateRecord("sales_order_line", new EntityRecord { ["id"] = Guid.Parse(ol["id"].ToString()), ["qty_invoiced"] = Round(Dec(ol["qty_invoiced"]) + qtyToInvoice) });
            invLines.Add(new { product_id = ol["product_id"], quantity = qtyToInvoice, line_total = lineTotal, vat_amount = vat });
        }

        if (invLines.Count == 0)
        {
            // Nothing delivered — fall back to invoicing the full order total (immediate billing).
            recMan.DeleteRecord("invoice", invoiceId);
            throw new InvalidOperationException("Nothing delivered to invoice. Deliver the order first.");
        }

        recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = invoiceId, ["amount"] = Round(amount), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(amount + vatTotal) });
        recMan.UpdateRecord("sales_order", new EntityRecord { ["id"] = orderId, ["status"] = "invoiced" });

        return new { invoice_id = invoiceId, invoice_number = inv["invoice_number"], amount = Round(amount), vat_total = Round(vatTotal), grand_total = Round(amount + vatTotal), due_date = ((DateTime)inv["due_date"]).ToString("yyyy-MM-dd"), lines = invLines };
    }

    /// <summary>Record a payment against a customer invoice; payable is the VAT-inclusive grand total.</summary>
    public static object RecordPayment(Guid invoiceId, decimal amount, string method, string paymentDate)
    {
        var invoice = FindById("invoice", invoiceId) ?? throw new InvalidOperationException("Invoice not found.");
        if (amount <= 0m) throw new InvalidOperationException("Payment amount must be positive.");

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
        var paymentId = Id(r);

        var payable = PayableOf(invoice);
        var paidTotal = Sum("SELECT amount FROM payment WHERE invoice_id = @id", invoiceId);
        var newStatus = paidTotal >= payable ? "paid" : "partial";
        recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = invoiceId, ["status"] = newStatus });

        return new { payment_id = paymentId, payment_ref = pay["payment_ref"], invoice_status = newStatus, paid_total = Round(paidTotal), balance = Round(payable - paidTotal) };
    }

    /// <summary>Issue a credit note against an invoice; reduces the invoice's outstanding balance.</summary>
    public static object CreateCreditNote(Guid invoiceId, decimal amount, string reason)
    {
        var invoice = FindById("invoice", invoiceId) ?? throw new InvalidOperationException("Invoice not found.");
        if (amount <= 0m) throw new InvalidOperationException("Credit amount must be positive.");

        var recMan = new RecordManager();
        // VAT ratio from the invoice (guard divide-by-zero).
        var baseAmt = Dec(invoice["amount"]);
        var vatRatio = baseAmt != 0m ? Dec(invoice["vat_total"]) / baseAmt : 0m;
        var vatTotal = Round(amount * vatRatio);

        var cn = new EntityRecord();
        cn["id"] = Guid.NewGuid();
        cn["credit_note_number"] = NextNumber("CRN");
        cn["invoice_id"] = invoiceId;
        cn["customer_id"] = Guid.Parse(invoice["customer_id"].ToString());
        cn["credit_date"] = DateTime.Today;
        cn["amount"] = Round(amount);
        cn["vat_total"] = vatTotal;
        cn["reason"] = reason ?? "";
        cn["status"] = "issued";
        var r = recMan.CreateRecord("credit_note", cn);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create credit note"));
        var cnId = Id(r);

        var payable = PayableOf(invoice);
        var paid = Sum("SELECT amount FROM payment WHERE invoice_id = @id", invoiceId);
        var credited = Sum("SELECT amount FROM credit_note WHERE invoice_id = @id", invoiceId);
        var outstanding = payable - paid - credited;
        var newStatus = outstanding <= 0.005m ? "credited" : invoice["status"]?.ToString();
        recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = invoiceId, ["status"] = newStatus });

        return new { credit_note_id = cnId, credit_note_number = cn["credit_note_number"], amount = Round(amount), vat_total = vatTotal, invoice_status = newStatus, outstanding = Round(outstanding) };
    }

    // ══════════════════════════════════════════════
    //  PURCHASING
    // ══════════════════════════════════════════════

    /// <summary>Create a purchase request (richiesta d'acquisto) with lines.</summary>
    public static object CreatePurchaseRequest(JArray lines, string requestedBy)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one line item is required.");
        var recMan = new RecordManager();
        var req = new EntityRecord
        {
            ["id"] = Guid.NewGuid(),
            ["request_number"] = NextNumber("PR"),
            ["requested_by"] = requestedBy ?? "",
            ["request_date"] = DateTime.Today,
            ["status"] = "draft"
        };
        var cr = recMan.CreateRecord("purchase_request", req);
        if (!cr.Success) throw new InvalidOperationException(Err(cr, "create purchase request"));
        var reqId = Id(cr);

        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var product = FindProductBySku(l.Value<string>("sku")) ?? throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var est = l.Value<decimal?>("estimatedCost") ?? Dec(product["unit_cost"]);
            recMan.CreateRecord("purchase_request_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["request_id"] = reqId,
                ["product_id"] = Guid.Parse(product["id"].ToString()),
                ["quantity"] = qty,
                ["estimated_cost"] = est
            });
            outLines.Add(new { sku = product["sku"], quantity = qty, estimated_cost = est });
        }
        return new { request_id = reqId, request_number = req["request_number"], lines = outLines };
    }

    /// <summary>Convert an approved purchase request into a purchase order for a supplier.</summary>
    public static object ConvertRequestToPurchaseOrder(Guid requestId, Guid supplierId)
    {
        var req = FindById("purchase_request", requestId) ?? throw new InvalidOperationException("Purchase request not found.");
        if (req["status"]?.ToString() == "converted") throw new InvalidOperationException("Request already converted.");
        var supplier = FindById("supplier", supplierId) ?? throw new InvalidOperationException("Supplier not found.");
        if (Has(supplier, "blocked") && supplier["blocked"] is bool b && b)
            throw new InvalidOperationException("Supplier is blocked; cannot place a purchase order.");

        var recMan = new RecordManager();
        var po = new EntityRecord();
        po["id"] = Guid.NewGuid();
        po["po_number"] = NextNumber("PO");
        po["supplier_id"] = supplierId;
        po["request_id"] = requestId;
        po["order_date"] = DateTime.Today;
        po["status"] = "ordered";
        po["currency"] = supplier["currency"]?.ToString() ?? "EUR";
        if (Has(supplier, "payment_term_id") && supplier["payment_term_id"] != null)
            po["payment_term_id"] = Guid.Parse(supplier["payment_term_id"].ToString());
        var cr = recMan.CreateRecord("purchase_order", po);
        if (!cr.Success) throw new InvalidOperationException(Err(cr, "create PO from request"));
        var poId = Id(cr);

        var rLines = Query("SELECT * FROM purchase_request_line WHERE request_id = @id", requestId);
        decimal subtotal = 0m, vatTotal = 0m;
        foreach (var rl in rLines)
        {
            var productId = Guid.Parse(rl["product_id"].ToString());
            var qty = Dec(rl["quantity"]);
            var cost = Dec(rl["estimated_cost"]);
            var product = FindById("product", productId);
            var vatId = product != null ? GuidOrEmpty(product, "vat_code_id") : Guid.Empty;
            var lineTotal = Round(qty * cost);
            var vat = Round(lineTotal * VatRate(vatId) / 100m);
            subtotal += lineTotal; vatTotal += vat;

            recMan.CreateRecord("purchase_order_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["purchase_order_id"] = poId,
                ["product_id"] = productId,
                ["quantity"] = qty,
                ["unit_cost"] = cost,
                ["vat_code_id"] = vatId == Guid.Empty ? (object)null : vatId,
                ["qty_received"] = 0m,
                ["line_total"] = lineTotal
            });
        }
        recMan.UpdateRecord("purchase_order", new EntityRecord { ["id"] = poId, ["total"] = Round(subtotal), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(subtotal + vatTotal) });
        recMan.UpdateRecord("purchase_request", new EntityRecord { ["id"] = requestId, ["status"] = "converted" });
        return new { purchase_order_id = poId, po_number = po["po_number"], from_request = req["request_number"], total = Round(subtotal), vat_total = Round(vatTotal), grand_total = Round(subtotal + vatTotal) };
    }

    /// <summary>Place a purchase order with lines; totals include VAT.</summary>
    public static object PlacePurchaseOrder(Guid supplierId, JArray lines, string orderDate, string expectedDate)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one line item is required.");
        var supplier = FindById("supplier", supplierId) ?? throw new InvalidOperationException("Supplier not found.");
        if (Has(supplier, "blocked") && supplier["blocked"] is bool b && b)
            throw new InvalidOperationException("Supplier is blocked; cannot place a purchase order.");

        var recMan = new RecordManager();
        var po = new EntityRecord();
        po["id"] = Guid.NewGuid();
        po["po_number"] = NextNumber("PO");
        po["supplier_id"] = supplierId;
        po["order_date"] = ParseDate(orderDate) ?? DateTime.Today;
        if (!string.IsNullOrWhiteSpace(expectedDate)) po["expected_date"] = ParseDate(expectedDate);
        po["status"] = "ordered";
        po["currency"] = supplier["currency"]?.ToString() ?? "EUR";
        if (Has(supplier, "payment_term_id") && supplier["payment_term_id"] != null)
            po["payment_term_id"] = Guid.Parse(supplier["payment_term_id"].ToString());
        var created = recMan.CreateRecord("purchase_order", po);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create purchase order"));
        var poId = Id(created);

        decimal subtotal = 0m, vatTotal = 0m;
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var product = FindProductBySku(l.Value<string>("sku")) ?? throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var cost = l.Value<decimal?>("unitCost") ?? Dec(product["unit_cost"]);
            var vatId = VatIdFor(l, product);
            var lineTotal = Round(qty * cost);
            var vat = Round(lineTotal * VatRate(vatId) / 100m);
            subtotal += lineTotal; vatTotal += vat;

            recMan.CreateRecord("purchase_order_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["purchase_order_id"] = poId,
                ["product_id"] = Guid.Parse(product["id"].ToString()),
                ["quantity"] = qty,
                ["unit_cost"] = cost,
                ["vat_code_id"] = vatId == Guid.Empty ? (object)null : vatId,
                ["qty_received"] = 0m,
                ["line_total"] = lineTotal
            });
            outLines.Add(new { sku = product["sku"], quantity = qty, unit_cost = cost, line_total = lineTotal });
        }
        recMan.UpdateRecord("purchase_order", new EntityRecord { ["id"] = poId, ["total"] = Round(subtotal), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(subtotal + vatTotal) });
        return new { purchase_order_id = poId, po_number = po["po_number"], total = Round(subtotal), vat_total = Round(vatTotal), grand_total = Round(subtotal + vatTotal), lines = outLines };
    }

    /// <summary>Receive goods against a purchase order: increment stock, record variance vs ordered.</summary>
    public static object ReceivePurchaseOrder(Guid poId, JArray lines, string warehouseCode)
    {
        var po = FindById("purchase_order", poId) ?? throw new InvalidOperationException("Purchase order not found.");
        if (po["status"]?.ToString() == "cancelled") throw new InvalidOperationException("Cannot receive a cancelled PO.");
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one receipt line is required.");

        var recMan = new RecordManager();
        var warehouse = ResolveWarehouse(warehouseCode, po, "purchase_order");

        var receipt = new EntityRecord();
        receipt["id"] = Guid.NewGuid();
        receipt["receipt_number"] = NextNumber("GRT");
        receipt["purchase_order_id"] = poId;
        receipt["supplier_id"] = Guid.Parse(po["supplier_id"].ToString());
        receipt["warehouse_id"] = warehouse.id;
        receipt["receipt_date"] = DateTime.Today;
        receipt["status"] = "confirmed";
        var rr = recMan.CreateRecord("goods_receipt", receipt);
        if (!rr.Success) throw new InvalidOperationException(Err(rr, "create goods receipt"));
        var receiptId = Id(rr);

        var received = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var product = FindProductBySku(sku) ?? throw UnknownSku(l);
            var productId = Guid.Parse(product["id"].ToString());
            var poline = Query("SELECT * FROM purchase_order_line WHERE purchase_order_id = @id AND product_id = @p", poId, ("p", productId)).FirstOrDefault()
                ?? throw new InvalidOperationException($"Product {sku} is not on this purchase order.");
            var ordered = Dec(poline["quantity"]);
            var cost = l.Value<decimal?>("unitCost") ?? Dec(poline["unit_cost"]);
            var lot = l.Value<string>("lot");
            var expiry = ParseDate(l.Value<string>("expiry"));

            AdjustStock(recMan, productId, warehouse.id, qty, "receipt", "goods_receipt", receiptId, cost, lot, expiry, true);
            recMan.UpdateRecord("purchase_order_line", new EntityRecord { ["id"] = Guid.Parse(poline["id"].ToString()), ["qty_received"] = Round(Dec(poline["qty_received"]) + qty) });

            recMan.CreateRecord("goods_receipt_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["receipt_id"] = receiptId,
                ["product_id"] = productId,
                ["quantity_ordered"] = ordered,
                ["quantity_received"] = qty,
                ["unit_cost"] = cost,
                ["lot_number"] = lot ?? "",
                ["expiry_date"] = expiry ?? (object)null
            });
            received.Add(new { sku, quantity_ordered = ordered, quantity_received = qty, variance = Round(qty - ordered), lot_number = lot });
        }

        var status = ComputeReceiveStatus(poId);
        recMan.UpdateRecord("purchase_order", new EntityRecord { ["id"] = poId, ["status"] = status });
        return new { receipt_id = receiptId, receipt_number = receipt["receipt_number"], warehouse = warehouse.code, received, po_status = status };
    }

    /// <summary>Register a supplier bill against a PO and run the triple-match check (ordered vs received vs billed).</summary>
    public static object RegisterPurchaseInvoice(Guid poId, JArray lines, string billDate, string dueDate)
    {
        var po = FindById("purchase_order", poId) ?? throw new InvalidOperationException("Purchase order not found.");
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one bill line is required.");

        var recMan = new RecordManager();
        var bill = new EntityRecord();
        bill["id"] = Guid.NewGuid();
        bill["bill_number"] = NextNumber("BILL");
        bill["supplier_id"] = Guid.Parse(po["supplier_id"].ToString());
        bill["purchase_order_id"] = poId;
        bill["bill_date"] = ParseDate(billDate) ?? DateTime.Today;
        bill["due_date"] = ParseDate(dueDate) ?? DateTime.Today.AddDays(30);
        bill["status"] = "registered";
        bill["triple_match_status"] = "pending";
        var br = recMan.CreateRecord("purchase_invoice", bill);
        if (!br.Success) throw new InvalidOperationException(Err(br, "create purchase invoice"));
        var billId = Id(br);

        decimal amount = 0m, vatTotal = 0m;
        bool allMatched = true;
        var mismatches = new List<object>();
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var cost = l.Value<decimal?>("unitCost") ?? 0m;
            var product = FindProductBySku(sku) ?? throw UnknownSku(l);
            var productId = Guid.Parse(product["id"].ToString());
            var poline = Query("SELECT * FROM purchase_order_line WHERE purchase_order_id = @id AND product_id = @p", poId, ("p", productId)).FirstOrDefault();

            var received = poline != null ? Dec(poline["qty_received"]) : 0m;
            var poCost = poline != null ? Dec(poline["unit_cost"]) : 0m;
            var qtyOk = qty <= received + 0.0001m;
            var costOk = Math.Abs(cost - poCost) < 0.01m;
            if (!qtyOk || !costOk)
            {
                allMatched = false;
                mismatches.Add(new { sku, billed_qty = qty, received_qty = received, billed_cost = cost, po_cost = poCost, qty_ok = qtyOk, cost_ok = costOk });
            }

            var vatId = poline != null ? GuidOrEmpty(poline, "vat_code_id") : GuidOrEmpty(product, "vat_code_id");
            var lineTotal = Round(qty * cost);
            var vat = Round(lineTotal * VatRate(vatId) / 100m);
            amount += lineTotal; vatTotal += vat;

            recMan.CreateRecord("purchase_invoice_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["bill_id"] = billId,
                ["product_id"] = productId,
                ["quantity"] = qty,
                ["unit_cost"] = cost,
                ["vat_code_id"] = vatId == Guid.Empty ? (object)null : vatId,
                ["line_total"] = lineTotal,
                ["vat_amount"] = vat
            });
            outLines.Add(new { sku, quantity = qty, unit_cost = cost, line_total = lineTotal });
        }

        var match = allMatched ? "matched" : "mismatched";
        recMan.UpdateRecord("purchase_invoice", new EntityRecord { ["id"] = billId, ["amount"] = Round(amount), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(amount + vatTotal), ["triple_match_status"] = match });
        recMan.UpdateRecord("purchase_order", new EntityRecord { ["id"] = poId, ["status"] = "invoiced" });
        return new { bill_id = billId, bill_number = bill["bill_number"], amount = Round(amount), vat_total = Round(vatTotal), grand_total = Round(amount + vatTotal), triple_match_status = match, mismatches };
    }

    /// <summary>Pay a supplier bill; payable is the VAT-inclusive grand total.</summary>
    public static object PaySupplierBill(Guid billId, decimal amount, string method, string paymentDate)
    {
        var bill = FindById("purchase_invoice", billId) ?? throw new InvalidOperationException("Purchase invoice not found.");
        if (amount <= 0m) throw new InvalidOperationException("Payment amount must be positive.");

        var recMan = new RecordManager();
        var pay = new EntityRecord();
        pay["id"] = Guid.NewGuid();
        pay["payment_ref"] = NextNumber("SPAY");
        pay["bill_id"] = billId;
        pay["supplier_id"] = Guid.Parse(bill["supplier_id"].ToString());
        pay["amount"] = amount;
        pay["payment_date"] = ParseDate(paymentDate) ?? DateTime.Today;
        pay["method"] = string.IsNullOrWhiteSpace(method) ? "bank" : method;
        var r = recMan.CreateRecord("supplier_payment", pay);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create supplier payment"));
        var payId = Id(r);

        var payable = PayableOf(bill);
        var paidTotal = Sum("SELECT amount FROM supplier_payment WHERE bill_id = @id", billId);
        var newStatus = paidTotal >= payable ? "paid" : "partial";
        recMan.UpdateRecord("purchase_invoice", new EntityRecord { ["id"] = billId, ["status"] = newStatus });
        return new { payment_id = payId, payment_ref = pay["payment_ref"], bill_status = newStatus, paid_total = Round(paidTotal), balance = Round(payable - paidTotal) };
    }

    /// <summary>Issue a credit note from a supplier against a bill.</summary>
    public static object CreateSupplierCreditNote(Guid billId, decimal amount, string reason)
    {
        var bill = FindById("purchase_invoice", billId) ?? throw new InvalidOperationException("Purchase invoice not found.");
        if (amount <= 0m) throw new InvalidOperationException("Credit amount must be positive.");
        var recMan = new RecordManager();
        var cn = new EntityRecord
        {
            ["id"] = Guid.NewGuid(),
            ["credit_note_number"] = NextNumber("SCRN"),
            ["bill_id"] = billId,
            ["supplier_id"] = Guid.Parse(bill["supplier_id"].ToString()),
            ["credit_date"] = DateTime.Today,
            ["amount"] = Round(amount),
            ["reason"] = reason ?? "",
            ["status"] = "issued"
        };
        var r = recMan.CreateRecord("supplier_credit_note", cn);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create supplier credit note"));
        return new { credit_note_id = Id(r), credit_note_number = cn["credit_note_number"], amount = Round(amount) };
    }

    // ══════════════════════════════════════════════
    //  WAREHOUSE
    // ══════════════════════════════════════════════

    /// <summary>Transfer stock between two warehouses; blocks a negative source.</summary>
    public static object TransferStock(string sku, string fromWarehouseCode, string toWarehouseCode, decimal quantity)
    {
        if (quantity <= 0m) throw new InvalidOperationException("Transfer quantity must be positive.");
        var product = FindProductBySku(sku) ?? throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
        var productId = Guid.Parse(product["id"].ToString());
        var from = ResolveWarehouseByCode(fromWarehouseCode);
        var to = ResolveWarehouseByCode(toWarehouseCode);
        if (from.id == to.id) throw new InvalidOperationException("Source and destination warehouses must differ.");

        var recMan = new RecordManager();
        var refId = Guid.NewGuid();
        AdjustStock(recMan, productId, from.id, -quantity, "transfer_out", "transfer", refId, Dec(product["unit_cost"]), null, null, from.allowNegative);
        AdjustStock(recMan, productId, to.id, quantity, "transfer_in", "transfer", refId, Dec(product["unit_cost"]), null, null, true);
        return new { sku, from = from.code, to = to.code, quantity = Round(quantity), transfer_ref = refId };
    }

    /// <summary>Manually adjust a product's stock in a warehouse to a new quantity (records the delta).</summary>
    public static object AdjustStockManual(string sku, string warehouseCode, decimal newQuantity, string reason)
    {
        var product = FindProductBySku(sku) ?? throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
        var productId = Guid.Parse(product["id"].ToString());
        var wh = ResolveWarehouseByCode(warehouseCode);
        var recMan = new RecordManager();
        var current = StockQty(productId, wh.id);
        var delta = Round(newQuantity - current);
        AdjustStock(recMan, productId, wh.id, delta, "adjustment", "manual", Guid.NewGuid(), Dec(product["unit_cost"]), null, null, wh.allowNegative);
        return new { sku, warehouse = wh.code, old_quantity = Round(current), new_quantity = Round(newQuantity), delta, reason = reason ?? "" };
    }

    // ══════════════════════════════════════════════
    //  v3 MASTER DATA, PRICING, RETURNS
    // ══════════════════════════════════════════════

    /// <summary>Create a customer with billing and shipping addresses and contacts in one call.</summary>
    public static object CreateCustomerProfile(string name, JObject billing, JObject ship, JArray contacts, string email, string currency)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Customer name is required.");
        var recMan = new RecordManager();

        var cust = new EntityRecord();
        cust["id"] = Guid.NewGuid();
        cust["name"] = name;
        cust["category"] = "retail";
        cust["blocked"] = false;
        if (!string.IsNullOrWhiteSpace(email)) cust["email"] = email;
        if (!string.IsNullOrWhiteSpace(currency)) cust["currency"] = currency;
        var cr = recMan.CreateRecord("customer", cust);
        if (!cr.Success) throw new InvalidOperationException(Err(cr, "create customer"));
        var customerId = Id(cr);

        // Billing is always created and is the default address; ship only when supplied.
        var addresses = new List<object> { CreateAddress(recMan, customerId, "billing", billing, true) };
        if (ship != null) addresses.Add(CreateAddress(recMan, customerId, "ship", ship, false));

        var outContacts = new List<object>();
        bool first = true;
        if (contacts != null)
        {
            foreach (var c in contacts)
            {
                var ct = new EntityRecord();
                ct["id"] = Guid.NewGuid();
                ct["customer_id"] = customerId;
                ct["name"] = c.Value<string>("name") ?? "";
                if (!string.IsNullOrWhiteSpace(c.Value<string>("role"))) ct["role"] = c.Value<string>("role");
                if (!string.IsNullOrWhiteSpace(c.Value<string>("email"))) ct["email"] = c.Value<string>("email");
                if (!string.IsNullOrWhiteSpace(c.Value<string>("phone"))) ct["phone"] = c.Value<string>("phone");
                ct["is_primary"] = first;
                var ctr = recMan.CreateRecord("contact", ct);
                if (!ctr.Success) throw new InvalidOperationException(Err(ctr, "create contact"));
                outContacts.Add(new { name = ct["name"], email = c.Value<string>("email") ?? "", is_primary = first });
                first = false;
            }
        }

        return new { customer_id = customerId, name, addresses, contacts = outContacts };
    }

    private static object CreateAddress(RecordManager recMan, Guid customerId, string kind, JObject addr, bool isDefault)
    {
        var rec = new EntityRecord();
        rec["id"] = Guid.NewGuid();
        rec["customer_id"] = customerId;
        rec["kind"] = kind;
        rec["is_default"] = isDefault;
        string line1 = null, city = null, country = null;
        if (addr != null)
        {
            line1 = addr.Value<string>("line1");
            city = addr.Value<string>("city");
            country = addr.Value<string>("country");
            var postal = addr.Value<string>("postal_code");
            if (!string.IsNullOrWhiteSpace(line1)) rec["line1"] = line1;
            if (!string.IsNullOrWhiteSpace(city)) rec["city"] = city;
            if (!string.IsNullOrWhiteSpace(country)) rec["country"] = country;
            if (!string.IsNullOrWhiteSpace(postal)) rec["postal_code"] = postal;
        }
        var r = recMan.CreateRecord("customer_address", rec);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create customer address"));
        return new { kind, line1 = line1 ?? "", city = city ?? "", country = country ?? "" };
    }

    /// <summary>Add barcodes and suppliers to an existing product (by SKU). Duplicate barcode codes are skipped.</summary>
    public static object EnrichProduct(string sku, JArray barcodes, JArray suppliers)
    {
        var product = FindProductBySku(sku) ?? throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
        var productId = Guid.Parse(product["id"].ToString());
        var recMan = new RecordManager();

        int barcodesAdded = 0;
        if (barcodes != null)
        {
            foreach (var b in barcodes)
            {
                var code = b.Value<string>("code");
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (Query("SELECT id FROM product_barcode WHERE code = @c", "c", code).Count > 0) continue;
                var rec = new EntityRecord();
                rec["id"] = Guid.NewGuid();
                rec["product_id"] = productId;
                rec["code"] = code;
                rec["barcode_type"] = string.IsNullOrWhiteSpace(b.Value<string>("barcode_type")) ? "ean13" : b.Value<string>("barcode_type");
                var r = recMan.CreateRecord("product_barcode", rec);
                if (!r.Success) throw new InvalidOperationException(Err(r, "create product barcode"));
                barcodesAdded++;
            }
        }

        int suppliersAdded = 0;
        if (suppliers != null)
        {
            foreach (var s in suppliers)
            {
                var supplierName = s.Value<string>("supplier_name");
                var sup = Query("SELECT id FROM supplier WHERE name = @n", "n", supplierName ?? "").FirstOrDefault()
                    ?? throw new InvalidOperationException($"Unknown supplier '{supplierName}'.");
                var rec = new EntityRecord();
                rec["id"] = Guid.NewGuid();
                rec["product_id"] = productId;
                rec["supplier_id"] = Guid.Parse(sup["id"].ToString());
                if (!string.IsNullOrWhiteSpace(s.Value<string>("supplier_sku"))) rec["supplier_sku"] = s.Value<string>("supplier_sku");
                rec["lead_time_days"] = s.Value<decimal?>("lead_time_days") ?? 0m;
                rec["unit_cost"] = s.Value<decimal?>("unit_cost") ?? 0m;
                rec["is_default"] = s.Value<bool?>("is_default") ?? false;
                var r = recMan.CreateRecord("product_supplier", rec);
                if (!r.Success) throw new InvalidOperationException(Err(r, "create product supplier"));
                suppliersAdded++;
            }
        }

        return new { sku, barcodes_added = barcodesAdded, suppliers_added = suppliersAdded };
    }

    /// <summary>Resolve the effective unit price for a SKU for a customer and quantity: price-list base, best discount rule, currency conversion.</summary>
    public static object ResolvePrice(string sku, string customerName, decimal qty)
    {
        var product = FindProductBySku(sku) ?? throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
        var productId = Guid.Parse(product["id"].ToString());
        var customer = Query("SELECT * FROM customer WHERE name = @n", "n", customerName ?? "").FirstOrDefault()
            ?? throw new InvalidOperationException($"Customer '{customerName}' not found.");

        var category = customer["category"]?.ToString() ?? "retail";
        var currency = string.IsNullOrWhiteSpace(customer["currency"]?.ToString()) ? "EUR" : customer["currency"].ToString();

        // Base price: the matching price-list item for the customer's category, else the catalog unit price.
        decimal basePrice = Dec(product["unit_price"]);
        var priceList = Query("SELECT * FROM price_list WHERE customer_category = @c", "c", category).FirstOrDefault();
        if (priceList != null)
        {
            var plId = Guid.Parse(priceList["id"].ToString());
            var item = Query("SELECT * FROM price_list_item WHERE price_list_id = @id AND product_id = @p", plId, ("p", productId)).FirstOrDefault();
            if (item != null) basePrice = Dec(item["unit_price"]);
        }

        // Discount: the MAX matching rule (product/category/customer scope) whose min_qty the order meets.
        decimal discount = 0m;
        foreach (var rule in Query("SELECT * FROM discount_rule"))
        {
            var scope = rule["scope"]?.ToString();
            var scopeValue = rule["scope_value"]?.ToString() ?? "";
            var minQty = Dec(rule["min_qty"]);
            bool applies = (scope == "product" && scopeValue == sku)
                      || (scope == "category" && scopeValue == category)
                      || (scope == "customer" && scopeValue == customerName);
            if (applies && qty >= minQty)
            {
                var d = Dec(rule["discount_percent"]);
                if (d > discount) discount = d;
            }
        }

        decimal unit = basePrice * (1m - discount / 100m);
        // Rate rows are from_currency -> EUR; to go EUR -> X divide by the rate.
        if (!string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            var rateRow = Query("SELECT * FROM exchange_rate WHERE from_currency = @c", "c", currency)
                .FirstOrDefault(r => string.Equals(r["to_currency"]?.ToString(), "EUR", StringComparison.OrdinalIgnoreCase));
            if (rateRow != null)
            {
                var rate = Dec(rateRow["rate"]);
                if (rate != 0m) unit = unit / rate;
            }
        }

        return new { sku, customer = customerName, base_price = Round(basePrice), discount_percent = Round(discount), unit_price = Round(unit), currency };
    }

    /// <summary>Place a sales order by customer name with confirmation/planned-delivery dates, transport terms and a sales agent.</summary>
    public static object CreateOrderWithTerms(string customerName, JArray lines, string confirmedDate, string plannedDate, string transportTerms, string agentName)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one line item is required.");
        var customer = Query("SELECT * FROM customer WHERE name = @n", "n", customerName ?? "").FirstOrDefault()
            ?? throw new InvalidOperationException($"Customer '{customerName}' not found.");
        if (Has(customer, "blocked") && customer["blocked"] is bool b && b)
            throw new InvalidOperationException("Customer is blocked; cannot place an order.");
        var customerId = Guid.Parse(customer["id"].ToString());

        var recMan = new RecordManager();
        var order = new EntityRecord();
        order["id"] = Guid.NewGuid();
        order["order_number"] = NextNumber("SO");
        order["customer_id"] = customerId;
        order["order_date"] = DateTime.Today;
        order["status"] = "confirmed";
        order["currency"] = string.IsNullOrWhiteSpace(customer["currency"]?.ToString()) ? "EUR" : customer["currency"].ToString();
        if (Has(customer, "payment_term_id") && customer["payment_term_id"] != null)
            order["payment_term_id"] = Guid.Parse(customer["payment_term_id"].ToString());
        if (!string.IsNullOrWhiteSpace(confirmedDate)) order["confirmed_date"] = ParseDate(confirmedDate);
        if (!string.IsNullOrWhiteSpace(plannedDate)) order["planned_delivery_date"] = ParseDate(plannedDate);
        if (!string.IsNullOrWhiteSpace(transportTerms)) order["transport_terms"] = transportTerms;

        string agent = null;
        if (!string.IsNullOrWhiteSpace(agentName))
        {
            var ag = Query("SELECT id FROM sales_agent WHERE name = @n", "n", agentName).FirstOrDefault();
            if (ag != null) { order["sales_agent_id"] = Guid.Parse(ag["id"].ToString()); agent = agentName; }
        }

        var created = recMan.CreateRecord("sales_order", order);
        if (!created.Success) throw new InvalidOperationException(Err(created, "create sales order"));
        var orderId = Id(created);

        decimal subtotal = 0m, vatTotal = 0m;
        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var product = FindProductBySku(l.Value<string>("sku")) ?? throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var price = l.Value<decimal?>("unitPrice") ?? Dec(product["unit_price"]);
            var disc = l.Value<decimal?>("discountPercent") ?? 0m;
            var vatId = VatIdFor(l, product);
            var rate = VatRate(vatId);
            var lineTotal = Round(qty * price * (1m - disc / 100m));
            var vat = Round(lineTotal * rate / 100m);
            subtotal += lineTotal; vatTotal += vat;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["sales_order_id"] = orderId;
            line["product_id"] = Guid.Parse(product["id"].ToString());
            line["quantity"] = qty;
            line["unit_price"] = price;
            line["discount_percent"] = disc;
            if (vatId != Guid.Empty) line["vat_code_id"] = vatId;
            line["qty_delivered"] = 0m;
            line["qty_invoiced"] = 0m;
            line["line_total"] = lineTotal;
            var lr = recMan.CreateRecord("sales_order_line", line);
            if (!lr.Success) throw new InvalidOperationException(Err(lr, "create order line"));
            outLines.Add(new { sku = product["sku"], quantity = qty, unit_price = price, discount_percent = disc, vat_rate = rate, line_total = lineTotal, vat_amount = vat });
        }
        recMan.UpdateRecord("sales_order", new EntityRecord { ["id"] = orderId, ["total"] = Round(subtotal), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(subtotal + vatTotal) });

        return new { order_id = orderId, order_number = order["order_number"], confirmed_date = confirmedDate ?? "", planned_delivery_date = plannedDate ?? "", transport_terms = transportTerms ?? "", agent = agent ?? "" };
    }

    /// <summary>Consolidate the delivered-but-not-yet-invoiced lines of several sales orders (same customer) into one invoice.</summary>
    public static object ConsolidateInvoice(JArray orderIds, int dueDays)
    {
        if (orderIds == null || orderIds.Count == 0) throw new InvalidOperationException("At least one order id is required.");
        var recMan = new RecordManager();

        Guid? customer = null;
        Guid firstOrderId = Guid.Empty;
        var orders = new List<EntityRecord>();
        foreach (var oidTok in orderIds)
        {
            var oid = Guid.Parse(oidTok.ToString());
            var o = FindById("sales_order", oid) ?? throw new InvalidOperationException($"Sales order {oid} not found.");
            var ocust = Guid.Parse(o["customer_id"].ToString());
            if (customer == null) { customer = ocust; firstOrderId = oid; }
            else if (ocust != customer.Value) throw new InvalidOperationException("All orders must belong to the same customer.");
            orders.Add(o);
        }

        // Aggregate delivered-not-invoiced lines by product across all orders.
        var agg = new Dictionary<Guid, (decimal qty, decimal lineTotal, decimal vat, Guid vatId)>();
        foreach (var o in orders)
        {
            var oid = Guid.Parse(o["id"].ToString());
            foreach (var ol in Query("SELECT * FROM sales_order_line WHERE sales_order_id = @id", oid))
            {
                var qtyToInvoice = Round(Dec(ol["qty_delivered"]) - Dec(ol["qty_invoiced"]));
                if (qtyToInvoice <= 0m) continue;
                var price = Dec(ol["unit_price"]);
                var disc = Dec(ol["discount_percent"]);
                var lineTotal = Round(qtyToInvoice * price * (1m - disc / 100m));
                var vatId = GuidOrEmpty(ol, "vat_code_id");
                var vat = Round(lineTotal * VatRate(vatId) / 100m);
                var pid = Guid.Parse(ol["product_id"].ToString());
                if (agg.TryGetValue(pid, out var cur))
                    agg[pid] = (cur.qty + qtyToInvoice, cur.lineTotal + lineTotal, cur.vat + vat, cur.vatId);
                else
                    agg[pid] = (qtyToInvoice, lineTotal, vat, vatId);
                recMan.UpdateRecord("sales_order_line", new EntityRecord { ["id"] = Guid.Parse(ol["id"].ToString()), ["qty_invoiced"] = Round(Dec(ol["qty_invoiced"]) + qtyToInvoice) });
            }
        }

        if (agg.Count == 0) throw new InvalidOperationException("Nothing delivered to invoice. Deliver the orders first.");

        var inv = new EntityRecord();
        inv["id"] = Guid.NewGuid();
        inv["invoice_number"] = NextNumber("INV");
        inv["customer_id"] = customer.Value;
        inv["sales_order_id"] = firstOrderId;
        inv["invoice_type"] = "immediate";
        inv["issue_date"] = DateTime.Today;
        inv["due_date"] = DateTime.Today.AddDays(dueDays <= 0 ? 30 : dueDays);
        inv["status"] = "sent";
        inv["notes"] = $"consolidated from {orders.Count} orders";
        var firstOrder = orders[0];
        if (Has(firstOrder, "payment_term_id") && firstOrder["payment_term_id"] != null)
            inv["payment_term_id"] = Guid.Parse(firstOrder["payment_term_id"].ToString());
        var ir = recMan.CreateRecord("invoice", inv);
        if (!ir.Success) throw new InvalidOperationException(Err(ir, "create consolidated invoice"));
        var invoiceId = Id(ir);

        decimal amount = 0m, vatTotal = 0m;
        var invLines = new List<object>();
        foreach (var kv in agg)
        {
            var v = kv.Value;
            var unitPrice = v.qty != 0m ? Round(v.lineTotal / v.qty) : 0m;
            recMan.CreateRecord("invoice_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["invoice_id"] = invoiceId,
                ["product_id"] = kv.Key,
                ["quantity"] = Round(v.qty),
                ["unit_price"] = unitPrice,
                ["discount_percent"] = 0m,
                ["vat_code_id"] = v.vatId == Guid.Empty ? (object)null : v.vatId,
                ["line_total"] = Round(v.lineTotal),
                ["vat_amount"] = Round(v.vat)
            });
            amount += v.lineTotal; vatTotal += v.vat;
            invLines.Add(new { product_id = kv.Key, quantity = Round(v.qty), line_total = Round(v.lineTotal), vat_amount = Round(v.vat) });
        }

        recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = invoiceId, ["amount"] = Round(amount), ["vat_total"] = Round(vatTotal), ["grand_total"] = Round(amount + vatTotal) });
        foreach (var o in orders)
            recMan.UpdateRecord("sales_order", new EntityRecord { ["id"] = Guid.Parse(o["id"].ToString()), ["status"] = "invoiced" });

        return new { invoice_id = invoiceId, invoice_number = inv["invoice_number"], order_count = orders.Count, grand_total = Round(amount + vatTotal) };
    }

    /// <summary>Run a cycle count in a warehouse: record expected vs counted per SKU and apply the variance to stock.</summary>
    public static object RunCycleCount(string warehouseCode, JArray lines)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one count line is required.");
        var wh = ResolveWarehouseByCode(warehouseCode);
        var recMan = new RecordManager();

        var count = new EntityRecord();
        count["id"] = Guid.NewGuid();
        count["warehouse_id"] = wh.id;
        count["count_date"] = DateTime.Today;
        count["status"] = "applied";
        var cr = recMan.CreateRecord("cycle_count", count);
        if (!cr.Success) throw new InvalidOperationException(Err(cr, "create cycle count"));
        var countId = Id(cr);

        var outLines = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var product = FindProductBySku(sku) ?? throw UnknownSku(l);
            var productId = Guid.Parse(product["id"].ToString());
            var counted = l.Value<decimal?>("counted_qty") ?? 0m;
            var expected = StockQty(productId, wh.id);
            var variance = Round(counted - expected);

            recMan.CreateRecord("cycle_count_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["cycle_count_id"] = countId,
                ["product_id"] = productId,
                ["expected_qty"] = Round(expected),
                ["counted_qty"] = Round(counted),
                ["variance"] = variance
            });
            if (variance != 0m)
                AdjustStock(recMan, productId, wh.id, variance, "adjustment", "cycle_count", countId, Dec(product["unit_cost"]), null, null, wh.allowNegative);
            outLines.Add(new { sku, expected = Round(expected), counted = Round(counted), variance });
        }
        return new { count_id = countId, lines = outLines };
    }

    /// <summary>Process a customer return against a delivery: record the return and restock (or scrap) each line.</summary>
    public static object ProcessCustomerReturn(Guid deliveryId, JArray lines, string warehouseCode)
    {
        var delivery = FindById("goods_delivery", deliveryId) ?? throw new InvalidOperationException("Goods delivery not found.");
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one return line is required.");
        var customerId = Guid.Parse(delivery["customer_id"].ToString());
        var wh = ResolveWarehouse(warehouseCode, delivery, "goods_delivery");
        var recMan = new RecordManager();

        var ret = new EntityRecord();
        ret["id"] = Guid.NewGuid();
        ret["customer_id"] = customerId;
        ret["original_delivery_id"] = deliveryId;
        ret["return_date"] = DateTime.Today;
        ret["status"] = "processed";
        var rr = recMan.CreateRecord("sales_return", ret);
        if (!rr.Success) throw new InvalidOperationException(Err(rr, "create sales return"));
        var returnId = Id(rr);

        var restocked = new List<object>();
        var scrapped = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var product = FindProductBySku(sku) ?? throw UnknownSku(l);
            var productId = Guid.Parse(product["id"].ToString());
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var disposition = string.IsNullOrWhiteSpace(l.Value<string>("disposition")) ? "restock" : l.Value<string>("disposition");

            recMan.CreateRecord("sales_return_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["sales_return_id"] = returnId,
                ["product_id"] = productId,
                ["quantity"] = qty,
                ["disposition"] = disposition
            });

            if (disposition == "restock")
            {
                AdjustStock(recMan, productId, wh.id, qty, "return_in", "sales_return", returnId, Dec(product["unit_cost"]), null, null, true);
                restocked.Add(new { sku, quantity = Round(qty) });
            }
            else
            {
                scrapped.Add(new { sku, quantity = Round(qty) });
            }
        }
        return new { return_id = returnId, customer_id = customerId, restocked, scrapped };
    }

    // ══════════════════════════════════════════════
    //  v4 DAILY-USE: rich invoicing, posting/storno, collections, reservations
    // ══════════════════════════════════════════════

    // Convert a EUR amount into the target currency. Exchange-rate rows are from_currency -> EUR,
    // so EUR -> X divides by the rate. EUR (or an unknown currency) returns the amount unchanged.
    private static decimal FromEur(decimal eur, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            return Round(eur);
        var row = Query("SELECT * FROM exchange_rate WHERE from_currency = @c", "c", currency)
            .FirstOrDefault(r => string.Equals(r["to_currency"]?.ToString(), "EUR", StringComparison.OrdinalIgnoreCase));
        if (row != null)
        {
            var rate = Dec(row["rate"]);
            if (rate != 0m) return Round(eur / rate);
        }
        return Round(eur);
    }

    // Resolve a vat_code id by its code string; Guid.Empty when blank or unknown.
    private static Guid VatIdByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Guid.Empty;
        var recs = Query("SELECT id FROM vat_code WHERE code = @c", "c", code);
        return recs.Count > 0 ? Guid.Parse(recs[0]["id"].ToString()) : Guid.Empty;
    }

    private static Guid ParseGuidOrEmpty(string s) => Guid.TryParse(s, out var g) ? g : Guid.Empty;

    // Read a numeric field defensively (0 when the key is absent or null).
    private static decimal DecField(EntityRecord rec, string field) => Has(rec, field) && rec[field] != null ? Dec(rec[field]) : 0m;

    // Read a text field defensively ("" when the key is absent or null).
    private static string StrField(EntityRecord rec, string field) => Has(rec, field) && rec[field] != null ? rec[field].ToString() : "";

    // Append a document_audit row for a post/storno/cancel action.
    private static void Audit(RecordManager recMan, string entityName, Guid recordId, string action, string note)
    {
        recMan.CreateRecord("document_audit", new EntityRecord
        {
            ["id"] = Guid.NewGuid(),
            ["entity_name"] = entityName,
            ["record_id"] = recordId,
            ["action"] = action ?? "",
            ["changed_by"] = "agent",
            ["changed_on"] = DateTime.UtcNow,
            ["note"] = note ?? ""
        });
    }

    private static (Guid id, string code, bool allowNegative) ResolveDefaultWarehouse()
    {
        var def = Query("SELECT * FROM warehouse WHERE is_default = true").FirstOrDefault()
            ?? Query("SELECT * FROM warehouse").FirstOrDefault()
            ?? throw new InvalidOperationException("No warehouse configured.");
        return (Guid.Parse(def["id"].ToString()), def["code"]?.ToString() ?? "", def["allow_negative_stock"] is bool b && b);
    }

    /// <summary>Create a sales invoice from scratch (no order): product lines, a document discount,
    /// accessory/transport lines, a stamp duty (bollo), a withholding (ritenuta d'acconto), optional
    /// reverse charge / tax-document type, an optional currency conversion and an installment plan.
    /// Money is computed in EUR then converted to the target currency. The returned "subtotal" is the
    /// gross product subtotal (before the document discount); accessory/transport amounts are added to
    /// the taxable base separately, so grand_total = (subtotal - document_discount) + accessories
    /// + vat_total + stamp_tax - withholding_amount.</summary>
    public static object CreateSalesInvoiceFromScratch(string customerName, JArray lines, decimal docDiscountPercent,
        JArray accessoryLines, decimal stampTax, decimal withholdingPercent, bool reverseCharge,
        string taxDocumentType, string currency, JArray installments)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one line item is required.");
        var customer = Query("SELECT * FROM customer WHERE name = @n", "n", customerName ?? "")
            .FirstOrDefault() ?? throw new InvalidOperationException($"Customer '{customerName}' not found.");
        if (Has(customer, "blocked") && customer["blocked"] is bool b && b)
            throw new InvalidOperationException("Customer is blocked; cannot create an invoice.");
        var customerId = Guid.Parse(customer["id"].ToString());
        var cur = string.IsNullOrWhiteSpace(currency) ? (customer["currency"]?.ToString() ?? "EUR") : currency;

        // A reverse charge or a non-taxable document type zeroes VAT on every line.
        var tdt = string.IsNullOrWhiteSpace(taxDocumentType) ? "taxable" : taxDocumentType;
        bool vatZero = reverseCharge || tdt == "exempt" || tdt == "out_of_scope" || tdt == "non_taxable";

        var recMan = new RecordManager();

        decimal prodSubtotal = 0m, prodVat = 0m;
        var productLines = new List<EntityRecord>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            EntityRecord product = string.IsNullOrWhiteSpace(sku) ? null : FindProductBySku(sku);
            if (!string.IsNullOrWhiteSpace(sku) && product == null) throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var price = l.Value<decimal?>("unit_price") ?? (product != null ? Dec(product["unit_price"]) : 0m);
            var disc = l.Value<decimal?>("discount_percent") ?? 0m;
            var vatId = VatIdFor(l, product);
            var rate = vatZero ? 0m : VatRate(vatId);
            var lineTotal = Round(qty * price * (1m - disc / 100m));
            var vat = Round(lineTotal * rate / 100m);
            prodSubtotal += lineTotal; prodVat += vat;

            var line = new EntityRecord();
            line["id"] = Guid.NewGuid();
            line["product_id"] = product != null ? Guid.Parse(product["id"].ToString()) : Guid.Empty;
            line["quantity"] = qty;
            line["unit_price"] = Round(FromEur(price, cur));
            line["discount_percent"] = disc;
            if (vatId != Guid.Empty) line["vat_code_id"] = vatId;
            line["line_total"] = Round(FromEur(lineTotal, cur));
            line["vat_amount"] = Round(FromEur(vat, cur));
            line["line_type"] = "product";
            productLines.Add(line);
        }

        decimal docDiscount = Round(prodSubtotal * docDiscountPercent / 100m);
        decimal prodAfterDoc = Round(prodSubtotal - docDiscount);

        decimal accSubtotal = 0m, accVat = 0m;
        var accRecords = new List<EntityRecord>();
        if (accessoryLines != null)
        {
            foreach (var a in accessoryLines)
            {
                var amount = a.Value<decimal?>("amount") ?? 0m;
                var vatId = VatIdByCode(a.Value<string>("vatCode"));
                var rate = vatZero ? 0m : VatRate(vatId);
                var vat = Round(amount * rate / 100m);
                accSubtotal += amount; accVat += vat;
                var rec = new EntityRecord();
                rec["id"] = Guid.NewGuid();
                rec["product_id"] = Guid.Empty;
                rec["quantity"] = 1m;
                rec["unit_price"] = Round(FromEur(amount, cur));
                rec["discount_percent"] = 0m;
                if (vatId != Guid.Empty) rec["vat_code_id"] = vatId;
                rec["line_total"] = Round(FromEur(amount, cur));
                rec["vat_amount"] = Round(FromEur(vat, cur));
                rec["line_type"] = a.Value<string>("type") == "transport" ? "transport" : "accessory";
                accRecords.Add(rec);
            }
        }

        decimal vatTotal = Round(prodVat + accVat);
        decimal withholding = Round(prodAfterDoc * withholdingPercent / 100m);
        decimal stamp = Round(stampTax);
        decimal taxable = Round(prodAfterDoc + accSubtotal);
        decimal grandTotal = Round(taxable + vatTotal + stamp - withholding);

        // Installments: use the supplied schedule, else a single balance due in 30 days.
        var sched = new List<(DateTime due, decimal amt)>();
        if (installments != null && installments.Count > 0)
        {
            foreach (var it in installments)
                sched.Add((ParseDate(it.Value<string>("due_date")) ?? DateTime.Today.AddDays(30),
                          Round(FromEur(it.Value<decimal?>("amount") ?? 0m, cur))));
        }
        else
        {
            sched.Add((DateTime.Today.AddDays(30), Round(FromEur(grandTotal, cur))));
        }
        var firstDue = sched[0].due;

        var inv = new EntityRecord();
        inv["id"] = Guid.NewGuid();
        inv["invoice_number"] = NextNumber("INV");
        inv["customer_id"] = customerId;
        inv["invoice_type"] = "immediate";
        inv["issue_date"] = DateTime.Today;
        inv["due_date"] = firstDue;
        inv["status"] = "sent";
        inv["amount"] = Round(FromEur(taxable, cur));
        inv["vat_total"] = Round(FromEur(vatTotal, cur));
        inv["grand_total"] = Round(FromEur(grandTotal, cur));
        inv["document_discount_percent"] = Round(docDiscountPercent);
        inv["stamp_tax"] = Round(FromEur(stamp, cur));
        inv["withholding_percent"] = Round(withholdingPercent);
        inv["withholding_amount"] = Round(FromEur(withholding, cur));
        inv["reverse_charge"] = reverseCharge;
        inv["tax_document_type"] = tdt;
        inv["customs_amount"] = 0m;
        inv["posted"] = false;
        inv["storned"] = false;
        inv["storno_of"] = Guid.Empty;
        if (Has(customer, "payment_term_id") && customer["payment_term_id"] != null)
            inv["payment_term_id"] = Guid.Parse(customer["payment_term_id"].ToString());
        var ir = recMan.CreateRecord("invoice", inv);
        if (!ir.Success) throw new InvalidOperationException(Err(ir, "create sales invoice"));
        var invoiceId = Id(ir);

        foreach (var pl in productLines)
        {
            pl["invoice_id"] = invoiceId;
            var lr = recMan.CreateRecord("invoice_line", pl);
            if (!lr.Success) throw new InvalidOperationException(Err(lr, "create invoice product line"));
        }
        foreach (var ar in accRecords)
        {
            ar["invoice_id"] = invoiceId;
            var arRes = recMan.CreateRecord("invoice_line", ar);
            if (!arRes.Success) throw new InvalidOperationException(Err(arRes, "create invoice accessory line"));
        }

        var outInst = new List<object>();
        int seq = 1;
        foreach (var s in sched)
        {
            recMan.CreateRecord("invoice_installment", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["invoice_id"] = invoiceId,
                ["due_date"] = s.due,
                ["amount"] = s.amt,
                ["sequence"] = (decimal)seq
            });
            outInst.Add(new { due_date = s.due.ToString("yyyy-MM-dd"), amount = s.amt });
            seq++;
        }

        return new
        {
            invoice_id = invoiceId,
            invoice_number = inv["invoice_number"],
            subtotal = Round(FromEur(prodSubtotal, cur)),
            document_discount = Round(FromEur(docDiscount, cur)),
            vat_total = Round(FromEur(vatTotal, cur)),
            stamp_tax = Round(FromEur(stamp, cur)),
            withholding_amount = Round(FromEur(withholding, cur)),
            grand_total = Round(FromEur(grandTotal, cur)),
            currency = cur,
            installments = outInst
        };
    }

    /// <summary>Create a supplier bill (purchase invoice) from scratch: lines priced at cost, a document
    /// discount and a stamp duty. purchase_invoice has no dedicated discount/stamp fields, so those are
    /// recorded in the notes; the totals are computed and stored.</summary>
    public static object CreatePurchaseInvoiceFromScratch(string supplierName, JArray lines, decimal docDiscountPercent, decimal stampTax, string currency)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one line item is required.");
        var supplier = Query("SELECT * FROM supplier WHERE name = @n", "n", supplierName ?? "")
            .FirstOrDefault() ?? throw new InvalidOperationException($"Supplier '{supplierName}' not found.");
        if (Has(supplier, "blocked") && supplier["blocked"] is bool b && b)
            throw new InvalidOperationException("Supplier is blocked; cannot register a bill.");
        var supplierId = Guid.Parse(supplier["id"].ToString());
        var cur = string.IsNullOrWhiteSpace(currency) ? (supplier["currency"]?.ToString() ?? "EUR") : currency;

        var recMan = new RecordManager();
        var bill = new EntityRecord();
        bill["id"] = Guid.NewGuid();
        bill["bill_number"] = NextNumber("BILL");
        bill["supplier_id"] = supplierId;
        bill["bill_date"] = DateTime.Today;
        bill["due_date"] = DateTime.Today.AddDays(30);
        bill["status"] = "registered";
        bill["triple_match_status"] = "pending";
        var br = recMan.CreateRecord("purchase_invoice", bill);
        if (!br.Success) throw new InvalidOperationException(Err(br, "create purchase invoice"));
        var billId = Id(br);

        decimal subtotal = 0m, vatTotal = 0m;
        foreach (var l in lines)
        {
            var product = FindProductBySku(l.Value<string>("sku")) ?? throw UnknownSku(l);
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            var cost = l.Value<decimal?>("unit_cost") ?? Dec(product["unit_cost"]);
            var vatId = VatIdFor(l, product);
            var lineTotal = Round(qty * cost);
            var vat = Round(lineTotal * VatRate(vatId) / 100m);
            subtotal += lineTotal; vatTotal += vat;
            recMan.CreateRecord("purchase_invoice_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["bill_id"] = billId,
                ["product_id"] = Guid.Parse(product["id"].ToString()),
                ["quantity"] = qty,
                ["unit_cost"] = Round(FromEur(cost, cur)),
                ["vat_code_id"] = vatId == Guid.Empty ? (object)null : vatId,
                ["line_total"] = Round(FromEur(lineTotal, cur)),
                ["vat_amount"] = Round(FromEur(vat, cur))
            });
        }

        decimal docDiscount = Round(subtotal * docDiscountPercent / 100m);
        decimal afterDoc = Round(subtotal - docDiscount);
        decimal stamp = Round(stampTax);
        decimal grandTotal = Round(afterDoc + vatTotal + stamp);

        recMan.UpdateRecord("purchase_invoice", new EntityRecord
        {
            ["id"] = billId,
            ["amount"] = Round(FromEur(afterDoc, cur)),
            ["vat_total"] = Round(FromEur(vatTotal, cur)),
            ["grand_total"] = Round(FromEur(grandTotal, cur)),
            ["notes"] = $"doc discount {Round(docDiscountPercent)}% ({Round(FromEur(docDiscount, cur))}); stamp {Round(FromEur(stamp, cur))}"
        });

        return new { bill_id = billId, bill_number = bill["bill_number"], amount = Round(FromEur(afterDoc, cur)), vat_total = Round(FromEur(vatTotal, cur)), grand_total = Round(FromEur(grandTotal, cur)) };
    }

    /// <summary>Issue a debit note against a supplier (optionally tied to a bill).</summary>
    public static object CreateDebitNote(string supplierName, string billId, decimal amount, string reason)
    {
        var supplier = Query("SELECT * FROM supplier WHERE name = @n", "n", supplierName ?? "")
            .FirstOrDefault() ?? throw new InvalidOperationException($"Supplier '{supplierName}' not found.");
        var recMan = new RecordManager();
        var dn = new EntityRecord();
        dn["id"] = Guid.NewGuid();
        dn["debit_note_number"] = NextNumber("DBN");
        dn["supplier_id"] = Guid.Parse(supplier["id"].ToString());
        dn["bill_id"] = ParseGuidOrEmpty(billId);
        dn["debit_date"] = DateTime.Today;
        dn["amount"] = Round(amount);
        dn["vat_total"] = 0m;
        dn["reason"] = reason ?? "";
        dn["status"] = "issued";
        var r = recMan.CreateRecord("debit_note", dn);
        if (!r.Success) throw new InvalidOperationException(Err(r, "create debit note"));
        return new { debit_note_id = Id(r), debit_note_number = dn["debit_note_number"], amount = Round(amount) };
    }

    /// <summary>Convert a proforma invoice into a real (immediate) invoice.</summary>
    public static object ConvertProformaToInvoice(string proformaInvoiceId)
    {
        var id = ParseGuidOrEmpty(proformaInvoiceId);
        var inv = FindById("invoice", id) ?? throw new InvalidOperationException("Invoice not found.");
        if (inv["invoice_type"]?.ToString() != "proforma")
            throw new InvalidOperationException("The invoice is not a proforma.");
        var recMan = new RecordManager();
        var u = recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = id, ["invoice_type"] = "immediate", ["status"] = "sent" });
        if (!u.Success) throw new InvalidOperationException(Err(u, "convert proforma to invoice"));
        return new { invoice_id = id, invoice_number = inv["invoice_number"], invoice_type = "immediate" };
    }

    /// <summary>Post an invoice (mark it registered in the ledger). Throws if already posted.</summary>
    public static object PostInvoice(string invoiceId)
    {
        var id = ParseGuidOrEmpty(invoiceId);
        var inv = FindById("invoice", id) ?? throw new InvalidOperationException("Invoice not found.");
        if (Has(inv, "posted") && inv["posted"] is bool p && p)
            throw new InvalidOperationException("Invoice already posted.");
        var recMan = new RecordManager();
        var u = recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = id, ["posted"] = true, ["posted_date"] = DateTime.Today });
        if (!u.Success) throw new InvalidOperationException(Err(u, "post invoice"));
        Audit(recMan, "invoice", id, "post", null);
        return new { invoice_id = id, posted = true };
    }

    /// <summary>Storno (reverse) an invoice: create a mirror invoice with negated amounts, mark the
    /// original storned, and record the audit trail.</summary>
    public static object StornoInvoice(string invoiceId)
    {
        var id = ParseGuidOrEmpty(invoiceId);
        var orig = FindById("invoice", id) ?? throw new InvalidOperationException("Invoice not found.");
        var recMan = new RecordManager();

        var rev = new EntityRecord();
        rev["id"] = Guid.NewGuid();
        rev["invoice_number"] = NextNumber("INV");
        rev["customer_id"] = Guid.Parse(orig["customer_id"].ToString());
        rev["invoice_type"] = string.IsNullOrWhiteSpace(StrField(orig, "invoice_type")) ? "immediate" : StrField(orig, "invoice_type");
        rev["issue_date"] = DateTime.Today;
        rev["due_date"] = DateTime.Today;
        rev["status"] = "sent";
        rev["amount"] = Round(-DecField(orig, "amount"));
        rev["vat_total"] = Round(-DecField(orig, "vat_total"));
        rev["grand_total"] = Round(-DecField(orig, "grand_total"));
        rev["document_discount_percent"] = 0m;
        rev["stamp_tax"] = Round(-DecField(orig, "stamp_tax"));
        rev["withholding_percent"] = 0m;
        rev["withholding_amount"] = Round(-DecField(orig, "withholding_amount"));
        rev["reverse_charge"] = Has(orig, "reverse_charge") && orig["reverse_charge"] is bool rc && rc;
        rev["tax_document_type"] = string.IsNullOrWhiteSpace(StrField(orig, "tax_document_type")) ? "taxable" : StrField(orig, "tax_document_type");
        rev["customs_amount"] = 0m;
        rev["posted"] = true;
        rev["posted_date"] = DateTime.Today;
        rev["storned"] = false;
        rev["storno_of"] = id;
        rev["notes"] = $"storno of {orig["invoice_number"]}";
        var rr = recMan.CreateRecord("invoice", rev);
        if (!rr.Success) throw new InvalidOperationException(Err(rr, "create storno invoice"));
        var revId = Id(rr);

        foreach (var ol in Query("SELECT * FROM invoice_line WHERE invoice_id = @id", id))
        {
            var vid = GuidOrEmpty(ol, "vat_code_id");
            recMan.CreateRecord("invoice_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["invoice_id"] = revId,
                ["product_id"] = GuidOrEmpty(ol, "product_id"),
                ["quantity"] = Round(-Dec(ol["quantity"])),
                ["unit_price"] = Dec(ol["unit_price"]),
                ["discount_percent"] = Dec(ol["discount_percent"]),
                ["vat_code_id"] = vid == Guid.Empty ? (object)null : vid,
                ["line_total"] = Round(-Dec(ol["line_total"])),
                ["vat_amount"] = Round(-Dec(ol["vat_amount"])),
                ["line_type"] = string.IsNullOrWhiteSpace(StrField(ol, "line_type")) ? "product" : StrField(ol, "line_type")
            });
        }

        recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = id, ["storned"] = true });
        Audit(recMan, "invoice", id, "storno", $"reversed by {rev["invoice_number"]}");
        return new { storno_invoice_id = revId, original_invoice_id = id };
    }

    /// <summary>Duplicate an invoice and its lines into a new draft (new number, not posted).</summary>
    public static object DuplicateInvoice(string invoiceId)
    {
        var id = ParseGuidOrEmpty(invoiceId);
        var orig = FindById("invoice", id) ?? throw new InvalidOperationException("Invoice not found.");
        var recMan = new RecordManager();
        var dup = new EntityRecord();
        dup["id"] = Guid.NewGuid();
        dup["invoice_number"] = NextNumber("INV");
        dup["customer_id"] = Guid.Parse(orig["customer_id"].ToString());
        dup["invoice_type"] = string.IsNullOrWhiteSpace(StrField(orig, "invoice_type")) ? "immediate" : StrField(orig, "invoice_type");
        dup["issue_date"] = DateTime.Today;
        dup["due_date"] = Has(orig, "due_date") && orig["due_date"] != null ? orig["due_date"] : DateTime.Today.AddDays(30);
        dup["status"] = "draft";
        dup["amount"] = DecField(orig, "amount");
        dup["vat_total"] = DecField(orig, "vat_total");
        dup["grand_total"] = DecField(orig, "grand_total");
        dup["document_discount_percent"] = DecField(orig, "document_discount_percent");
        dup["stamp_tax"] = DecField(orig, "stamp_tax");
        dup["withholding_percent"] = DecField(orig, "withholding_percent");
        dup["withholding_amount"] = DecField(orig, "withholding_amount");
        dup["reverse_charge"] = Has(orig, "reverse_charge") && orig["reverse_charge"] is bool rc && rc;
        dup["tax_document_type"] = string.IsNullOrWhiteSpace(StrField(orig, "tax_document_type")) ? "taxable" : StrField(orig, "tax_document_type");
        dup["customs_amount"] = DecField(orig, "customs_amount");
        dup["posted"] = false;
        dup["storned"] = false;
        dup["storno_of"] = Guid.Empty;
        var dr = recMan.CreateRecord("invoice", dup);
        if (!dr.Success) throw new InvalidOperationException(Err(dr, "duplicate invoice"));
        var dupId = Id(dr);

        foreach (var ol in Query("SELECT * FROM invoice_line WHERE invoice_id = @id", id))
        {
            var vid = GuidOrEmpty(ol, "vat_code_id");
            recMan.CreateRecord("invoice_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["invoice_id"] = dupId,
                ["product_id"] = GuidOrEmpty(ol, "product_id"),
                ["quantity"] = Dec(ol["quantity"]),
                ["unit_price"] = Dec(ol["unit_price"]),
                ["discount_percent"] = Dec(ol["discount_percent"]),
                ["vat_code_id"] = vid == Guid.Empty ? (object)null : vid,
                ["line_total"] = Dec(ol["line_total"]),
                ["vat_amount"] = Dec(ol["vat_amount"]),
                ["line_type"] = string.IsNullOrWhiteSpace(StrField(ol, "line_type")) ? "product" : StrField(ol, "line_type")
            });
        }
        return new { new_invoice_id = dupId, new_invoice_number = dup["invoice_number"] };
    }

    /// <summary>Cancel an invoice. Throws if it has been posted (a posted invoice must be storned).</summary>
    public static object CancelInvoice(string invoiceId)
    {
        var id = ParseGuidOrEmpty(invoiceId);
        var inv = FindById("invoice", id) ?? throw new InvalidOperationException("Invoice not found.");
        if (Has(inv, "posted") && inv["posted"] is bool p && p)
            throw new InvalidOperationException("Invoice is posted; use storno instead of cancel.");
        var recMan = new RecordManager();
        var u = recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = id, ["status"] = "cancelled" });
        if (!u.Success) throw new InvalidOperationException(Err(u, "cancel invoice"));
        Audit(recMan, "invoice", id, "cancel", null);
        return new { invoice_id = id, status = "cancelled" };
    }

    /// <summary>Collect cash across several invoices: allocate the amount over their outstanding balances
    /// in order (partial allowed), then apply an optional allowance (abbuono) that reduces the remaining
    /// balance without cash. One payment row is created per invoice actually paid.
    /// Invoices may be referenced by GUID (invoiceIds) or by human-readable invoice_number
    /// (invoiceNumbers); the two are merged, so an agent can pass the numbers it sees on screen.</summary>
    public static object RecordCollection(JArray invoiceIds, JArray invoiceNumbers, decimal amount, string method, decimal allowance)
    {
        if (amount < 0m) throw new InvalidOperationException("Amount must not be negative.");
        if (allowance < 0m) throw new InvalidOperationException("Allowance must not be negative.");

        var ids = new List<Guid>();
        if (invoiceIds != null)
            foreach (var tok in invoiceIds)
                if (Guid.TryParse(tok.ToString(), out var g)) ids.Add(g);
        if (invoiceNumbers != null)
            foreach (var tok in invoiceNumbers)
            {
                var num = tok.ToString();
                var rec = Query("SELECT id FROM invoice WHERE invoice_number = @n", "n", num).FirstOrDefault()
                    ?? throw new InvalidOperationException($"Invoice number '{num}' not found.");
                ids.Add(Guid.Parse(rec["id"].ToString()));
            }
        if (ids.Count == 0) throw new InvalidOperationException("At least one invoice id or invoice_number is required.");

        var recMan = new RecordManager();
        var methodNorm = string.IsNullOrWhiteSpace(method) ? "bank" : method;

        var rows = new List<(Guid id, decimal payable, decimal paidBefore, decimal cash)>();
        decimal remaining = amount, collected = 0m;
        foreach (var iid in ids)
        {
            var inv = FindById("invoice", iid) ?? throw new InvalidOperationException($"Invoice {iid} not found.");
            var payable = PayableOf(inv);
            var paidBefore = Sum("SELECT amount FROM payment WHERE invoice_id = @id", iid);
            var bal = payable - paidBefore;
            var take = Math.Min(remaining, Math.Max(bal, 0m));
            if (take > 0.0001m)
            {
                recMan.CreateRecord("payment", new EntityRecord
                {
                    ["id"] = Guid.NewGuid(),
                    ["payment_ref"] = NextNumber("PAY"),
                    ["invoice_id"] = iid,
                    ["amount"] = Round(take),
                    ["payment_date"] = DateTime.Today,
                    ["method"] = methodNorm
                });
                collected += take; remaining -= take;
            }
            rows.Add((iid, payable, paidBefore, Round(take)));
        }

        decimal allowRemaining = allowance, allowApplied = 0m;
        var outInvoices = new List<object>();
        foreach (var r in rows)
        {
            var balAfterCash = r.payable - r.paidBefore - r.cash;
            var ab = Math.Min(allowRemaining, Math.Max(balAfterCash, 0m));
            allowRemaining -= ab; allowApplied += ab;
            var newBal = Round(balAfterCash - ab);
            var status = newBal <= 0.005m ? "paid" : (r.cash > 0.0001m ? "partial" : "sent");
            recMan.UpdateRecord("invoice", new EntityRecord { ["id"] = r.id, ["status"] = status });
            outInvoices.Add(new { invoice_id = r.id, paid = r.cash, balance = newBal, status });
        }
        return new { collected = Round(collected), allowance = Round(allowApplied), invoices = outInvoices };
    }

    /// <summary>Customer statement: the customer's open invoices (not paid/cancelled) with balance,
    /// due date, days late and an aging bucket.</summary>
    public static object CustomerStatement(string customerName)
    {
        var customer = Query("SELECT * FROM customer WHERE name = @n", "n", customerName ?? "")
            .FirstOrDefault() ?? throw new InvalidOperationException($"Customer '{customerName}' not found.");
        var cid = Guid.Parse(customer["id"].ToString());
        var today = DateTime.Today;
        var openItems = new List<object>();
        decimal totalOpen = 0m;
        foreach (var inv in Query("SELECT * FROM invoice WHERE customer_id = @id", cid))
        {
            var status = inv["status"]?.ToString();
            if (status == "paid" || status == "cancelled") continue;
            var iid = Guid.Parse(inv["id"].ToString());
            var payable = PayableOf(inv);
            var paid = Sum("SELECT amount FROM payment WHERE invoice_id = @id", iid);
            var bal = Round(payable - paid);
            if (bal <= 0.005m) continue;
            DateTime? due = Has(inv, "due_date") && inv["due_date"] != null ? (DateTime)inv["due_date"] : (DateTime?)null;
            int daysLate = due.HasValue ? Math.Max(0, (today - due.Value).Days) : 0;
            totalOpen += bal;
            openItems.Add(new { invoice_number = inv["invoice_number"], due_date = due?.ToString("yyyy-MM-dd") ?? "", balance = bal, days_late = daysLate, bucket = Bucket(daysLate) });
        }
        return new { customer = customerName, open_items = openItems, total_open = Round(totalOpen) };
    }

    private static string Bucket(int daysLate)
    {
        if (daysLate <= 0) return "current";
        if (daysLate <= 30) return "1-30";
        if (daysLate <= 60) return "31-60";
        if (daysLate <= 90) return "61-90";
        return "90+";
    }

    /// <summary>Reserve stock of a SKU in a warehouse for an order (creates a reservation row).</summary>
    public static object ReserveStock(string sku, string warehouseCode, decimal quantity, string orderId)
    {
        if (quantity <= 0m) throw new InvalidOperationException("Reservation quantity must be positive.");
        var product = FindProductBySku(sku) ?? throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
        var wh = ResolveWarehouseByCode(warehouseCode);
        var recMan = new RecordManager();
        var rec = new EntityRecord
        {
            ["id"] = Guid.NewGuid(),
            ["product_id"] = Guid.Parse(product["id"].ToString()),
            ["warehouse_id"] = wh.id,
            ["sales_order_id"] = ParseGuidOrEmpty(orderId),
            ["quantity"] = Round(quantity),
            ["reserved_on"] = DateTime.UtcNow
        };
        var r = recMan.CreateRecord("reservation", rec);
        if (!r.Success) throw new InvalidOperationException(Err(r, "reserve stock"));
        return new { sku, warehouse = wh.code, reserved = Round(quantity) };
    }

    /// <summary>Release reservations for a SKU in a warehouse, up to the given quantity.</summary>
    public static object ReleaseStock(string sku, string warehouseCode, decimal quantity)
    {
        if (quantity <= 0m) throw new InvalidOperationException("Release quantity must be positive.");
        var product = FindProductBySku(sku) ?? throw new InvalidOperationException($"Unknown product SKU '{sku}'.");
        var wh = ResolveWarehouseByCode(warehouseCode);
        var pid = Guid.Parse(product["id"].ToString());
        var recMan = new RecordManager();
        decimal toRelease = quantity, released = 0m;
        foreach (var res in Query("SELECT * FROM reservation WHERE product_id = @id AND warehouse_id = @w", pid, ("w", wh.id)))
        {
            if (toRelease <= 0.0001m) break;
            var q = Dec(res["quantity"]);
            var rid = Guid.Parse(res["id"].ToString());
            if (q <= toRelease + 0.0001m)
            {
                recMan.DeleteRecord("reservation", rid);
                released += q; toRelease -= q;
            }
            else
            {
                recMan.UpdateRecord("reservation", new EntityRecord { ["id"] = rid, ["quantity"] = Round(q - toRelease) });
                released += toRelease; toRelease = 0m;
            }
        }
        return new { sku, warehouse = wh.code, released = Round(released) };
    }

    /// <summary>Process a supplier return: record the return and its lines and reduce warehouse stock.</summary>
    public static object ProcessSupplierReturn(string supplierName, string receiptId, JArray lines, string warehouseCode)
    {
        if (lines == null || lines.Count == 0) throw new InvalidOperationException("At least one return line is required.");
        var supplier = Query("SELECT * FROM supplier WHERE name = @n", "n", supplierName ?? "")
            .FirstOrDefault() ?? throw new InvalidOperationException($"Supplier '{supplierName}' not found.");
        var supplierId = Guid.Parse(supplier["id"].ToString());
        var wh = string.IsNullOrWhiteSpace(warehouseCode) ? ResolveDefaultWarehouse() : ResolveWarehouseByCode(warehouseCode);
        var recMan = new RecordManager();
        var ret = new EntityRecord
        {
            ["id"] = Guid.NewGuid(),
            ["supplier_id"] = supplierId,
            ["receipt_id"] = ParseGuidOrEmpty(receiptId),
            ["return_date"] = DateTime.Today,
            ["status"] = "processed"
        };
        var rr = recMan.CreateRecord("supplier_return", ret);
        if (!rr.Success) throw new InvalidOperationException(Err(rr, "create supplier return"));
        var returnId = Id(rr);

        var returned = new List<object>();
        foreach (var l in lines)
        {
            var sku = l.Value<string>("sku");
            var product = FindProductBySku(sku) ?? throw UnknownSku(l);
            var pid = Guid.Parse(product["id"].ToString());
            var qty = l.Value<decimal?>("quantity") ?? 0m;
            recMan.CreateRecord("supplier_return_line", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["supplier_return_id"] = returnId,
                ["product_id"] = pid,
                ["quantity"] = Round(qty),
                ["reason"] = l.Value<string>("reason") ?? ""
            });
            AdjustStock(recMan, pid, wh.id, -qty, "return_out", "supplier_return", returnId, Dec(product["unit_cost"]), null, null, wh.allowNegative);
            returned.Add(new { sku, quantity = Round(qty) });
        }
        return new { return_id = returnId, supplier_id = supplierId, returned };
    }

    // ══════════════════════════════════════════════
    //  MANAGER REPORTS (read-only, aggregated in C#)
    // ══════════════════════════════════════════════
    //
    // EQL has no COUNT/GROUP BY/SUM/JOIN, so every report pulls the raw records with a
    // simple SELECT and aggregates with LINQ/loops here. Sales live in the `invoice`
    // entity (customer_id, issue_date); purchases live in the separate `purchase_invoice`
    // entity (supplier_id, bill_date). `invoice_type` does NOT separate sales from purchase.
    // Collected money is not a stored field: it is the sum of `payment` (sales) or
    // `supplier_payment` (purchases) rows, exactly as PayableOf/Sum already compute it.

    /// <summary>Manager report: sales revenue grouped by customer over an optional issue-date
    /// range (inclusive; null bounds are ignored). Storned invoices are excluded. Returns the
    /// per-customer totals sorted by gross total descending.</summary>
    public static object SalesByCustomer(string fromDate, string toDate)
    {
        var from = ParseDate(fromDate);
        var to = ParseDate(toDate);
        var names = LookupNames("customer");

        var agg = new Dictionary<Guid, (decimal net, decimal vat, decimal gross, int count)>();
        foreach (var inv in Query("SELECT * FROM invoice"))
        {
            if (IsStorned(inv)) continue;
            if (!InDateRange(DateOf(inv, "issue_date"), from, to)) continue;
            var cid = GuidOrEmpty(inv, "customer_id");
            var net = DecField(inv, "amount");
            var vat = DecField(inv, "vat_total");
            var gross = DecField(inv, "grand_total");
            if (agg.TryGetValue(cid, out var cur))
                agg[cid] = (cur.net + net, cur.vat + vat, cur.gross + gross, cur.count + 1);
            else
                agg[cid] = (net, vat, gross, 1);
        }

        var rows = agg
            .Select(kv => new
            {
                customer_name = names.TryGetValue(kv.Key, out var n) ? n : kv.Key.ToString(),
                invoice_count = kv.Value.count,
                net_total = Round(kv.Value.net),
                vat_total = Round(kv.Value.vat),
                gross_total = Round(kv.Value.gross)
            })
            .OrderByDescending(r => r.gross_total)
            .ToList();

        return new { from_date = fromDate ?? "", to_date = toDate ?? "", customers = rows };
    }

    /// <summary>Manager report: sales revenue grouped by product over an optional issue-date
    /// range (inclusive). Only product lines of non-storned sales invoices in range are counted
    /// (accessory/transport lines, which carry no product, are skipped). Sorted by net total desc.</summary>
    public static object SalesByProduct(string fromDate, string toDate)
    {
        var from = ParseDate(fromDate);
        var to = ParseDate(toDate);

        // Sales invoice ids in range (not storned).
        var ids = new HashSet<Guid>();
        foreach (var inv in Query("SELECT * FROM invoice"))
        {
            if (IsStorned(inv)) continue;
            if (!InDateRange(DateOf(inv, "issue_date"), from, to)) continue;
            ids.Add(Guid.Parse(inv["id"].ToString()));
        }

        var prods = new Dictionary<Guid, (string sku, string name)>();
        foreach (var p in Query("SELECT id, sku, name FROM product"))
            prods[Guid.Parse(p["id"].ToString())] = (p["sku"]?.ToString() ?? "", p["name"]?.ToString() ?? "");

        // invoice_line has no date; filter in C# to the in-range invoice ids and product lines only.
        var agg = new Dictionary<Guid, (decimal qty, decimal net)>();
        foreach (var ln in Query("SELECT * FROM invoice_line"))
        {
            if (StrField(ln, "line_type") != "product") continue; // skip accessory/transport lines
            if (!ids.Contains(GuidOrEmpty(ln, "invoice_id"))) continue;
            var pid = GuidOrEmpty(ln, "product_id");
            var qty = DecField(ln, "quantity");
            var net = DecField(ln, "line_total");
            if (agg.TryGetValue(pid, out var cur))
                agg[pid] = (cur.qty + qty, cur.net + net);
            else
                agg[pid] = (qty, net);
        }

        var rows = agg
            .Select(kv =>
            {
                var (sku, name) = prods.TryGetValue(kv.Key, out var p) ? p : (kv.Key.ToString(), kv.Key.ToString());
                return new { sku, product_name = name, total_quantity = Round(kv.Value.qty), net_total = Round(kv.Value.net) };
            })
            .OrderByDescending(r => r.net_total)
            .ToList();

        return new { from_date = fromDate ?? "", to_date = toDate ?? "", products = rows };
    }

    /// <summary>Manager report: purchase cost grouped by supplier over an optional bill-date
    /// range (inclusive; null bounds are ignored). Returns the per-supplier totals sorted by
    /// gross total descending.</summary>
    public static object PurchasesBySupplier(string fromDate, string toDate)
    {
        var from = ParseDate(fromDate);
        var to = ParseDate(toDate);
        var names = LookupNames("supplier");

        var agg = new Dictionary<Guid, (decimal net, decimal vat, decimal gross, int count)>();
        foreach (var bill in Query("SELECT * FROM purchase_invoice"))
        {
            if (IsStorned(bill)) continue;
            if (!InDateRange(DateOf(bill, "bill_date"), from, to)) continue;
            var sid = GuidOrEmpty(bill, "supplier_id");
            var net = DecField(bill, "amount");
            var vat = DecField(bill, "vat_total");
            var gross = DecField(bill, "grand_total");
            if (agg.TryGetValue(sid, out var cur))
                agg[sid] = (cur.net + net, cur.vat + vat, cur.gross + gross, cur.count + 1);
            else
                agg[sid] = (net, vat, gross, 1);
        }

        var rows = agg
            .Select(kv => new
            {
                supplier_name = names.TryGetValue(kv.Key, out var n) ? n : kv.Key.ToString(),
                invoice_count = kv.Value.count,
                net_total = Round(kv.Value.net),
                vat_total = Round(kv.Value.vat),
                gross_total = Round(kv.Value.gross)
            })
            .OrderByDescending(r => r.gross_total)
            .ToList();

        return new { from_date = fromDate ?? "", to_date = toDate ?? "", suppliers = rows };
    }

    /// <summary>Manager report: receivables aging (scadenzario). Every open sales invoice
    /// (not storned, not draft/cancelled, with a positive outstanding balance = grand_total minus
    /// collected payments) is bucketed by how far its due_date is from today. Returns the five
    /// buckets in order (current, 1-30, 31-60, 61-90, over-90) and the total outstanding.</summary>
    public static object AgingReceivables()
    {
        var today = DateTime.UtcNow.Date;
        var labels = new[] { "current", "1-30", "31-60", "61-90", "over-90" };
        var amount = new decimal[5];
        var count = new int[5];

        // Collected per invoice = sum of payment.amount rows for that invoice_id, built once.
        var collected = new Dictionary<Guid, decimal>();
        foreach (var p in Query("SELECT * FROM payment"))
        {
            var iid = GuidOrEmpty(p, "invoice_id");
            collected.TryGetValue(iid, out var cur);
            collected[iid] = cur + DecField(p, "amount");
        }

        foreach (var inv in Query("SELECT * FROM invoice"))
        {
            if (IsStorned(inv)) continue;
            var status = inv["status"]?.ToString();
            if (status == "draft" || status == "cancelled") continue;
            var iid = Guid.Parse(inv["id"].ToString());
            collected.TryGetValue(iid, out var paid);
            var bal = Round(DecField(inv, "grand_total") - paid);
            if (bal <= 0.005m) continue;
            var due = DateOf(inv, "due_date");
            int daysLate = due.HasValue ? Math.Max(0, (today - due.Value).Days) : 0;
            int idx = BucketIndex(daysLate);
            amount[idx] += bal;
            count[idx] += 1;
        }

        var buckets = new List<object>();
        decimal total = 0m;
        for (int i = 0; i < 5; i++)
        {
            buckets.Add(new { label = labels[i], amount = Round(amount[i]), invoice_count = count[i] });
            total += amount[i];
        }
        return new { buckets, total_outstanding = Round(total) };
    }

    // ══════════════════════════════════════════════
    //  STOCK LEDGER
    // ══════════════════════════════════════════════

    private static void AdjustStock(RecordManager recMan, Guid productId, Guid warehouseId, decimal delta, string movementType, string refType, Guid refId, decimal unitCost, string lot, DateTime? expiry, bool allowNegative)
    {
        var item = Query("SELECT * FROM stock_item WHERE product_id = @id AND warehouse_id = @w", productId, ("w", warehouseId)).FirstOrDefault();
        decimal current = item != null ? Dec(item["quantity"]) : 0m;
        decimal newQty = Round(current + delta);
        if (newQty < 0m && !allowNegative)
            throw new InvalidOperationException($"Insufficient stock: {Math.Abs(delta)} of product required but only {Round(current)} available in this warehouse (negative stock not allowed).");

        if (item == null)
        {
            recMan.CreateRecord("stock_item", new EntityRecord
            {
                ["id"] = Guid.NewGuid(),
                ["stock_key"] = $"{productId:N}|{warehouseId:N}",
                ["product_id"] = productId,
                ["warehouse_id"] = warehouseId,
                ["quantity"] = newQty,
                ["avg_cost"] = Round(unitCost)
            });
        }
        else
        {
            // Weighted-average cost on receipts.
            var avg = Dec(item["avg_cost"]);
            if (delta > 0m && newQty > 0m)
                avg = Round((avg * current + unitCost * delta) / newQty);
            recMan.UpdateRecord("stock_item", new EntityRecord { ["id"] = Guid.Parse(item["id"].ToString()), ["quantity"] = newQty, ["avg_cost"] = avg });
        }

        recMan.CreateRecord("stock_movement", new EntityRecord
        {
            ["id"] = Guid.NewGuid(),
            ["product_id"] = productId,
            ["warehouse_id"] = warehouseId,
            ["movement_type"] = movementType,
            ["quantity_delta"] = Round(delta),
            ["unit_cost"] = Round(unitCost),
            ["reference_type"] = refType ?? "",
            ["reference_id"] = refId,
            ["lot_number"] = lot ?? "",
            ["expiry_date"] = expiry ?? (object)null,
            ["movement_date"] = DateTime.UtcNow
        });

        // Keep the denormalized product total in sync.
        var prod = FindById("product", productId);
        if (prod != null)
            recMan.UpdateRecord("product", new EntityRecord { ["id"] = productId, ["stock_quantity"] = Round(Dec(prod["stock_quantity"]) + delta) });
    }

    private static decimal StockQty(Guid productId, Guid warehouseId)
    {
        var item = Query("SELECT quantity FROM stock_item WHERE product_id = @id AND warehouse_id = @w", productId, ("w", warehouseId)).FirstOrDefault();
        return item != null ? Dec(item["quantity"]) : 0m;
    }

    // ══════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════

    private static string ComputeDeliveryStatus(Guid orderId)
    {
        var lines = Query("SELECT quantity, qty_delivered FROM sales_order_line WHERE sales_order_id = @id", orderId);
        bool anyDelivered = false, allDelivered = true;
        foreach (var l in lines)
        {
            var q = Dec(l["quantity"]); var d = Dec(l["qty_delivered"]);
            if (d > 0m) anyDelivered = true;
            if (d + 0.0001m < q) allDelivered = false;
        }
        if (allDelivered && anyDelivered) return "delivered";
        if (anyDelivered) return "partially_delivered";
        return "confirmed";
    }

    private static string ComputeReceiveStatus(Guid poId)
    {
        var lines = Query("SELECT quantity, qty_received FROM purchase_order_line WHERE purchase_order_id = @id", poId);
        bool anyReceived = false, allReceived = true;
        foreach (var l in lines)
        {
            var q = Dec(l["quantity"]); var r = Dec(l["qty_received"]);
            if (r > 0m) anyReceived = true;
            if (r + 0.0001m < q) allReceived = false;
        }
        if (allReceived && anyReceived) return "received";
        if (anyReceived) return "partially_received";
        return "ordered";
    }

    private static decimal PayableOf(EntityRecord invoice)
    {
        var gt = Has(invoice, "grand_total") && invoice["grand_total"] != null ? Dec(invoice["grand_total"]) : 0m;
        return gt > 0m ? gt : Dec(invoice["amount"]);
    }

    private static decimal Sum(string eql, Guid id)
    {
        var recs = new EqlCommand(eql, new List<EqlParameter> { new EqlParameter("id", id.ToString()) }).Execute();
        if (recs == null) return 0m;
        decimal sum = 0m;
        foreach (var r in recs) sum += Dec(r["amount"]);
        return sum;
    }

    private static decimal VatRate(Guid vatCodeId)
    {
        if (vatCodeId == Guid.Empty) return 0m;
        var vc = FindById("vat_code", vatCodeId);
        return vc != null ? Dec(vc["rate"]) : 0m;
    }

    private static Guid VatIdFor(JToken line, EntityRecord product)
    {
        var explicitCode = line.Value<string>("vatCode");
        if (!string.IsNullOrWhiteSpace(explicitCode))
        {
            var recs = Query("SELECT id FROM vat_code WHERE code = @c", "c", explicitCode);
            if (recs.Count > 0) return Guid.Parse(recs[0]["id"].ToString());
        }
        return GuidOrEmpty(product, "vat_code_id");
    }

    private static Guid GuidOrEmpty(EntityRecord rec, string field)
    {
        if (!Has(rec, field) || rec[field] == null) return Guid.Empty;
        return Guid.TryParse(rec[field].ToString(), out var g) ? g : Guid.Empty;
    }

    private static bool Has(EntityRecord rec, string key)
    {
        try { var _ = rec[key]; return true; } catch { return false; }
    }

    private static Exception UnknownSku(JToken l) => new InvalidOperationException($"Unknown product SKU '{l.Value<string>("sku")}'.");

    private static (Guid id, string code, bool allowNegative) ResolveWarehouse(string code, EntityRecord doc, string docName)
    {
        if (!string.IsNullOrWhiteSpace(code)) return ResolveWarehouseByCode(code);
        if (Has(doc, "warehouse_id") && doc["warehouse_id"] != null)
        {
            var w = FindById("warehouse", Guid.Parse(doc["warehouse_id"].ToString()));
            if (w != null) return (Guid.Parse(w["id"].ToString()), w["code"]?.ToString() ?? "", w["allow_negative_stock"] is bool b && b);
        }
        // Fall back to the default warehouse.
        var def = Query("SELECT * FROM warehouse WHERE is_default = true").FirstOrDefault()
            ?? Query("SELECT * FROM warehouse").FirstOrDefault()
            ?? throw new InvalidOperationException("No warehouse configured.");
        return (Guid.Parse(def["id"].ToString()), def["code"]?.ToString() ?? "", def["allow_negative_stock"] is bool bb && bb);
    }

    private static (Guid id, string code, bool allowNegative) ResolveWarehouseByCode(string code)
    {
        var w = Query("SELECT * FROM warehouse WHERE code = @c", "c", code).FirstOrDefault()
            ?? throw new InvalidOperationException($"Unknown warehouse '{code}'.");
        return (Guid.Parse(w["id"].ToString()), w["code"]?.ToString() ?? "", w["allow_negative_stock"] is bool b && b);
    }

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

    private static List<EntityRecord> Query(string eql, Guid id)
        => new EqlCommand(eql, new List<EqlParameter> { new EqlParameter("id", id.ToString()) }).Execute()?.ToList() ?? new List<EntityRecord>();

    private static List<EntityRecord> Query(string eql, Guid id, (string, Guid) extra)
        => new EqlCommand(eql, new List<EqlParameter> { new EqlParameter("id", id.ToString()), new EqlParameter(extra.Item1, extra.Item2.ToString()) }).Execute()?.ToList() ?? new List<EntityRecord>();

    private static List<EntityRecord> Query(string eql)
        => new EqlCommand(eql).Execute()?.ToList() ?? new List<EntityRecord>();

    private static List<EntityRecord> Query(string eql, string name, string value)
        => new EqlCommand(eql, new List<EqlParameter> { new EqlParameter(name, value) }).Execute()?.ToList() ?? new List<EntityRecord>();

    private static Guid Id(QueryResponse r) => Guid.Parse(r.Object.Data.First()["id"].ToString());

    private static string NextNumber(string prefix)
        => prefix + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

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

    // Read a date field as a date-only DateTime? (null when absent/unparsable). Handles both a
    // native DateTime value and a string.
    private static DateTime? DateOf(EntityRecord rec, string field)
    {
        if (!Has(rec, field) || rec[field] == null) return null;
        var v = rec[field];
        if (v is DateTime dt) return dt.Date;
        return ParseDate(v.ToString());
    }

    // True when the record's storned flag is set (absent flag counts as not storned, so this is
    // safe on entities that lack the field, e.g. purchase_invoice).
    private static bool IsStorned(EntityRecord rec) => Has(rec, "storned") && rec["storned"] is bool s && s;

    // Inclusive date-range test with open (null) bounds. A null date is only in range when both
    // bounds are null (an undated record cannot be placed inside a bounded window).
    private static bool InDateRange(DateTime? d, DateTime? from, DateTime? to)
    {
        if (!from.HasValue && !to.HasValue) return true;
        if (!d.HasValue) return false;
        if (from.HasValue && d.Value < from.Value) return false;
        if (to.HasValue && d.Value > to.Value) return false;
        return true;
    }

    // Load an entity's id -> name map once, for resolving grouped report keys to display names.
    private static Dictionary<Guid, string> LookupNames(string entity)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var r in Query($"SELECT id, name FROM {entity}"))
            if (Guid.TryParse(r["id"]?.ToString(), out var id))
                map[id] = r["name"]?.ToString() ?? "";
        return map;
    }

    // Aging bucket index for a days-late count, matching the report's five ordered buckets.
    private static int BucketIndex(int daysLate)
    {
        if (daysLate <= 0) return 0;
        if (daysLate <= 30) return 1;
        if (daysLate <= 60) return 2;
        if (daysLate <= 90) return 3;
        return 4;
    }

    private static string Err(QueryResponse r, string what)
    {
        var msg = r?.Message ?? "";
        var first = r?.Errors?.FirstOrDefault()?.Message;
        return $"{what} failed: {msg}{(string.IsNullOrWhiteSpace(first) ? "" : " | " + first)}";
    }
}
