using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services
{
    public interface IPolicyInventoryService
    {
        Task<(bool Success, string Message, int Policies, int Rules, int Exceptions)> ImportFileAsync(IFormFile file);
        Task<byte[]> ExportJsonAsync();
        Task<byte[]> ExportExcelAsync();

        // Single CRUD
        Task<(bool Success, string Message, PIPolicy Data)> CreatePolicyAsync(PIPolicy policy);
        Task<(bool Success, string Message, PIPolicy Data)> UpdatePolicyAsync(int id, PIPolicy policy);
        Task<(bool Success, string Message)> DeletePolicyAsync(int id);

        Task<(bool Success, string Message, PIRule Data)> CreateRuleAsync(PIRule rule);
        Task<(bool Success, string Message, PIRule Data)> UpdateRuleAsync(int id, PIRule rule);
        Task<(bool Success, string Message)> DeleteRuleAsync(int id);

        Task<(bool Success, string Message, PIException Data)> CreateExceptionAsync(PIException exc);
        Task<(bool Success, string Message, PIException Data)> UpdateExceptionAsync(int id, PIException exc);
        Task<(bool Success, string Message)> DeleteExceptionAsync(int id);
    }

    public class PolicyInventoryService : IPolicyInventoryService
    {
        private readonly AnalyzerDbContext _context;

        public PolicyInventoryService(AnalyzerDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message, int Policies, int Rules, int Exceptions)> ImportFileAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext == ".json")
            {
                return await ImportJsonAsync(file);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                return await ImportExcelAsync(file);
            }
            return (false, "Desteklenmeyen dosya formatı.", 0, 0, 0);
        }

        private async Task<(bool, string, int, int, int)> ImportJsonAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var document = await System.Text.Json.JsonDocument.ParseAsync(stream);

            var importedPolicies = new List<PIPolicy>();

            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                // Legacy format: direct array
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
                };
                importedPolicies = System.Text.Json.JsonSerializer.Deserialize<List<PIPolicy>>(document.RootElement.GetRawText(), options);
            }
            else if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("policy", out var policyElement))
                {
                    var policyName = policyElement.TryGetProperty("policy_name", out var pn) ? pn.GetString() : null;
                    if (!string.IsNullOrEmpty(policyName))
                    {
                        var policy = new PIPolicy { PolicyName = policyName };

                        if (policyElement.TryGetProperty("rules", out var rulesElement) && rulesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var rElement in rulesElement.EnumerateArray())
                            {
                                var rule = new PIRule
                                {
                                    RuleName = rElement.TryGetProperty("rule_name", out var rn) ? rn.GetString() : null,
                                    PartsCountType = rElement.TryGetProperty("parts_count_type", out var pct) ? pct.GetString() : null,
                                    ConditionRelationType = rElement.TryGetProperty("condition_relation_type", out var crt) ? crt.GetString() : null
                                };

                                // Rule Classifiers — FIX: now reads threshold_value_from
                                if (rElement.TryGetProperty("classifiers", out var classElement) && classElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var cElement in classElement.EnumerateArray())
                                    {
                                        int? tvf = null;
                                        if (cElement.TryGetProperty("threshold_value_from", out var tvfEl) && tvfEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                            tvf = tvfEl.GetInt32();
                                        rule.Classifiers.Add(new PIRuleClassifier
                                        {
                                            ClassifierName = cElement.TryGetProperty("classifier_name", out var cn) ? cn.GetString() : null,
                                            ThresholdType = cElement.TryGetProperty("threshold_type", out var tt) ? tt.GetString() : null,
                                            ThresholdValueFrom = tvf,
                                            ThresholdCalculateType = cElement.TryGetProperty("threshold_calculate_type", out var tct) ? tct.GetString() : null
                                        });
                                    }
                                }

                                // Exceptions
                                if (rElement.TryGetProperty("exception_rules", out var excObj) && excObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (excObj.TryGetProperty("exception_rules", out var excArr) && excArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var eElement in excArr.EnumerateArray())
                                        {
                                            var exc = new PIException
                                            {
                                                ExceptionRuleName = eElement.TryGetProperty("exception_rule_name", out var ern) ? ern.GetString() : null,
                                                Enabled = eElement.TryGetProperty("enabled", out var ee) ? ee.GetString() : null,
                                                Description = eElement.TryGetProperty("description", out var ed) ? ed.GetString() : null,
                                                ConditionEnabled = eElement.TryGetProperty("condition_enabled", out var ce) ? ce.GetString() : null,
                                                SourceEnabled = eElement.TryGetProperty("source_enabled", out var se) ? se.GetString() : null,
                                                DestinationEnabled = eElement.TryGetProperty("destination_enabled", out var de) ? de.GetString() : null,
                                                PartsCountType = eElement.TryGetProperty("parts_count_type", out var epct) ? epct.GetString() : null,
                                                ConditionRelationType = eElement.TryGetProperty("condition_relation_type", out var ecrt) ? ecrt.GetString() : null
                                            };

                                            // FIX: Exception Classifiers (was never parsed before)
                                            if (eElement.TryGetProperty("classifiers", out var excClassArr) && excClassArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                            {
                                                int autoPos = 0;
                                                foreach (var ecEl in excClassArr.EnumerateArray())
                                                {
                                                    autoPos++;
                                                    int? epos = null, etvf = null;
                                                    if (ecEl.TryGetProperty("position", out var eposEl) && eposEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                                        epos = eposEl.GetInt32();
                                                    if (ecEl.TryGetProperty("threshold_value_from", out var etvfEl) && etvfEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                                        etvf = etvfEl.GetInt32();
                                                    exc.Classifiers.Add(new PIExceptionClassifier
                                                    {
                                                        ClassifierName = ecEl.TryGetProperty("classifier_name", out var ecn) ? ecn.GetString() : null,
                                                        Position = epos ?? autoPos,
                                                        ThresholdType = ecEl.TryGetProperty("threshold_type", out var ett) ? ett.GetString() : null,
                                                        ThresholdValueFrom = etvf,
                                                        ThresholdCalculateType = ecEl.TryGetProperty("threshold_calculate_type", out var etct) ? etct.GetString() : null,
                                                        AnalyzedSpecificFields = ecEl.TryGetProperty("analyzed_specific_fields", out var easf) ? 
                                                            (easf.ValueKind == System.Text.Json.JsonValueKind.Array ? string.Join(", ", easf.EnumerateArray().Select(x => x.GetString())) : easf.GetString()) : null
                                                    });
                                                }
                                            }

                                            // Exception Severity Action
                                            if (eElement.TryGetProperty("severity_action", out var sevObj) && sevObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                if (sevObj.TryGetProperty("classifier_details", out var cdArr) && cdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var cdElement in cdArr.EnumerateArray())
                                                    {
                                                        int? nom = null;
                                                        if (cdElement.TryGetProperty("number_of_matches", out var nomEl) && nomEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                                            nom = nomEl.GetInt32();
                                                        exc.SeverityActions.Add(new PIExceptionSeverityAction
                                                        {
                                                            Selected = cdElement.TryGetProperty("selected", out var sel) ? sel.GetString() : null,
                                                            NumberOfMatches = nom,
                                                            SeverityType = cdElement.TryGetProperty("severity_type", out var st) ? st.GetString() : null,
                                                            DupSeverityType = cdElement.TryGetProperty("dup_severity_type", out var dst) ? dst.GetString() : null,
                                                            ActionPlan = cdElement.TryGetProperty("action_plan", out var ap) ? ap.GetString() : null
                                                        });
                                                    }
                                                }
                                            }

                                            // FIX: Exception Source — field is "type" not "resource_type"
                                            if (eElement.TryGetProperty("rule_source", out var srcObj) && srcObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                if (srcObj.TryGetProperty("resources", out var ndArr) && ndArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var ndElement in ndArr.EnumerateArray())
                                                    {
                                                        exc.Sources.Add(new PIExceptionSource
                                                        {
                                                            ResourceName = ndElement.TryGetProperty("resource_name", out var srn) ? srn.GetString() : null,
                                                            ResourceType = ndElement.TryGetProperty("type", out var srt) ? srt.GetString() : null,
                                                            Include = ndElement.TryGetProperty("include", out var inc) ? inc.GetString() : null
                                                        });
                                                    }
                                                }
                                            }

                                            // FIX: Exception Destination — now saves channel resources too
                                            if (eElement.TryGetProperty("rule_destination", out var destObj) && destObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                var emailDir = "";
                                                if (destObj.TryGetProperty("email_monitor_directions", out var emdArr) && emdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    var dirs = new List<string>();
                                                    foreach (var d in emdArr.EnumerateArray()) dirs.Add(d.GetString());
                                                    emailDir = string.Join(",", dirs);
                                                }

                                                if (destObj.TryGetProperty("channels", out var chanArr) && chanArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var chanElement in chanArr.EnumerateArray())
                                                    {
                                                        var excDest = new PIExceptionDestination
                                                        {
                                                            EmailMonitorDirections = emailDir,
                                                            ChannelType = chanElement.TryGetProperty("channel_type", out var ct) ? ct.GetString() : null,
                                                            ChannelEnabled = chanElement.TryGetProperty("enabled", out var cen) ? cen.GetString() : null
                                                        };
                                                        // FIX: save channel resources within each channel
                                                        if (chanElement.TryGetProperty("resources", out var chanResArr) && chanResArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                        {
                                                            foreach (var crEl in chanResArr.EnumerateArray())
                                                            {
                                                                excDest.ChannelResources.Add(new PIExceptionChannelResource
                                                                {
                                                                    ResourceName = crEl.TryGetProperty("resource_name", out var crn) ? crn.GetString() : null,
                                                                    ResourceType = crEl.TryGetProperty("type", out var crt2) ? crt2.GetString() : null,
                                                                    Include = crEl.TryGetProperty("include", out var crinc) ? crinc.GetString() : null
                                                                });
                                                            }
                                                        }
                                                        exc.Destinations.Add(excDest);
                                                    }
                                                }
                                            }

                                            rule.Exceptions.Add(exc);
                                        }
                                    }
                                }

                                policy.Rules.Add(rule);
                            }
                        }
                        importedPolicies.Add(policy);
                    }
                }

                // Root-level severity_action — Rule Severity Actions
                if (document.RootElement.TryGetProperty("severity_action", out var rootSevElement) && rootSevElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (rootSevElement.TryGetProperty("rules", out var sevRules) && sevRules.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var sevRule in sevRules.EnumerateArray())
                        {
                            var rName = sevRule.TryGetProperty("rule_name", out var rn) ? rn.GetString() : null;
                            var targetRule = importedPolicies.SelectMany(p => p.Rules).FirstOrDefault(r => r.RuleName == rName);
                            if (targetRule != null)
                            {
                                // FIX: "type" alanı (Excel'de C38 / "Value.rules.type") JSON'da rule seviyesinde
                                // (classifier_details'in dışında, kardeşi) geliyor ama önceden hiç okunmuyordu.
                                // Bu, veritabanındaki 'type' kolonunun NULL kalmasına ve liste sorgusunun
                                // InvalidCastException ile çökmesine sebep oluyordu.
                                var sevType = sevRule.TryGetProperty("type", out var styp) ? styp.GetString() : null;
                                var maxMatches = sevRule.TryGetProperty("max_matches", out var mm) ? mm.GetString() : null;
                                if (sevRule.TryGetProperty("classifier_details", out var cdArr) && cdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var cdElement in cdArr.EnumerateArray())
                                    {
                                        int? nom = null;
                                        if (cdElement.TryGetProperty("number_of_matches", out var nomEl) && nomEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                            nom = nomEl.GetInt32();
                                        targetRule.SeverityActions.Add(new PIRuleSeverityAction
                                        {
                                            Type = sevType ?? "",
                                            MaxMatches = maxMatches,
                                            Selected = cdElement.TryGetProperty("selected", out var sel) ? sel.GetString() : null,
                                            NumberOfMatches = nom,
                                            SeverityType = cdElement.TryGetProperty("severity_type", out var st) ? st.GetString() : null,
                                            DupSeverityType = cdElement.TryGetProperty("dup_severity_type", out var dst) ? dst.GetString() : null,
                                            ActionPlan = cdElement.TryGetProperty("action_plan", out var ap) ? ap.GetString() : null
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                // Root-level source_destination — Rule Sources and Destinations
                if (document.RootElement.TryGetProperty("source_destination", out var rootSrcDestElement) && rootSrcDestElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (rootSrcDestElement.TryGetProperty("rules", out var sdRules) && sdRules.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var sdRule in sdRules.EnumerateArray())
                        {
                            var rName = sdRule.TryGetProperty("rule_name", out var rn) ? rn.GetString() : null;
                            var targetRule = importedPolicies.SelectMany(p => p.Rules).FirstOrDefault(r => r.RuleName == rName);
                            if (targetRule != null)
                            {
                                // FIX: Rule Source — field is "type" not "resource_type"
                                if (sdRule.TryGetProperty("rule_source", out var rSrcObj) && rSrcObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (rSrcObj.TryGetProperty("resources", out var resArr) && resArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var resElement in resArr.EnumerateArray())
                                        {
                                            targetRule.Sources.Add(new PIRuleSource
                                            {
                                                ResourceName = resElement.TryGetProperty("resource_name", out var rsName) ? rsName.GetString() : null,
                                                ResourceType = resElement.TryGetProperty("type", out var rsType) ? rsType.GetString() : null,
                                                Include = resElement.TryGetProperty("include", out var inc) ? inc.GetString() : null
                                            });
                                        }
                                    }
                                }

                                // FIX: Rule Destination — now saves channel resources too
                                if (sdRule.TryGetProperty("rule_destination", out var rDestObj) && rDestObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    var emailDir = "";
                                    if (rDestObj.TryGetProperty("email_monitor_directions", out var emdArr) && emdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        var dirs = new List<string>();
                                        foreach (var d in emdArr.EnumerateArray()) dirs.Add(d.GetString());
                                        emailDir = string.Join(",", dirs);
                                    }

                                    if (rDestObj.TryGetProperty("channels", out var chanArr) && chanArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var chanElement in chanArr.EnumerateArray())
                                        {
                                            var ruleDest = new PIRuleDestination
                                            {
                                                EmailMonitorDirections = emailDir,
                                                ChannelType = chanElement.TryGetProperty("channel_type", out var ct) ? ct.GetString() : null,
                                                ChannelEnabled = chanElement.TryGetProperty("enabled", out var cen) ? cen.GetString() : null
                                            };
                                            // FIX: save channel resources within each rule destination channel
                                            if (chanElement.TryGetProperty("resources", out var chanResArr) && chanResArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                            {
                                                foreach (var crEl in chanResArr.EnumerateArray())
                                                {
                                                    ruleDest.ChannelResources.Add(new PIRuleChannelResource
                                                    {
                                                        ResourceName = crEl.TryGetProperty("resource_name", out var crn) ? crn.GetString() : null,
                                                        ResourceType = crEl.TryGetProperty("type", out var crt2) ? crt2.GetString() : null,
                                                        Include = crEl.TryGetProperty("include", out var crinc) ? crinc.GetString() : null
                                                    });
                                                }
                                            }
                                            targetRule.Destinations.Add(ruleDest);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (importedPolicies == null || !importedPolicies.Any())
            {
                return (false, "Geçersiz veya boş JSON dosyası.", 0, 0, 0);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var existingPolicies = await _context.PIPolicies.ToListAsync();
                    _context.PIPolicies.RemoveRange(existingPolicies);
                    await _context.SaveChangesAsync();

                    // Id'leri sıfırla ki yeni insert olarak algılansın
                    foreach (var p in importedPolicies)
                    {
                        p.Id = 0;
                        foreach (var r in p.Rules)
                        {
                            r.Id = 0;
                            r.PolicyId = 0;
                            foreach (var c in r.Classifiers) { c.Id = 0; c.RuleId = 0; }
                            foreach (var s in r.SeverityActions) { s.Id = 0; s.RuleId = 0; }
                            foreach (var src in r.Sources) { src.Id = 0; src.RuleId = 0; }
                            foreach (var d in r.Destinations)
                            {
                                d.Id = 0; d.RuleId = 0;
                                foreach (var cr in d.ChannelResources) { cr.Id = 0; cr.DestinationId = 0; }
                            }
                            foreach (var e in r.Exceptions)
                            {
                                e.Id = 0; e.RuleId = 0;
                                foreach (var ec in e.Classifiers) { ec.Id = 0; ec.ExceptionId = 0; }
                                foreach (var es in e.SeverityActions) { es.Id = 0; es.ExceptionId = 0; }
                                foreach (var esrc in e.Sources) { esrc.Id = 0; esrc.ExceptionId = 0; }
                                foreach (var ed in e.Destinations)
                                {
                                    ed.Id = 0; ed.ExceptionId = 0;
                                    foreach (var ecr in ed.ChannelResources) { ecr.Id = 0; ecr.DestinationId = 0; }
                                }
                            }
                        }
                    }

                    _context.PIPolicies.AddRange(importedPolicies);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    int polCount = importedPolicies.Count;
                    int ruleCount = importedPolicies.SelectMany(p => p.Rules).Count();
                    int excCount = importedPolicies.SelectMany(p => p.Rules).SelectMany(r => r.Exceptions).Count();

                    return (true, "JSON içe aktarma başarılı.", polCount, ruleCount, excCount);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return (false, $"Hata oluştu: {ex.Message}", 0, 0, 0);
                }
            });
        }

        private async Task<(bool, string, int, int, int)> ImportExcelAsync(IFormFile file)
        {
            // Dictionaries for deduplication
            var policiesMap = new Dictionary<string, PIPolicy>(StringComparer.OrdinalIgnoreCase);
            var rulesMap = new Dictionary<string, PIRule>(StringComparer.OrdinalIgnoreCase);
            var exceptionsMap = new Dictionary<string, PIException>(StringComparer.OrdinalIgnoreCase);
            var addedRuleClassifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedExcClassifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedExcSeverities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedExcSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedExcDests = new Dictionary<string, PIExceptionDestination>(StringComparer.OrdinalIgnoreCase);
            var addedRuleSeverities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedRuleSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedRuleDests = new Dictionary<string, PIRuleDestination>(StringComparer.OrdinalIgnoreCase);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (int r = 2; r <= rowCount; r++)
            {
                // C1: Name column (formerly Type) — currently all values are "policy"
                var policyName = worksheet.Cell(r, 2).GetString().Trim();
                var ruleName   = worksheet.Cell(r, 3).GetString().Trim();
                if (string.IsNullOrEmpty(policyName)) continue;

                var ruleKey = $"{policyName}|{ruleName}";
                var excName = worksheet.Cell(r, 10).GetString().Trim();
                var excKey  = $"{ruleKey}|{excName}";

                // ── Policy ──────────────────────────────────────────────
                if (!policiesMap.TryGetValue(policyName, out var currentPolicy))
                {
                    currentPolicy = new PIPolicy { PolicyName = policyName };
                    policiesMap[policyName] = currentPolicy;
                }

                // ── Rule ────────────────────────────────────────────────
                PIRule currentRule = null;
                if (!string.IsNullOrEmpty(ruleName))
                {
                    if (!rulesMap.TryGetValue(ruleKey, out currentRule))
                    {
                        currentRule = new PIRule
                        {
                            RuleName = ruleName,
                            PartsCountType = worksheet.Cell(r, 4).GetString(),
                            ConditionRelationType = worksheet.Cell(r, 5).GetString()
                        };
                        currentPolicy.Rules.Add(currentRule);
                        rulesMap[ruleKey] = currentRule;
                    }

                    // C6–C9: Rule Classifier (deduplicated by name)
                    var rClassName = worksheet.Cell(r, 6).GetString().Trim();
                    if (!string.IsNullOrEmpty(rClassName))
                    {
                        var rClassKey = $"{ruleKey}|{rClassName}";
                        if (!addedRuleClassifiers.Contains(rClassKey))
                        {
                            addedRuleClassifiers.Add(rClassKey);
                            int? tvf = null;
                            if (int.TryParse(worksheet.Cell(r, 8).GetString().Trim(), out var tvfParsed)) tvf = tvfParsed;
                            currentRule.Classifiers.Add(new PIRuleClassifier
                            {
                                ClassifierName = rClassName,
                                ThresholdType = worksheet.Cell(r, 7).GetString(),
                                ThresholdValueFrom = tvf,
                                ThresholdCalculateType = worksheet.Cell(r, 9).GetString()
                            });
                        }
                    }

                    // C38–C44: Rule Severity Action (deduplicated by number_of_matches)
                    var ruleSevNomStr = worksheet.Cell(r, 41).GetString().Trim();
                    var ruleSevType   = worksheet.Cell(r, 42).GetString().Trim();
                    if (!string.IsNullOrEmpty(ruleSevType))
                    {
                        var ruleSevKey = $"{ruleKey}|{ruleSevNomStr}|{ruleSevType}";
                        if (!addedRuleSeverities.Contains(ruleSevKey))
                        {
                            addedRuleSeverities.Add(ruleSevKey);
                            int? nom = null;
                            if (int.TryParse(ruleSevNomStr, out var nomParsed)) nom = nomParsed;
                            currentRule.SeverityActions.Add(new PIRuleSeverityAction
                            {
                                Type        = worksheet.Cell(r, 38).GetString(),
                                MaxMatches  = worksheet.Cell(r, 39).GetString(),
                                Selected    = worksheet.Cell(r, 40).GetString(),
                                NumberOfMatches = nom,
                                SeverityType    = ruleSevType,
                                DupSeverityType = worksheet.Cell(r, 43).GetString(),
                                ActionPlan      = worksheet.Cell(r, 44).GetString()
                            });
                        }
                    }

                    // C45–C47: Rule Source (deduplicated by resource_name)
                    var ruleSrcName = worksheet.Cell(r, 45).GetString().Trim();
                    if (!string.IsNullOrEmpty(ruleSrcName))
                    {
                        var ruleSrcKey = $"{ruleKey}|{ruleSrcName}";
                        if (!addedRuleSources.Contains(ruleSrcKey))
                        {
                            addedRuleSources.Add(ruleSrcKey);
                            currentRule.Sources.Add(new PIRuleSource
                            {
                                ResourceName = ruleSrcName,
                                ResourceType = worksheet.Cell(r, 46).GetString(),
                                Include      = worksheet.Cell(r, 47).GetString()
                            });
                        }
                    }

                    // C48–C53: Rule Destination (grouped by channel_type, resources as sub-entries)
                    var ruleDestChanType = worksheet.Cell(r, 49).GetString().Trim();
                    if (!string.IsNullOrEmpty(ruleDestChanType))
                    {
                        var ruleDestKey = $"{ruleKey}|{ruleDestChanType}";
                        if (!addedRuleDests.TryGetValue(ruleDestKey, out var ruleDest))
                        {
                            ruleDest = new PIRuleDestination
                            {
                                EmailMonitorDirections = worksheet.Cell(r, 48).GetString(),
                                ChannelType    = ruleDestChanType,
                                ChannelEnabled = worksheet.Cell(r, 50).GetString()
                            };
                            currentRule.Destinations.Add(ruleDest);
                            addedRuleDests[ruleDestKey] = ruleDest;
                        }
                        // C51–C53: channel resource within rule destination
                        var ruleDestResName = worksheet.Cell(r, 51).GetString().Trim();
                        if (!string.IsNullOrEmpty(ruleDestResName))
                        {
                            if (!ruleDest.ChannelResources.Any(cr => string.Equals(cr.ResourceName, ruleDestResName, StringComparison.OrdinalIgnoreCase)))
                            {
                                ruleDest.ChannelResources.Add(new PIRuleChannelResource
                                {
                                    ResourceName = ruleDestResName,
                                    ResourceType = worksheet.Cell(r, 52).GetString(),
                                    Include      = worksheet.Cell(r, 53).GetString()
                                });
                            }
                        }
                    }
                }

                // ── Exception ────────────────────────────────────────────
                if (!string.IsNullOrEmpty(excName) && currentRule != null)
                {
                    if (!exceptionsMap.TryGetValue(excKey, out var currentExc))
                    {
                        var enStr  = worksheet.Cell(r, 11).GetString(); if (string.IsNullOrEmpty(enStr)) enStr = "true";
                        var ceStr  = worksheet.Cell(r, 13).GetString(); if (string.IsNullOrEmpty(ceStr)) ceStr = "false";
                        var seStr  = worksheet.Cell(r, 14).GetString(); if (string.IsNullOrEmpty(seStr)) seStr = "false";
                        var deStr  = worksheet.Cell(r, 15).GetString(); if (string.IsNullOrEmpty(deStr)) deStr = "false";
                        currentExc = new PIException
                        {
                            ExceptionRuleName     = excName,
                            Enabled               = enStr,
                            Description           = worksheet.Cell(r, 12).GetString(),
                            ConditionEnabled      = ceStr,
                            SourceEnabled         = seStr,
                            DestinationEnabled    = deStr,
                            PartsCountType        = worksheet.Cell(r, 16).GetString(),
                            ConditionRelationType = worksheet.Cell(r, 17).GetString()
                        };
                        currentRule.Exceptions.Add(currentExc);
                        exceptionsMap[excKey] = currentExc;
                    }

                    // C18–C23: Exception Classifier (deduplicated by position+name)
                    var excClassName = worksheet.Cell(r, 18).GetString().Trim();
                    if (!string.IsNullOrEmpty(excClassName))
                    {
                        var excClassPos = worksheet.Cell(r, 19).GetString().Trim();
                        var excClassKey = $"{excKey}|{excClassPos}|{excClassName}";
                        if (!addedExcClassifiers.Contains(excClassKey))
                        {
                            addedExcClassifiers.Add(excClassKey);
                            int? pos = null, etvf = null;
                            if (int.TryParse(excClassPos, out var posParsed)) pos = posParsed;
                            if (int.TryParse(worksheet.Cell(r, 21).GetString().Trim(), out var tvfParsed)) etvf = tvfParsed;
                            currentExc.Classifiers.Add(new PIExceptionClassifier
                            {
                                ClassifierName       = excClassName,
                                Position             = pos,
                                ThresholdType        = worksheet.Cell(r, 20).GetString(),
                                ThresholdValueFrom   = etvf,
                                ThresholdCalculateType  = worksheet.Cell(r, 22).GetString(),
                                AnalyzedSpecificFields  = worksheet.Cell(r, 23).GetString()
                            });
                        }
                    }

                    // C24–C28: Exception Severity Action (deduplicated by number_of_matches+severity_type)
                    var excSevNomStr = worksheet.Cell(r, 25).GetString().Trim();
                    var excSevType   = worksheet.Cell(r, 26).GetString().Trim();
                    if (!string.IsNullOrEmpty(excSevType))
                    {
                        var excSevKey = $"{excKey}|{excSevNomStr}|{excSevType}";
                        if (!addedExcSeverities.Contains(excSevKey))
                        {
                            addedExcSeverities.Add(excSevKey);
                            int? nom = null;
                            if (int.TryParse(excSevNomStr, out var nomParsed)) nom = nomParsed;
                            currentExc.SeverityActions.Add(new PIExceptionSeverityAction
                            {
                                Selected        = worksheet.Cell(r, 24).GetString(),
                                NumberOfMatches = nom,
                                SeverityType    = excSevType,
                                DupSeverityType = worksheet.Cell(r, 27).GetString(),
                                ActionPlan      = worksheet.Cell(r, 28).GetString()
                            });
                        }
                    }

                    // C29–C31: Exception Source (deduplicated by resource_name)
                    var excSrcName = worksheet.Cell(r, 29).GetString().Trim();
                    if (!string.IsNullOrEmpty(excSrcName))
                    {
                        var excSrcKey = $"{excKey}|{excSrcName}";
                        if (!addedExcSources.Contains(excSrcKey))
                        {
                            addedExcSources.Add(excSrcKey);
                            currentExc.Sources.Add(new PIExceptionSource
                            {
                                ResourceName = excSrcName,
                                ResourceType = worksheet.Cell(r, 30).GetString(),
                                Include      = worksheet.Cell(r, 31).GetString()
                            });
                        }
                    }

                    // C32–C37: Exception Destination (grouped by channel_type, resources as sub-entries)
                    var excDestChanType = worksheet.Cell(r, 33).GetString().Trim();
                    if (!string.IsNullOrEmpty(excDestChanType))
                    {
                        var excDestKey = $"{excKey}|{excDestChanType}";
                        if (!addedExcDests.TryGetValue(excDestKey, out var excDest))
                        {
                            excDest = new PIExceptionDestination
                            {
                                EmailMonitorDirections = worksheet.Cell(r, 32).GetString(),
                                ChannelType    = excDestChanType,
                                ChannelEnabled = worksheet.Cell(r, 34).GetString()
                            };
                            currentExc.Destinations.Add(excDest);
                            addedExcDests[excDestKey] = excDest;
                        }
                        // C35–C37: channel resource within exception destination
                        var excDestResName = worksheet.Cell(r, 35).GetString().Trim();
                        if (!string.IsNullOrEmpty(excDestResName))
                        {
                            if (!excDest.ChannelResources.Any(cr => string.Equals(cr.ResourceName, excDestResName, StringComparison.OrdinalIgnoreCase)))
                            {
                                excDest.ChannelResources.Add(new PIExceptionChannelResource
                                {
                                    ResourceName = excDestResName,
                                    ResourceType = worksheet.Cell(r, 36).GetString(),
                                    Include      = worksheet.Cell(r, 37).GetString()
                                });
                            }
                        }
                    }
                }
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var existingPolicies = await _context.PIPolicies.ToListAsync();
                    _context.PIPolicies.RemoveRange(existingPolicies);
                    await _context.SaveChangesAsync();

                    _context.PIPolicies.AddRange(policiesMap.Values);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    int polCount = policiesMap.Count;
                    int ruleCount = rulesMap.Count;
                    int excCount = policiesMap.Values.SelectMany(p => p.Rules).SelectMany(r => r.Exceptions).Count();

                    return (true, "Excel içe aktarma başarılı.", polCount, ruleCount, excCount);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return (false, $"Excel kaydedilirken hata oluştu: {ex.Message}", 0, 0, 0);
                }
            });
        }

        public async Task<byte[]> ExportJsonAsync()
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            var policies = await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                var result = await _context.PIPolicies
                    .Include(p => p.Rules).ThenInclude(r => r.Classifiers)
                    .Include(p => p.Rules).ThenInclude(r => r.SeverityActions)
                    .Include(p => p.Rules).ThenInclude(r => r.Sources)
                    .Include(p => p.Rules).ThenInclude(r => r.Destinations)
                    .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.SeverityActions)
                    .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Sources)
                    .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Destinations)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .ToListAsync();

                await transaction.CommitAsync();
                return result;
            });

            var options = new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(policies, options));
        }

        public async Task<byte[]> ExportExcelAsync()
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            var policies = await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                var result = await _context.PIPolicies
                    .Include(p => p.Rules).ThenInclude(r => r.Classifiers)
                    .Include(p => p.Rules).ThenInclude(r => r.SeverityActions)
                    .Include(p => p.Rules).ThenInclude(r => r.Sources)
                    .Include(p => p.Rules).ThenInclude(r => r.Destinations)
                    .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.SeverityActions)
                    .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Sources)
                    .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Destinations)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .ToListAsync();

                await transaction.CommitAsync();
                return result;
            });

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Politika Envanteri");

            // ── Başlıklar (53 sütun) ────────────────────────────────────────
            ws.Cell(1, 1).Value  = "Name";                              // C1
            ws.Cell(1, 2).Value  = "Value.policy_name";                 // C2
            ws.Cell(1, 3).Value  = "Value.rules.rule_name";             // C3
            ws.Cell(1, 4).Value  = "Value.rules.parts_count_type";      // C4
            ws.Cell(1, 5).Value  = "Value.rules.condition_relation_type"; // C5
            // Rule Classifier
            ws.Cell(1, 6).Value  = "Value.rules.classifiers.classifier_name";         // C6
            ws.Cell(1, 7).Value  = "Value.rules.classifiers.threshold_type";          // C7
            ws.Cell(1, 8).Value  = "Value.rules.classifiers.threshold_value_from";    // C8
            ws.Cell(1, 9).Value  = "Value.rules.classifiers.threshold_calculate_type"; // C9
            // Exception
            ws.Cell(1, 10).Value = "Value.rules.exception_rules.exception_rules.exception_rule_name"; // C10
            ws.Cell(1, 11).Value = "Value.rules.exception_rules.exception_rules.enabled";             // C11
            ws.Cell(1, 12).Value = "Value.rules.exception_rules.exception_rules.description";         // C12
            ws.Cell(1, 13).Value = "Value.rules.exception_rules.exception_rules.condition_enabled";   // C13
            ws.Cell(1, 14).Value = "Value.rules.exception_rules.exception_rules.source_enabled";      // C14
            ws.Cell(1, 15).Value = "Value.rules.exception_rules.exception_rules.destination_enabled"; // C15
            ws.Cell(1, 16).Value = "Value.rules.exception_rules.exception_rules.parts_count_type";    // C16
            ws.Cell(1, 17).Value = "Value.rules.exception_rules.exception_rules.condition_relation_type"; // C17
            // Exception Classifiers
            ws.Cell(1, 18).Value = "Value.rules.exception_rules.exception_rules.classifiers.classifier_name";        // C18
            ws.Cell(1, 19).Value = "Value.rules.exception_rules.exception_rules.classifiers.position";               // C19
            ws.Cell(1, 20).Value = "Value.rules.exception_rules.exception_rules.classifiers.threshold_type";         // C20
            ws.Cell(1, 21).Value = "Value.rules.exception_rules.exception_rules.classifiers.threshold_value_from";   // C21
            ws.Cell(1, 22).Value = "Value.rules.exception_rules.exception_rules.classifiers.threshold_calculate_type"; // C22
            ws.Cell(1, 23).Value = "Value.rules.exception_rules.exception_rules.classifiers.analyzed_specific_fields"; // C23
            // Exception Severity
            ws.Cell(1, 24).Value = "Value.rules.exception_rules.exception_rules.severity_action.classifier_details.selected";          // C24
            ws.Cell(1, 25).Value = "Value.rules.exception_rules.exception_rules.severity_action.classifier_details.number_of_matches"; // C25
            ws.Cell(1, 26).Value = "Value.rules.exception_rules.exception_rules.severity_action.classifier_details.severity_type";     // C26
            ws.Cell(1, 27).Value = "Value.rules.exception_rules.exception_rules.severity_action.classifier_details.dup_severity_type"; // C27
            ws.Cell(1, 28).Value = "Value.rules.exception_rules.exception_rules.severity_action.classifier_details.action_plan";       // C28
            // Exception Source
            ws.Cell(1, 29).Value = "Value.rules.exception_rules.exception_rules.rule_source.resources.resource_name"; // C29
            ws.Cell(1, 30).Value = "Value.rules.exception_rules.exception_rules.rule_source.resources.type";          // C30
            ws.Cell(1, 31).Value = "Value.rules.exception_rules.exception_rules.rule_source.resources.include";       // C31
            // Exception Destination
            ws.Cell(1, 32).Value = "Value.rules.exception_rules.exception_rules.rule_destination.email_monitor_directions"; // C32
            ws.Cell(1, 33).Value = "Value.rules.exception_rules.exception_rules.rule_destination.channels.channel_type";   // C33
            ws.Cell(1, 34).Value = "Value.rules.exception_rules.exception_rules.rule_destination.channels.enabled";        // C34
            ws.Cell(1, 35).Value = "Value.rules.exception_rules.exception_rules.rule_destination.channels.resources.resource_name"; // C35
            ws.Cell(1, 36).Value = "Value.rules.exception_rules.exception_rules.rule_destination.channels.resource.type";          // C36
            ws.Cell(1, 37).Value = "Value.rules.exception_rules.exception_rules.rule_destination.channels.resource.include";       // C37
            // Rule Severity Action
            ws.Cell(1, 38).Value = "Value.rules.type";                              // C38
            ws.Cell(1, 39).Value = "Value.rules.max_matches";                       // C39
            ws.Cell(1, 40).Value = "Value.rules.classifier_details.selected";       // C40
            ws.Cell(1, 41).Value = "Value.rules.classifier_details.number_of_matches"; // C41
            ws.Cell(1, 42).Value = "Value.rules.classifier_details.severity_type";  // C42
            ws.Cell(1, 43).Value = "Value.rules.classifier_details.dup_severity_type"; // C43
            ws.Cell(1, 44).Value = "Value.rules.classifier_details.action_plan";    // C44
            // Rule Source
            ws.Cell(1, 45).Value = "Value.rules.rule_source.resources.resource_name"; // C45
            ws.Cell(1, 46).Value = "Value.rules.rule_source.resources.type";          // C46
            ws.Cell(1, 47).Value = "Value.rules.rule_source.resources.include";       // C47
            // Rule Destination
            ws.Cell(1, 48).Value = "Value.rules.rule_destination.email_monitor_directions";  // C48
            ws.Cell(1, 49).Value = "Value.rules.rule_destination.channels.channel_type";     // C49
            ws.Cell(1, 50).Value = "Value.rules.rule_destination.channels.enabled";          // C50
            ws.Cell(1, 51).Value = "Value.rules.rule_destination.channels.resources.resource_name"; // C51
            ws.Cell(1, 52).Value = "Value.rules.rule_destination.channels.resources.type";          // C52
            ws.Cell(1, 53).Value = "Value.rules.rule_destination.channels.resources.include";        // C53

            ws.Range(ws.Cell(1,1), ws.Cell(1,53)).Style.Font.Bold = true;
            ws.Range(ws.Cell(1,1), ws.Cell(1,53)).Style.Fill.BackgroundColor = XLColor.LightGray;

            int r = 2;
            foreach (var policy in policies)
            {
                foreach (var rule in policy.Rules)
                {
                    var ruleClassifier = rule.Classifiers.FirstOrDefault();

                    // Each exception × each severity action × each destination channel = one row
                    // Build a cross-product of severity actions, sources, destinations per exception
                    var excList  = rule.Exceptions.Any() ? rule.Exceptions.ToList() : new List<PIException> { null };
                    var ruleSevs = rule.SeverityActions.ToList();
                    var ruleSrcs = rule.Sources.ToList();

                    // For rule-level severity+dest we flatten per destination channel row
                    // Collect all dest channels across rule destinations (one row per channel per resource)
                    var ruleDestRows = new List<(PIRuleDestination dest, PIRuleChannelResource? res)>();
                    foreach (var d in rule.Destinations)
                    {
                        if (d.ChannelResources.Any())
                            foreach (var cr in d.ChannelResources) ruleDestRows.Add((d, cr));
                        else
                            ruleDestRows.Add((d, null));
                    }

                    foreach (var exc in excList)
                    {
                        // Build rows for this exception (severity × destination channel × source)
                        var excSevs  = exc?.SeverityActions.ToList() ?? new List<PIExceptionSeverityAction> { null };
                        var excSrcs  = exc?.Sources.ToList() ?? new List<PIExceptionSource> { null };
                        var excDestRows = new List<(PIExceptionDestination dest, PIExceptionChannelResource? res)>();
                        if (exc != null)
                        {
                            foreach (var d in exc.Destinations)
                            {
                                if (d.ChannelResources.Any())
                                    foreach (var cr in d.ChannelResources) excDestRows.Add((d, cr));
                                else
                                    excDestRows.Add((d, null));
                            }
                        }
                        if (!excDestRows.Any()) excDestRows.Add((null, null));

                        // Max rows for this exception (severity or destination, whichever is larger)
                        int excRowCount = Math.Max(Math.Max(excSevs.Count, excSrcs.Count), excDestRows.Count);
                        // Also factor in rule-level rows
                        int ruleRowCount = Math.Max(Math.Max(ruleSevs.Count, ruleSrcs.Count), ruleDestRows.Count > 0 ? ruleDestRows.Count : 1);
                        int rowsToWrite  = Math.Max(excRowCount, ruleRowCount);

                        // Exception classifiers
                        var excClassList = exc?.Classifiers.ToList() ?? new List<PIExceptionClassifier>();

                        for (int i = 0; i < rowsToWrite; i++)
                        {
                            ws.Cell(r, 1).Value = "policy";
                            ws.Cell(r, 2).Value = policy.PolicyName;
                            ws.Cell(r, 3).Value = rule.RuleName;
                            ws.Cell(r, 4).Value = rule.PartsCountType;
                            ws.Cell(r, 5).Value = rule.ConditionRelationType;

                            // C6–C9: Rule Classifier (repeat on every row)
                            if (ruleClassifier != null)
                            {
                                ws.Cell(r, 6).Value = ruleClassifier.ClassifierName;
                                ws.Cell(r, 7).Value = ruleClassifier.ThresholdType;
                                ws.Cell(r, 8).Value = ruleClassifier.ThresholdValueFrom.HasValue ? ruleClassifier.ThresholdValueFrom.Value.ToString() : "";
                                ws.Cell(r, 9).Value = ruleClassifier.ThresholdCalculateType;
                            }

                            // C10–C17: Exception base info (repeat on every row of this exception)
                            if (exc != null)
                            {
                                ws.Cell(r, 10).Value = exc.ExceptionRuleName;
                                ws.Cell(r, 11).Value = exc.Enabled;
                                ws.Cell(r, 12).Value = exc.Description;
                                ws.Cell(r, 13).Value = exc.ConditionEnabled;
                                ws.Cell(r, 14).Value = exc.SourceEnabled;
                                ws.Cell(r, 15).Value = exc.DestinationEnabled;
                                ws.Cell(r, 16).Value = exc.PartsCountType;
                                ws.Cell(r, 17).Value = exc.ConditionRelationType;

                                // C18–C23: Exception Classifier (once per row index)
                                if (i < excClassList.Count)
                                {
                                    var ec = excClassList[i];
                                    ws.Cell(r, 18).Value = ec.ClassifierName;
                                    ws.Cell(r, 19).Value = ec.Position.HasValue ? ec.Position.Value.ToString() : "";
                                    ws.Cell(r, 20).Value = ec.ThresholdType;
                                    ws.Cell(r, 21).Value = ec.ThresholdValueFrom.HasValue ? ec.ThresholdValueFrom.Value.ToString() : "";
                                    ws.Cell(r, 22).Value = ec.ThresholdCalculateType;
                                    ws.Cell(r, 23).Value = ec.AnalyzedSpecificFields;
                                }

                                // C24–C28: Exception Severity
                                if (i < excSevs.Count && excSevs[i] != null)
                                {
                                    var es = excSevs[i];
                                    ws.Cell(r, 24).Value = es.Selected;
                                    ws.Cell(r, 25).Value = es.NumberOfMatches.HasValue ? es.NumberOfMatches.Value.ToString() : "";
                                    ws.Cell(r, 26).Value = es.SeverityType;
                                    ws.Cell(r, 27).Value = es.DupSeverityType;
                                    ws.Cell(r, 28).Value = es.ActionPlan;
                                }

                                // C29–C31: Exception Source
                                if (i < excSrcs.Count && excSrcs[i] != null)
                                {
                                    var esrc = excSrcs[i];
                                    ws.Cell(r, 29).Value = esrc.ResourceName;
                                    ws.Cell(r, 30).Value = esrc.ResourceType;
                                    ws.Cell(r, 31).Value = esrc.Include;
                                }

                                // C32–C37: Exception Destination + Channel Resource
                                if (i < excDestRows.Count)
                                {
                                    var (ed, ecr) = excDestRows[i];
                                    if (ed != null)
                                    {
                                        ws.Cell(r, 32).Value = ed.EmailMonitorDirections;
                                        ws.Cell(r, 33).Value = ed.ChannelType;
                                        ws.Cell(r, 34).Value = ed.ChannelEnabled;
                                        if (ecr != null)
                                        {
                                            ws.Cell(r, 35).Value = ecr.ResourceName;
                                            ws.Cell(r, 36).Value = ecr.ResourceType;
                                            ws.Cell(r, 37).Value = ecr.Include;
                                        }
                                    }
                                }
                            }

                            // C38–C44: Rule Severity Action
                            if (i < ruleSevs.Count)
                            {
                                var rs = ruleSevs[i];
                                ws.Cell(r, 38).Value = rs.Type;
                                ws.Cell(r, 39).Value = rs.MaxMatches;
                                ws.Cell(r, 40).Value = rs.Selected;
                                ws.Cell(r, 41).Value = rs.NumberOfMatches.HasValue ? rs.NumberOfMatches.Value.ToString() : "";
                                ws.Cell(r, 42).Value = rs.SeverityType;
                                ws.Cell(r, 43).Value = rs.DupSeverityType;
                                ws.Cell(r, 44).Value = rs.ActionPlan;
                            }

                            // C45–C47: Rule Source
                            if (i < ruleSrcs.Count)
                            {
                                var rsrc = ruleSrcs[i];
                                ws.Cell(r, 45).Value = rsrc.ResourceName;
                                ws.Cell(r, 46).Value = rsrc.ResourceType;
                                ws.Cell(r, 47).Value = rsrc.Include;
                            }

                            // C48–C53: Rule Destination + Channel Resource
                            if (i < ruleDestRows.Count)
                            {
                                var (rd, rcr) = ruleDestRows[i];
                                if (rd != null)
                                {
                                    ws.Cell(r, 48).Value = rd.EmailMonitorDirections;
                                    ws.Cell(r, 49).Value = rd.ChannelType;
                                    ws.Cell(r, 50).Value = rd.ChannelEnabled;
                                    if (rcr != null)
                                    {
                                        ws.Cell(r, 51).Value = rcr.ResourceName;
                                        ws.Cell(r, 52).Value = rcr.ResourceType;
                                        ws.Cell(r, 53).Value = rcr.Include;
                                    }
                                }
                            }

                            r++;
                        }
                    }
                }
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }
        public async Task<(bool Success, string Message, PIPolicy Data)> CreatePolicyAsync(PIPolicy policy)
        {
            policy.CreatedAt = DateTime.UtcNow;
            policy.UpdatedAt = DateTime.UtcNow;
            _context.PIPolicies.Add(policy);
            await _context.SaveChangesAsync();
            return (true, "Policy created successfully.", policy);
        }

        public async Task<(bool Success, string Message, PIPolicy Data)> UpdatePolicyAsync(int id, PIPolicy policy)
        {
            var existing = await _context.PIPolicies.FindAsync(id);
            if (existing == null) return (false, "Policy not found.", null);

            existing.PolicyName = policy.PolicyName;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Policy updated successfully.", existing);
        }

        public async Task<(bool Success, string Message)> DeletePolicyAsync(int id)
        {
            var existing = await _context.PIPolicies.FindAsync(id);
            if (existing == null) return (false, "Policy not found.");

            _context.PIPolicies.Remove(existing);
            await _context.SaveChangesAsync();
            return (true, "Policy deleted successfully.");
        }

        public async Task<(bool Success, string Message, PIRule Data)> CreateRuleAsync(PIRule rule)
        {
            var policy = await _context.PIPolicies.FindAsync(rule.PolicyId);
            if (policy == null) return (false, "Parent policy not found.", null);

            rule.CreatedAt = DateTime.UtcNow;
            rule.UpdatedAt = DateTime.UtcNow;
            _context.PIRules.Add(rule);
            await _context.SaveChangesAsync();
            return (true, "Rule created successfully.", rule);
        }

        public async Task<(bool Success, string Message, PIRule Data)> UpdateRuleAsync(int id, PIRule rule)
        {
            var existing = await _context.PIRules.FindAsync(id);
            if (existing == null) return (false, "Rule not found.", null);

            existing.RuleName = rule.RuleName;
            existing.PartsCountType = rule.PartsCountType;
            existing.ConditionRelationType = rule.ConditionRelationType;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Rule updated successfully.", existing);
        }

        public async Task<(bool Success, string Message)> DeleteRuleAsync(int id)
        {
            var existing = await _context.PIRules.FindAsync(id);
            if (existing == null) return (false, "Rule not found.");

            _context.PIRules.Remove(existing);
            await _context.SaveChangesAsync();
            return (true, "Rule deleted successfully.");
        }

        public async Task<(bool Success, string Message, PIException Data)> CreateExceptionAsync(PIException exc)
        {
            var rule = await _context.PIRules.FindAsync(exc.RuleId);
            if (rule == null) return (false, "Parent rule not found.", null);

            exc.CreatedAt = DateTime.UtcNow;
            exc.UpdatedAt = DateTime.UtcNow;
            _context.PIExceptions.Add(exc);
            await _context.SaveChangesAsync();
            return (true, "Exception created successfully.", exc);
        }

        public async Task<(bool Success, string Message, PIException Data)> UpdateExceptionAsync(int id, PIException exc)
        {
            var existing = await _context.PIExceptions.FindAsync(id);
            if (existing == null) return (false, "Exception not found.", null);

            existing.ExceptionRuleName = exc.ExceptionRuleName;
            existing.Enabled = exc.Enabled;
            existing.Description = exc.Description;
            existing.ConditionEnabled = exc.ConditionEnabled;
            existing.SourceEnabled = exc.SourceEnabled;
            existing.DestinationEnabled = exc.DestinationEnabled;
            existing.PartsCountType = exc.PartsCountType;
            existing.ConditionRelationType = exc.ConditionRelationType;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Exception updated successfully.", existing);
        }

        public async Task<(bool Success, string Message)> DeleteExceptionAsync(int id)
        {
            var existing = await _context.PIExceptions.FindAsync(id);
            if (existing == null) return (false, "Exception not found.");

            _context.PIExceptions.Remove(existing);
            await _context.SaveChangesAsync();
            return (true, "Exception deleted successfully.");
        }
    }
}
