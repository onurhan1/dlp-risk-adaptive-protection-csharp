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
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            // Sadece bizim modellere denk gelen kısımları deserialize etmeye çalışıyoruz.
            // Gerçek dünyada kompleks JSON yapısına özel bir DTO yaratmak gerekir.
            // Bu kısım şimdilik örneklenmiştir.
            
            return (true, "JSON içe aktarma başarılı (taslak).", 0, 0, 0);
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

            // Temizlik ve Kayıt İşlemi
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Varsa eski dataları sil (Mocking / tam sync senaryosu için, gerçekte update mantığı yazılır)
                var existingPolicies = await _context.PIPolicies.ToListAsync();
                _context.PIPolicies.RemoveRange(existingPolicies);
                await _context.SaveChangesAsync();

                _context.PIPolicies.AddRange(policiesMap.Values);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                int polCount = policiesMap.Count;
                int ruleCount = rulesMap.Count;
                int excCount = policiesMap.Values.SelectMany(p => p.Rules).SelectMany(r => r.Exceptions).Count();

                return (true, "Excel başarıyla işlendi.", polCount, ruleCount, excCount);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Hata oluştu: {ex.Message}", 0, 0, 0);
            }
        }
    }
}
