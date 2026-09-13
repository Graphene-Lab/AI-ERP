namespace ErpAgentApi;

using ErpApi;
using ErpCore;
using ErpModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Headless first-run setup. Reads a single <c>bootstrap.json</c> placed next to the ERP's
/// <c>config.json</c> and, on a fresh database, creates the business schema (entities + fields)
/// and the seed records a company needs to start working — with no interactive wizard and no
/// human installer. Runs inside the plugin's <see cref="AgentApiPlugin.Initialize"/> hook, which
/// the host already wraps in a system security scope, so it can create meta and records freely.
///
/// Idempotency: the whole run is gated by the SHA-256 of <c>bootstrap.json</c> stored in the
/// plugin-data table. If the file has not changed since it was last applied, the run is skipped.
/// Individual entity creation is additionally guarded by an existence check, so a partial apply
/// can be safely re-run. To force a clean re-apply, drop and recreate the database.
/// </summary>
public static class Bootstrap
{
    private const string MarkerKey = "bootstrap";

    /// <summary>Apply the bootstrap spec. Returns a short human-readable summary (also logged).</summary>
    public static string Apply(IServiceProvider serviceProvider)
    {
        var path = ResolveBootstrapPath(serviceProvider);
        if (path == null || !File.Exists(path))
            return "bootstrap.json not found — first-run setup skipped.";

        var raw = File.ReadAllText(path);
        var hash = Sha256(raw);

        var plugin = new AgentApiPlugin();
        var marker = ReadMarker(plugin);
        if (marker != null && marker.Value<string>("hash") == hash)
            return $"bootstrap already applied (hash {hash.Substring(0, 8)}) — nothing to do.";

        JObject spec;
        try { spec = JObject.Parse(raw); }
        catch (Exception ex) { return $"bootstrap.json is not valid JSON — {ex.Message}"; }

        var log = new List<string>();
        var entMan = new EntityManager();
        var recMan = new RecordManager(null, true);

        // 1) Business entities + fields.
        if (spec["entities"] is JArray entities)
        {
            foreach (var e in entities)
            {
                var name = e.Value<string>("name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (EntityExists(entMan, name))
                {
                    log.Add($"entity '{name}' already exists — skipped");
                    continue;
                }

                var id = GuidFromName("erp.entity." + name);
                var createResp = entMan.CreateEntity(id, name,
                    e.Value<string>("label") ?? Capitalize(name),
                    e.Value<string>("labelPlural") ?? Capitalize(name) + "s");
                if (!createResp.Success)
                {
                    log.Add($"FAILED to create entity '{name}': {createResp.Message}");
                    continue;
                }

                if (e["fields"] is JArray fields)
                {
                    foreach (var f in fields)
                    {
                        var input = BuildField(f);
                        if (input == null)
                        {
                            log.Add($"FAILED to build field '{name}.{f.Value<string>("name")}' (unknown type '{f.Value<string>("type")}')");
                            continue;
                        }
                        var fr = entMan.CreateField(id, input, transactional: false);
                        if (!fr.Success)
                        {
                            var details = fr.Errors != null && fr.Errors.Count > 0
                                ? " [" + string.Join("; ", fr.Errors.Select(e => $"{e.Key}:{e.Message}")) + "]"
                                : "";
                            log.Add($"FAILED to create field '{name}.{input.Name}' (type '{f.Value<string>("type")}'): {fr.Message}{details}");
                        }
                    }
                }
                log.Add($"created entity '{name}'");
            }
        }

        // 2) Seed records.
        if (spec["seed"] is JObject seed)
        {
            foreach (var prop in seed.Properties())
            {
                var entity = prop.Name;
                if (!(prop.Value is JArray rows)) continue;
                int added = 0, skipped = 0;
                foreach (var row in rows)
                {
                    var rec = new EntityRecord();
                    foreach (var p in ((JObject)row).Properties())
                        rec[p.Name] = Coerce(p.Value);

                    // Resolve foreign-key placeholders: a guid field whose value is "@entity:uniqueValue"
                    // is replaced with the id of the referenced record (looked up by that entity's
                    // unique field). Lets the seed express relationships without hardcoding GUIDs.
                    ResolveGuidRefs(entity, rec, spec);

                    // Idempotency by the entity's unique field, when present.
                    var uniqueField = UniqueFieldOf(spec, entity);
                    if (uniqueField != null && HasField(rec, uniqueField) && rec[uniqueField] != null
                        && RecordExists(entity, uniqueField, rec[uniqueField]))
                    { skipped++; continue; }

                    // RecordManager does not auto-generate the record id on this path — the ERP's
                    // own seed sets it explicitly. Assign a deterministic id so re-runs stay stable.
                    if (!HasField(rec, "id") || rec["id"] == null)
                    {
                        var seedKey = uniqueField != null && HasField(rec, uniqueField) && rec[uniqueField] != null
                            ? $"{entity}.{rec[uniqueField]}"
                            : $"{entity}.{added + skipped}";
                        rec["id"] = GuidFromName("seed." + seedKey);
                    }

                    var r = recMan.CreateRecord(entity, rec);
                    if (r.Success) added++;
                    else log.Add($"FAILED seed {entity}: {r.Message} {r.Errors?.FirstOrDefault()?.Message}");
                }
                log.Add($"seeded {entity}: {added} added, {skipped} already present");
            }
        }

        var summary = string.Join("; ", log);
        bool anyFailed = log.Any(l => l.StartsWith("FAILED"));
        if (!anyFailed)
            SaveMarker(plugin, hash);
        else
            Console.WriteLine("[Bootstrap] completed with failures — marker NOT saved, will retry on next start.");
        Console.WriteLine("[Bootstrap] " + summary);
        return summary;
    }

    /// <summary>Report the current setup state so an agent can observe whether the ERP is
    /// provisioned and whether re-applying <c>bootstrap.json</c> would change anything.
    /// Read-only — creates nothing.</summary>
    public static object Status(IServiceProvider serviceProvider)
    {
        var plugin = new AgentApiPlugin();
        var marker = ReadMarker(plugin);

        var path = ResolveBootstrapPath(serviceProvider);
        string currentHash = null;
        JObject spec = null;
        if (path != null && File.Exists(path))
        {
            var raw = File.ReadAllText(path);
            currentHash = Sha256(raw);
            try { spec = JObject.Parse(raw); } catch { spec = null; }
        }

        var appliedHash = marker?.Value<string>("hash");
        bool installed = marker != null;
        bool pending = currentHash != null && appliedHash != currentHash;

        int entityCount = 0;
        try
        {
            var resp = new EntityManager().ReadEntities();
            if (resp != null && resp.Success && resp.Object != null)
                entityCount = resp.Object.Count(e => !e.System);
        }
        catch { }

        var seedCounts = new JObject();
        if (spec?["seed"] is JObject seed)
        {
            foreach (var prop in seed.Properties())
            {
                long n = 0;
                try
                {
                    var recs = new ErpEql.EqlCommand($"SELECT id FROM {prop.Name}").Execute();
                    n = recs?.TotalCount ?? 0;
                }
                catch { }
                seedCounts[prop.Name] = n;
            }
        }

        return new
        {
            installed,
            bootstrap_present = currentHash != null,
            applied_hash = appliedHash != null ? appliedHash.Substring(0, 8) : null,
            current_hash = currentHash != null ? currentHash.Substring(0, 8) : null,
            pending,
            applied_on = marker?.Value<string>("appliedOn"),
            entity_count = entityCount,
            seed_counts = seedCounts
        };
    }

    /// <summary>Re-apply <c>bootstrap.json</c> on demand (idempotent). Runs inside a system
    /// security scope, mirroring the startup <c>Initialize</c> hook, so meta creation is
    /// permitted. If the file is unchanged since it was last applied this is a no-op that
    /// returns "already applied".</summary>
    public static string Reprovision(IServiceProvider serviceProvider)
    {
        using (SecurityContext.OpenSystemScope())
        {
            return Apply(serviceProvider);
        }
    }

    // ──────────────────────────────────────────────
    //  Field construction
    // ──────────────────────────────────────────────

    private static InputField BuildField(JToken f)
    {
        var name = f.Value<string>("name");
        var label = f.Value<string>("label") ?? Capitalize(name);
        var type = (f.Value<string>("type") ?? "text").ToLowerInvariant();
        var required = f.Value<bool?>("required") ?? false;
        var unique = f.Value<bool?>("unique") ?? false;

        InputField field = type switch
        {
            "text" => new InputTextField { MaxLength = f.Value<int?>("maxLength") ?? 200, DefaultValue = "" },
            "multiline" => new InputMultiLineTextField { MaxLength = f.Value<int?>("maxLength") ?? 4000, DefaultValue = "" },
            "email" => new InputEmailField { DefaultValue = "" },
            "phone" => new InputPhoneField { DefaultValue = "" },
            "url" => new InputUrlField { DefaultValue = "" },
            "number" => new InputNumberField
            {
                DecimalPlaces = (byte)(f.Value<int?>("decimalPlaces") ?? 2),
                DefaultValue = f.Value<decimal?>("defaultValue") ?? 0m,
                MinValue = f.Value<decimal?>("minValue"),
                MaxValue = f.Value<decimal?>("maxValue")
            },
            "percent" => new InputPercentField
            {
                DecimalPlaces = (byte)(f.Value<int?>("decimalPlaces") ?? 2),
                DefaultValue = f.Value<decimal?>("defaultValue") ?? 0m
            },
            "bool" => new InputCheckboxField { DefaultValue = f.Value<bool?>("defaultValue") ?? false },
            "date" => new InputDateField
            {
                Format = f.Value<string>("format") ?? "dd MMM yyyy",
                UseCurrentTimeAsDefaultValue = required
            },
            "datetime" => new InputDateTimeField
            {
                Format = f.Value<string>("format") ?? "dd MMM yyyy HH:mm:ss",
                UseCurrentTimeAsDefaultValue = required
            },
            "guid" => new InputGuidField { GenerateNewId = true },
            "select" => new InputSelectField
            {
                Options = OptionsOf(f),
                DefaultValue = f.Value<string>("defaultValue") ?? OptionsOf(f).FirstOrDefault()?.Value ?? ""
            },
            _ => null
        };
        if (field == null) return null;

        field.Name = name;
        field.Label = label;
        field.Required = required;
        field.Unique = unique;
        field.Searchable = f.Value<bool?>("searchable") ?? true;
        field.Auditable = false;
        field.System = false;
        field.Description = f.Value<string>("description") ?? "";
        field.HelpText = f.Value<string>("helpText") ?? "";
        return field;
    }

    private static List<SelectOption> OptionsOf(JToken f)
    {
        var list = new List<SelectOption>();
        if (f["options"] is JArray opts)
        {
            foreach (var o in opts)
            {
                if (o.Type == JTokenType.String)
                    list.Add(new SelectOption(o.Value<string>(), Capitalize(o.Value<string>())));
                else
                    list.Add(new SelectOption(o.Value<string>("value"), o.Value<string>("label")));
            }
        }
        return list;
    }

    // ──────────────────────────────────────────────
    //  Value coercion (JSON token -> CLR value for EntityRecord)
    // ──────────────────────────────────────────────

    private static object Coerce(JToken v)
    {
        switch (v.Type)
        {
            case JTokenType.Integer: return v.Value<long>();
            case JTokenType.Float: return v.Value<decimal>();
            case JTokenType.Boolean: return v.Value<bool>();
            case JTokenType.Null: return null;
            case JTokenType.Date: return v.Value<DateTime>();
            default:
                var s = v.Value<string>();
                if (s != null && Guid.TryParse(s, out var g)) return g;
                return s;
        }
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private static bool EntityExists(EntityManager entMan, string name)
    {
        try { return entMan.ReadEntity(name)?.Object != null; }
        catch { return false; }
    }

    private static bool HasField(EntityRecord rec, string key)
    {
        try { var _ = rec[key]; return true; }
        catch { return false; }
    }

    private static string UniqueFieldOf(JObject spec, string entity)
    {
        if (spec["entities"] is JArray entities)
        {
            var e = entities.FirstOrDefault(x => x.Value<string>("name") == entity);
            if (e?["fields"] is JArray fields)
                return fields.FirstOrDefault(f => f.Value<bool?>("unique") ?? false)?.Value<string>("name");
        }
        return null;
    }

    private static bool RecordExists(string entity, string field, object value)
    {
        try
        {
            var p = new List<ErpEql.EqlParameter> { new ErpEql.EqlParameter("v", value?.ToString()) };
            var recs = new ErpEql.EqlCommand($"SELECT id FROM {entity} WHERE {field} = @v", p).Execute();
            return recs != null && recs.TotalCount > 0;
        }
        catch { return false; }
    }

    // For every guid-typed field of the entity whose seed value is a "@refEntity:uniqueValue"
    // placeholder, replace it with the referenced record's id (looked up by the referenced
    // entity's unique field). Non-placeholder guid values are left untouched.
    private static void ResolveGuidRefs(string entity, EntityRecord rec, JObject spec)
    {
        var guidFields = GuidFieldsOf(spec, entity);
        foreach (var fieldName in guidFields)
        {
            if (!HasField(rec, fieldName)) continue;
            var raw = rec[fieldName] as string;
            if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("@")) continue;

            var body = raw.Substring(1);
            var colon = body.IndexOf(':');
            if (colon <= 0) continue;
            var refEntity = body.Substring(0, colon);
            var refValue = body.Substring(colon + 1);
            var refUnique = UniqueFieldOf(spec, refEntity);
            if (refUnique == null) continue;

            try
            {
                var p = new List<ErpEql.EqlParameter> { new ErpEql.EqlParameter("v", refValue) };
                var found = new ErpEql.EqlCommand($"SELECT id FROM {refEntity} WHERE {refUnique} = @v", p).Execute();
                if (found != null && found.TotalCount > 0)
                {
                    var idToken = found.First()["id"];
                    rec[fieldName] = idToken is Guid g ? g : Guid.Parse(idToken.ToString());
                }
                else
                {
                    Console.WriteLine($"[Bootstrap] unresolved FK {entity}.{fieldName} = {raw} (no {refEntity} with {refUnique}='{refValue}')");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bootstrap] FK resolve error {entity}.{fieldName}={raw}: {ex.Message}");
            }
        }
    }

    private static List<string> GuidFieldsOf(JObject spec, string entity)
    {
        var list = new List<string>();
        if (spec["entities"] is JArray entities)
        {
            var e = entities.FirstOrDefault(x => x.Value<string>("name") == entity);
            if (e?["fields"] is JArray fields)
                foreach (var f in fields)
                    if ((f.Value<string>("type") ?? "").ToLowerInvariant() == "guid")
                        list.Add(f.Value<string>("name"));
        }
        return list;
    }

    private static string ResolveBootstrapPath(IServiceProvider sp)
    {
        var env = sp?.GetService<IWebHostEnvironment>();
        var candidates = new List<string>();
        if (env != null) candidates.Add(Path.Combine(env.ContentRootPath, "bootstrap.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "bootstrap.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "bootstrap.json"));
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return null;
    }

    private static string Sha256(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private static Guid GuidFromName(string name)
    {
        // Deterministic UUIDv5-style (SHA-1 based) so entity ids are stable across runs.
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(name));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string Capitalize(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private static JToken ReadMarker(AgentApiPlugin plugin)
    {
        try
        {
            var data = plugin.GetPluginData();
            if (string.IsNullOrWhiteSpace(data)) return null;
            var root = JObject.Parse(data);
            return root[MarkerKey];
        }
        catch { return null; }
    }

    private static void SaveMarker(AgentApiPlugin plugin, string hash)
    {
        try
        {
            var root = new JObject();
            var existing = plugin.GetPluginData();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                try { root = JObject.Parse(existing); } catch { root = new JObject(); }
            }
            root[MarkerKey] = new JObject { ["hash"] = hash, ["appliedOn"] = DateTime.UtcNow.ToString("o") };
            plugin.SavePluginData(root.ToString(Formatting.None));
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Bootstrap] could not save marker: " + ex.Message);
        }
    }
}
