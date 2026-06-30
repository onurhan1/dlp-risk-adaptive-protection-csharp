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
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                                
                                if (rElement.TryGetProperty("classifiers", out var classElement) && classElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var cElement in classElement.EnumerateArray())
                                    {
                                        rule.Classifiers.Add(new PIRuleClassifier
                                        {
                                            ClassifierName = cElement.TryGetProperty("classifier_name", out var cn) ? cn.GetString() : null,
                                            ThresholdType = cElement.TryGetProperty("threshold_type", out var tt) ? tt.GetString() : null,
                                            ThresholdCalculateType = cElement.TryGetProperty("threshold_calculate_type", out var tct) ? tct.GetString() : null
                                        });
                                    }
                                }

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

                                            if (eElement.TryGetProperty("severity_action", out var sevObj) && sevObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                if (sevObj.TryGetProperty("classifier_details", out var cdArr) && cdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var cdElement in cdArr.EnumerateArray())
                                                    {
                                                        exc.SeverityActions.Add(new PIExceptionSeverityAction
                                                        {
                                                            Selected = cdElement.TryGetProperty("selected", out var sel) ? sel.GetString() : null,
                                                            SeverityType = cdElement.TryGetProperty("severity_type", out var st) ? st.GetString() : null,
                                                            DupSeverityType = cdElement.TryGetProperty("dup_severity_type", out var dst) ? dst.GetString() : null,
                                                            ActionPlan = cdElement.TryGetProperty("action_plan", out var ap) ? ap.GetString() : null
                                                        });
                                                    }
                                                }
                                            }

                                            if (eElement.TryGetProperty("source", out var srcObj) && srcObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                if (srcObj.TryGetProperty("network_details", out var ndArr) && ndArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var ndElement in ndArr.EnumerateArray())
                                                    {
                                                        exc.Sources.Add(new PIExceptionSource
                                                        {
                                                            ResourceName = ndElement.TryGetProperty("resource_name", out var srn) ? srn.GetString() : null,
                                                            ResourceType = ndElement.TryGetProperty("resource_type", out var srt) ? srt.GetString() : null,
                                                            Include = ndElement.TryGetProperty("include", out var inc) ? inc.GetString() : null
                                                        });
                                                    }
                                                }
                                            }

                                            if (eElement.TryGetProperty("destination", out var destObj) && destObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                var emailDir = destObj.TryGetProperty("email_monitor_directions", out var emd) ? emd.GetString() : null;
                                                if (destObj.TryGetProperty("channel_details", out var cdArr) && cdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var cdElement in cdArr.EnumerateArray())
                                                    {
                                                        exc.Destinations.Add(new PIExceptionDestination
                                                        {
                                                            EmailMonitorDirections = emailDir,
                                                            ChannelType = cdElement.TryGetProperty("channel_type", out var ct) ? ct.GetString() : null,
                                                            ChannelEnabled = cdElement.TryGetProperty("enabled", out var ce2) ? ce2.GetString() : null
                                                        });
                                                    }
                                                }
                                            }

                                            rule.Exceptions.Add(exc);
                                        }
                                    }
                                }

                                if (rElement.TryGetProperty("severity_action", out var rSevObj) && rSevObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    var maxMatches = rSevObj.TryGetProperty("max_matches", out var mm) ? mm.GetString() : null;
                                    if (rSevObj.TryGetProperty("classifier_details", out var cdArr) && cdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var cdElement in cdArr.EnumerateArray())
                                        {
                                            rule.SeverityActions.Add(new PIRuleSeverityAction
                                            {
                                                MaxMatches = maxMatches,
                                                Selected = cdElement.TryGetProperty("selected", out var sel) ? sel.GetString() : null,
                                                SeverityType = cdElement.TryGetProperty("severity_type", out var st) ? st.GetString() : null,
                                                DupSeverityType = cdElement.TryGetProperty("dup_severity_type", out var dst) ? dst.GetString() : null,
                                                ActionPlan = cdElement.TryGetProperty("action_plan", out var ap) ? ap.GetString() : null
                                            });
                                        }
                                    }
                                }

                                if (rElement.TryGetProperty("source", out var rSrcObj) && rSrcObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (rSrcObj.TryGetProperty("network_details", out var ndArr) && ndArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var ndElement in ndArr.EnumerateArray())
                                        {
                                            rule.Sources.Add(new PIRuleSource
                                            {
                                                ResourceName = ndElement.TryGetProperty("resource_name", out var rn2) ? rn2.GetString() : null,
                                                ResourceType = ndElement.TryGetProperty("resource_type", out var rt) ? rt.GetString() : null,
                                                Include = ndElement.TryGetProperty("include", out var inc) ? inc.GetString() : null
                                            });
                                        }
                                    }
                                }

                                if (rElement.TryGetProperty("destination", out var rDestObj) && rDestObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    var emailDir = rDestObj.TryGetProperty("email_monitor_directions", out var emd) ? emd.GetString() : null;
                                    if (rDestObj.TryGetProperty("channel_details", out var cdArr) && cdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var cdElement in cdArr.EnumerateArray())
                                        {
                                            rule.Destinations.Add(new PIRuleDestination
                                            {
                                                EmailMonitorDirections = emailDir,
                                                ChannelType = cdElement.TryGetProperty("channel_type", out var ct) ? ct.GetString() : null,
                                                ChannelEnabled = cdElement.TryGetProperty("enabled", out var ce2) ? ce2.GetString() : null
                                            });
                                        }
                                    }
                                }

                                policy.Rules.Add(rule);
                            }
                        }
                        importedPolicies.Add(policy);
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
            var policiesMap = new Dictionary<string, PIPolicy>();
            var rulesMap = new Dictionary<string, PIRule>();

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (int r = 2; r <= rowCount; r++)
            {
                var type = worksheet.Cell(r, 1).GetString().Trim();
                var policyName = worksheet.Cell(r, 2).GetString().Trim();
                var ruleName = worksheet.Cell(r, 3).GetString().Trim();

                if (string.IsNullOrEmpty(policyName)) continue;

                // Ensure Policy exists in memory
                if (!policiesMap.TryGetValue(policyName, out var currentPolicy))
                {
                    currentPolicy = new PIPolicy { PolicyName = policyName };
                    policiesMap[policyName] = currentPolicy;
                }

                // Ensure Rule exists in memory
                var ruleKey = $"{policyName}_{ruleName}";
                PIRule currentRule = null;
                if (!string.IsNullOrEmpty(ruleName))
                {
                    if (!rulesMap.TryGetValue(ruleKey, out currentRule))
                    {
                        currentRule = new PIRule { RuleName = ruleName };
                        currentPolicy.Rules.Add(currentRule);
                        rulesMap[ruleKey] = currentRule;
                    }
                }

                if (type == "policy")
                {
                    // Rule ana bilgileri (Col 4-5)
                    if (currentRule != null)
                    {
                        currentRule.PartsCountType = worksheet.Cell(r, 4).GetString();
                        currentRule.ConditionRelationType = worksheet.Cell(r, 5).GetString();

                        // Kural Classifier (Col 6-9)
                        var rClass = worksheet.Cell(r, 6).GetString();
                        if (!string.IsNullOrEmpty(rClass))
                        {
                            currentRule.Classifiers.Add(new PIRuleClassifier
                            {
                                ClassifierName = rClass,
                                ThresholdType = worksheet.Cell(r, 7).GetString(),
                                ThresholdCalculateType = worksheet.Cell(r, 9).GetString()
                            });
                        }
                    }

                    // Exception bilgileri (Col 10-17)
                    var excName = worksheet.Cell(r, 10).GetString();
                    if (!string.IsNullOrEmpty(excName) && currentRule != null)
                    {
                        var exc = new PIException
                        {
                            ExceptionRuleName = excName,
                            Enabled = worksheet.Cell(r, 11).GetString(),
                            Description = worksheet.Cell(r, 12).GetString(),
                            ConditionEnabled = worksheet.Cell(r, 13).GetString(),
                            SourceEnabled = worksheet.Cell(r, 14).GetString(),
                            DestinationEnabled = worksheet.Cell(r, 15).GetString(),
                            PartsCountType = worksheet.Cell(r, 16).GetString(),
                            ConditionRelationType = worksheet.Cell(r, 17).GetString()
                        };

                        // Exception Severity Action (Col 24-28)
                        var excSevType = worksheet.Cell(r, 26).GetString();
                        if (!string.IsNullOrEmpty(excSevType))
                        {
                            exc.SeverityActions.Add(new PIExceptionSeverityAction
                            {
                                Selected = worksheet.Cell(r, 24).GetString(),
                                SeverityType = excSevType,
                                DupSeverityType = worksheet.Cell(r, 27).GetString(),
                                ActionPlan = worksheet.Cell(r, 28).GetString()
                            });
                        }

                        // Exception Source (Col 29-31)
                        var excSrcName = worksheet.Cell(r, 29).GetString();
                        if (!string.IsNullOrEmpty(excSrcName))
                        {
                            exc.Sources.Add(new PIExceptionSource
                            {
                                ResourceName = excSrcName,
                                ResourceType = worksheet.Cell(r, 30).GetString(),
                                Include = worksheet.Cell(r, 31).GetString()
                            });
                        }

                        // Exception Destination (Col 32-34)
                        var excDestChannel = worksheet.Cell(r, 33).GetString();
                        if (!string.IsNullOrEmpty(excDestChannel))
                        {
                            exc.Destinations.Add(new PIExceptionDestination
                            {
                                ChannelType = excDestChannel,
                                ChannelEnabled = worksheet.Cell(r, 34).GetString(),
                                EmailMonitorDirections = worksheet.Cell(r, 32).GetString()
                            });
                        }

                        currentRule.Exceptions.Add(exc);
                    }
                }
                else if (type == "severity_action" && currentRule != null)
                {
                    // Rule Severity Actions (Col 38-44)
                    currentRule.SeverityActions.Add(new PIRuleSeverityAction
                    {
                        Type = worksheet.Cell(r, 38).GetString(),
                        MaxMatches = worksheet.Cell(r, 39).GetString(),
                        Selected = worksheet.Cell(r, 40).GetString(),
                        SeverityType = worksheet.Cell(r, 42).GetString(),
                        DupSeverityType = worksheet.Cell(r, 43).GetString(),
                        ActionPlan = worksheet.Cell(r, 44).GetString()
                    });
                }
                else if (type == "source_destination" && currentRule != null)
                {
                    // Rule Source (Col 45-47)
                    var srcName = worksheet.Cell(r, 45).GetString();
                    if (!string.IsNullOrEmpty(srcName))
                    {
                        currentRule.Sources.Add(new PIRuleSource
                        {
                            ResourceName = srcName,
                            ResourceType = worksheet.Cell(r, 46).GetString(),
                            Include = worksheet.Cell(r, 47).GetString()
                        });
                    }

                    // Rule Destination (Col 48-50)
                    var destChannel = worksheet.Cell(r, 49).GetString();
                    if (!string.IsNullOrEmpty(destChannel))
                    {
                        currentRule.Destinations.Add(new PIRuleDestination
                        {
                            ChannelType = destChannel,
                            ChannelEnabled = worksheet.Cell(r, 50).GetString(),
                            EmailMonitorDirections = worksheet.Cell(r, 48).GetString()
                        });
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
            var policies = await _context.PIPolicies
                .Include(p => p.Rules).ThenInclude(r => r.Classifiers)
                .Include(p => p.Rules).ThenInclude(r => r.SeverityActions)
                .Include(p => p.Rules).ThenInclude(r => r.Sources)
                .Include(p => p.Rules).ThenInclude(r => r.Destinations).ThenInclude(d => d.ChannelResources)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Classifiers)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.SeverityActions)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Sources)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Destinations).ThenInclude(d => d.ChannelResources)
                .AsNoTracking()
                .ToListAsync();

            var options = new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(policies, options));
        }

        public async Task<byte[]> ExportExcelAsync()
        {
            var policies = await _context.PIPolicies
                .Include(p => p.Rules).ThenInclude(r => r.Classifiers)
                .Include(p => p.Rules).ThenInclude(r => r.SeverityActions)
                .Include(p => p.Rules).ThenInclude(r => r.Sources)
                .Include(p => p.Rules).ThenInclude(r => r.Destinations)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.SeverityActions)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Sources)
                .Include(p => p.Rules).ThenInclude(r => r.Exceptions).ThenInclude(e => e.Destinations)
                .AsNoTracking()
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Politika Envanteri");

            // Başlıklar
            ws.Cell(1, 1).Value = "Type";
            ws.Cell(1, 2).Value = "policy_name";
            ws.Cell(1, 3).Value = "rule_name";
            ws.Cell(1, 4).Value = "parts_count_type";
            ws.Cell(1, 5).Value = "condition_relation_type";
            ws.Cell(1, 6).Value = "classifier_name";
            ws.Cell(1, 7).Value = "threshold_type";
            ws.Cell(1, 9).Value = "threshold_calculate_type";
            ws.Cell(1, 10).Value = "exception_rule_name";
            ws.Cell(1, 11).Value = "enabled";
            ws.Cell(1, 12).Value = "description";
            ws.Cell(1, 13).Value = "condition_enabled";
            ws.Cell(1, 14).Value = "source_enabled";
            ws.Cell(1, 15).Value = "destination_enabled";
            ws.Cell(1, 16).Value = "exc_parts_count_type";
            ws.Cell(1, 17).Value = "exc_condition_relation_type";
            ws.Cell(1, 24).Value = "exc_selected";
            ws.Cell(1, 26).Value = "exc_severity_type";
            ws.Cell(1, 27).Value = "exc_dup_severity_type";
            ws.Cell(1, 28).Value = "exc_action_plan";
            ws.Cell(1, 29).Value = "exc_resource_name";
            ws.Cell(1, 30).Value = "exc_resource_type";
            ws.Cell(1, 31).Value = "exc_include";
            ws.Cell(1, 32).Value = "exc_email_monitor_directions";
            ws.Cell(1, 33).Value = "exc_channel_type";
            ws.Cell(1, 34).Value = "exc_channel_enabled";
            
            // Rule Severity & Source Dest Başlıkları
            ws.Cell(1, 38).Value = "rule_sev_type";
            ws.Cell(1, 39).Value = "rule_sev_max_matches";
            ws.Cell(1, 40).Value = "rule_sev_selected";
            ws.Cell(1, 42).Value = "rule_sev_severity_type";
            ws.Cell(1, 43).Value = "rule_sev_dup_severity";
            ws.Cell(1, 44).Value = "rule_sev_action_plan";
            
            ws.Cell(1, 45).Value = "rule_src_resource_name";
            ws.Cell(1, 46).Value = "rule_src_resource_type";
            ws.Cell(1, 47).Value = "rule_src_include";
            
            ws.Cell(1, 48).Value = "rule_dest_email_monitor_directions";
            ws.Cell(1, 49).Value = "rule_dest_channel_type";
            ws.Cell(1, 50).Value = "rule_dest_channel_enabled";

            ws.Range("A1:AX1").Style.Font.Bold = true;
            ws.Range("A1:AX1").Style.Fill.BackgroundColor = XLColor.LightGray;

            int r = 2;
            foreach (var policy in policies)
            {
                foreach (var rule in policy.Rules)
                {
                    // 1. Policy Satırı ve Exceptions
                    if (rule.Exceptions.Any())
                    {
                        foreach (var exc in rule.Exceptions)
                        {
                            ws.Cell(r, 1).Value = "policy";
                            ws.Cell(r, 2).Value = policy.PolicyName;
                            ws.Cell(r, 3).Value = rule.RuleName;
                            ws.Cell(r, 4).Value = rule.PartsCountType;
                            ws.Cell(r, 5).Value = rule.ConditionRelationType;

                            var ruleClassifier = rule.Classifiers.FirstOrDefault();
                            if (ruleClassifier != null)
                            {
                                ws.Cell(r, 6).Value = ruleClassifier.ClassifierName;
                                ws.Cell(r, 7).Value = ruleClassifier.ThresholdType;
                                ws.Cell(r, 9).Value = ruleClassifier.ThresholdCalculateType;
                            }

                            ws.Cell(r, 10).Value = exc.ExceptionRuleName;
                            ws.Cell(r, 11).Value = exc.Enabled;
                            ws.Cell(r, 12).Value = exc.Description;
                            ws.Cell(r, 13).Value = exc.ConditionEnabled;
                            ws.Cell(r, 14).Value = exc.SourceEnabled;
                            ws.Cell(r, 15).Value = exc.DestinationEnabled;
                            ws.Cell(r, 16).Value = exc.PartsCountType;
                            ws.Cell(r, 17).Value = exc.ConditionRelationType;

                            var excSev = exc.SeverityActions.FirstOrDefault();
                            if (excSev != null)
                            {
                                ws.Cell(r, 24).Value = excSev.Selected;
                                ws.Cell(r, 26).Value = excSev.SeverityType;
                                ws.Cell(r, 27).Value = excSev.DupSeverityType;
                                ws.Cell(r, 28).Value = excSev.ActionPlan;
                            }

                            var excSrc = exc.Sources.FirstOrDefault();
                            if (excSrc != null)
                            {
                                ws.Cell(r, 29).Value = excSrc.ResourceName;
                                ws.Cell(r, 30).Value = excSrc.ResourceType;
                                ws.Cell(r, 31).Value = excSrc.Include;
                            }

                            var excDest = exc.Destinations.FirstOrDefault();
                            if (excDest != null)
                            {
                                ws.Cell(r, 32).Value = excDest.EmailMonitorDirections;
                                ws.Cell(r, 33).Value = excDest.ChannelType;
                                ws.Cell(r, 34).Value = excDest.ChannelEnabled;
                            }
                            r++;
                        }
                    }
                    else
                    {
                        ws.Cell(r, 1).Value = "policy";
                        ws.Cell(r, 2).Value = policy.PolicyName;
                        ws.Cell(r, 3).Value = rule.RuleName;
                        ws.Cell(r, 4).Value = rule.PartsCountType;
                        ws.Cell(r, 5).Value = rule.ConditionRelationType;
                        r++;
                    }

                    // 2. Rule Severity Actions Satırları
                    foreach (var sev in rule.SeverityActions)
                    {
                        ws.Cell(r, 1).Value = "severity_action";
                        ws.Cell(r, 2).Value = policy.PolicyName;
                        ws.Cell(r, 3).Value = rule.RuleName;
                        
                        ws.Cell(r, 38).Value = sev.Type;
                        ws.Cell(r, 39).Value = sev.MaxMatches;
                        ws.Cell(r, 40).Value = sev.Selected;
                        ws.Cell(r, 42).Value = sev.SeverityType;
                        ws.Cell(r, 43).Value = sev.DupSeverityType;
                        ws.Cell(r, 44).Value = sev.ActionPlan;
                        r++;
                    }

                    // 3. Rule Source Destination Satırları (Birleştirilmiş gösterim)
                    int maxSrcDest = Math.Max(rule.Sources.Count, rule.Destinations.Count);
                    var srcList = rule.Sources.ToList();
                    var destList = rule.Destinations.ToList();

                    for (int i = 0; i < maxSrcDest; i++)
                    {
                        ws.Cell(r, 1).Value = "source_destination";
                        ws.Cell(r, 2).Value = policy.PolicyName;
                        ws.Cell(r, 3).Value = rule.RuleName;

                        if (i < srcList.Count)
                        {
                            ws.Cell(r, 45).Value = srcList[i].ResourceName;
                            ws.Cell(r, 46).Value = srcList[i].ResourceType;
                            ws.Cell(r, 47).Value = srcList[i].Include;
                        }

                        if (i < destList.Count)
                        {
                            ws.Cell(r, 48).Value = destList[i].EmailMonitorDirections;
                            ws.Cell(r, 49).Value = destList[i].ChannelType;
                            ws.Cell(r, 50).Value = destList[i].ChannelEnabled;
                        }
                        r++;
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
