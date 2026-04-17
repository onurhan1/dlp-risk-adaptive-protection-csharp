using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using RiskAnalyzerShared = DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer;

namespace DLP.RiskAnalyzer.Tests.Services
{
    public class DashboardAnalyticsServiceTests
    {
        private readonly Mock<IIncidentRepository> _incidentRepoMock;
        private readonly AnalyzerDbContext _context;
        private readonly DashboardAnalyticsService _service;

        public DashboardAnalyticsServiceTests()
        {
            _incidentRepoMock = new Mock<IIncidentRepository>();
            
            // Setup InMemory DB for DbContext testing
            var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AnalyzerDbContext(options);

            // Using null for RiskAnalyzer since it's not used in tested methods overloads
            // Or we just instantiate it without injection safely if it has a paramless ctor
            _service = new DashboardAnalyticsService(
                _incidentRepoMock.Object,
                _context,
                null! // assuming safe if methods under test don't use it
            );
        }

        [Fact]
        public async Task GetUserRiskTrendsAsync_ReturnsMappedData()
        {
            // Arrange
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var aggregatedDtos = new List<UserRiskTrendDto>
            {
                new UserRiskTrendDto("test@test.com", date, 5, 90)
            };

            _incidentRepoMock.Setup(r => r.GetUserRiskTrendsAggregatedAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string?>()))
                .ReturnsAsync(aggregatedDtos);

            // Act
            var result = await _service.GetUserRiskTrendsAsync(7, null);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("test@test.com", result[0].UserEmail);
            Assert.Equal(date, result[0].Date);
            Assert.Equal(5, result[0].TotalIncidents);
            Assert.Equal(90, result[0].RiskScore);
        }

        [Fact]
        public async Task GetDepartmentSummariesAsync_UsesDbContextAndGroupsDataProperly()
        {
            // Arrange
            var today = DateTime.UtcNow;
            
            _context.Incidents.Add(new Incident 
            { 
                Id = 1, UserEmail = "a@test.com", Department = "IT", RiskScore = 60, Timestamp = today
            });
            _context.Incidents.Add(new Incident 
            { 
                Id = 2, UserEmail = "b@test.com", Department = "IT", RiskScore = 40, Timestamp = today
            });
            _context.Incidents.Add(new Incident 
            { 
                Id = 3, UserEmail = "c@test.com", Department = "HR", RiskScore = 80, Timestamp = today
            });
            
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetDepartmentSummariesAsync(
                DateOnly.FromDateTime(today.AddDays(-1)), 
                DateOnly.FromDateTime(today.AddDays(1)));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // IT and HR

            var itDept = result.FirstOrDefault(r => r.Department == "IT");
            Assert.NotNull(itDept);
            Assert.Equal(2, itDept.TotalIncidents);
            Assert.Equal(1, itDept.HighRiskCount); // Only one has score >= 50
            Assert.Equal(50.0, itDept.AvgRiskScore); // (60+40)/2
            Assert.Equal(2, itDept.UniqueUsers);

            var hrDept = result.FirstOrDefault(r => r.Department == "HR");
            Assert.NotNull(hrDept);
            Assert.Equal(1, hrDept.TotalIncidents);
            Assert.Equal(1, hrDept.HighRiskCount);
            Assert.Equal(80.0, hrDept.AvgRiskScore);
        }
    }
}
