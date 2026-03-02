import { DailyIncidentTrends } from "./components/DailyIncidentTrends";
import { ActionAnalysisWithKPIs } from "./components/ActionAnalysisWithKPIs";
import { DataExfiltrationAlerts } from "./components/DataExfiltrationAlerts";
import { TopRiskyUsers } from "./components/TopRiskyUsers";
import { TodayActiveUsers } from "./components/TodayActiveUsers";
import { DataMovement } from "./components/DataMovement";
import { ThemeToggle } from "./components/ThemeToggle";

export default function App() {
  return (
    <div className="min-h-screen bg-[#F8FAFC] dark:bg-[#0F172A] font-['Inter'] transition-colors">
      {/* Container with 12-column grid, 80px margins */}
      <div
        className="mx-auto"
        style={{ maxWidth: "1440px", padding: "0 80px" }}
      >
        {/* Header */}
        <div className="py-8 flex items-center justify-between">
          <h1 className="text-[24px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">
            Analytics Dashboard
          </h1>
          <ThemeToggle />
        </div>

        {/* Two-column layout: 8 cols + 4 cols */}
        <div className="grid grid-cols-12 gap-6">
          {/* Left Column - 8 cols */}
          <div className="col-span-8 space-y-6">
            <DailyIncidentTrends />
            <ActionAnalysisWithKPIs />
          </div>

          {/* Right Column - 4 cols */}
          <div className="col-span-4">
            <DataExfiltrationAlerts />
          </div>
        </div>

        {/* Bottom Tables - Two equal cards */}
        <div className="grid grid-cols-2 gap-6 mt-6">
          <TopRiskyUsers />
          <TodayActiveUsers />
        </div>

        {/* Data Movement Section */}
        <div className="mt-6 pb-8">
          <DataMovement />
        </div>
      </div>
    </div>
  );
}