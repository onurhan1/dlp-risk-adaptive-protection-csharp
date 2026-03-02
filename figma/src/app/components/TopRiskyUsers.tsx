import { ArrowRight, ChevronDown } from 'lucide-react';
import { useState } from 'react';
import { Pagination } from './Pagination';

const allUsersData = {
  'week': [
    { username: 'john.smith@company.com', incidentCount: 147, daysActive: 7, riskScore: 84, status: 'Critical' },
    { username: 'sarah.jones@company.com', incidentCount: 132, daysActive: 6, riskScore: 78, status: 'High' },
    { username: 'mike.wilson@company.com', incidentCount: 98, daysActive: 5, riskScore: 65, status: 'High' },
    { username: 'emma.davis@company.com', incidentCount: 87, daysActive: 4, riskScore: 61, status: 'Medium' },
    { username: 'alex.brown@company.com', incidentCount: 76, daysActive: 7, riskScore: 58, status: 'Medium' },
    { username: 'david.martin@company.com', incidentCount: 65, daysActive: 5, riskScore: 52, status: 'Medium' },
    { username: 'lisa.anderson@company.com', incidentCount: 54, daysActive: 4, riskScore: 48, status: 'Low' },
    { username: 'robert.taylor@company.com', incidentCount: 43, daysActive: 3, riskScore: 42, status: 'Low' },
  ],
  'month': [
    { username: 'john.smith@company.com', incidentCount: 623, daysActive: 23, riskScore: 91, status: 'Critical' },
    { username: 'sarah.jones@company.com', incidentCount: 542, daysActive: 21, riskScore: 86, status: 'Critical' },
    { username: 'mike.wilson@company.com', incidentCount: 456, daysActive: 19, riskScore: 74, status: 'High' },
    { username: 'emma.davis@company.com', incidentCount: 398, daysActive: 17, riskScore: 69, status: 'High' },
    { username: 'alex.brown@company.com', incidentCount: 342, daysActive: 22, riskScore: 63, status: 'Medium' },
    { username: 'david.martin@company.com', incidentCount: 298, daysActive: 18, riskScore: 59, status: 'Medium' },
    { username: 'lisa.anderson@company.com', incidentCount: 256, daysActive: 15, riskScore: 55, status: 'Medium' },
    { username: 'robert.taylor@company.com', incidentCount: 213, daysActive: 14, riskScore: 51, status: 'Low' },
    { username: 'jennifer.white@company.com', incidentCount: 187, daysActive: 16, riskScore: 47, status: 'Low' },
    { username: 'chris.moore@company.com', incidentCount: 165, daysActive: 13, riskScore: 43, status: 'Low' },
  ],
  '3months': [
    { username: 'john.smith@company.com', incidentCount: 1247, daysActive: 67, riskScore: 94, status: 'Critical' },
    { username: 'sarah.jones@company.com', incidentCount: 982, daysActive: 58, riskScore: 87, status: 'Critical' },
    { username: 'mike.wilson@company.com', incidentCount: 856, daysActive: 52, riskScore: 72, status: 'High' },
    { username: 'emma.davis@company.com', incidentCount: 743, daysActive: 48, riskScore: 68, status: 'High' },
    { username: 'alex.brown@company.com', incidentCount: 691, daysActive: 61, riskScore: 61, status: 'Medium' },
    { username: 'david.martin@company.com', incidentCount: 623, daysActive: 54, riskScore: 58, status: 'Medium' },
    { username: 'lisa.anderson@company.com', incidentCount: 587, daysActive: 47, riskScore: 54, status: 'Medium' },
    { username: 'robert.taylor@company.com', incidentCount: 521, daysActive: 42, riskScore: 49, status: 'Low' },
    { username: 'jennifer.white@company.com', incidentCount: 478, daysActive: 51, riskScore: 45, status: 'Low' },
    { username: 'chris.moore@company.com', incidentCount: 432, daysActive: 44, riskScore: 42, status: 'Low' },
    { username: 'patricia.lee@company.com', incidentCount: 398, daysActive: 38, riskScore: 38, status: 'Low' },
    { username: 'daniel.garcia@company.com', incidentCount: 356, daysActive: 40, riskScore: 35, status: 'Low' },
  ],
  '6months': [
    { username: 'john.smith@company.com', incidentCount: 2456, daysActive: 134, riskScore: 96, status: 'Critical' },
    { username: 'sarah.jones@company.com', incidentCount: 2187, daysActive: 128, riskScore: 92, status: 'Critical' },
    { username: 'mike.wilson@company.com', incidentCount: 1923, daysActive: 115, riskScore: 78, status: 'High' },
    { username: 'emma.davis@company.com', incidentCount: 1756, daysActive: 107, riskScore: 74, status: 'High' },
    { username: 'alex.brown@company.com', incidentCount: 1598, daysActive: 129, riskScore: 68, status: 'High' },
    { username: 'david.martin@company.com', incidentCount: 1432, daysActive: 118, riskScore: 64, status: 'Medium' },
    { username: 'lisa.anderson@company.com', incidentCount: 1298, daysActive: 102, riskScore: 59, status: 'Medium' },
    { username: 'robert.taylor@company.com', incidentCount: 1156, daysActive: 96, riskScore: 54, status: 'Medium' },
    { username: 'jennifer.white@company.com', incidentCount: 1023, daysActive: 112, riskScore: 50, status: 'Low' },
    { username: 'chris.moore@company.com', incidentCount: 934, daysActive: 98, riskScore: 46, status: 'Low' },
    { username: 'patricia.lee@company.com', incidentCount: 867, daysActive: 87, riskScore: 42, status: 'Low' },
    { username: 'daniel.garcia@company.com', incidentCount: 798, daysActive: 91, riskScore: 39, status: 'Low' },
    { username: 'nancy.martinez@company.com', incidentCount: 723, daysActive: 79, riskScore: 36, status: 'Low' },
    { username: 'kevin.rodriguez@company.com', incidentCount: 678, daysActive: 74, riskScore: 33, status: 'Low' },
  ],
  'year': [
    { username: 'john.smith@company.com', incidentCount: 4923, daysActive: 287, riskScore: 98, status: 'Critical' },
    { username: 'sarah.jones@company.com', incidentCount: 4456, daysActive: 276, riskScore: 95, status: 'Critical' },
    { username: 'mike.wilson@company.com', incidentCount: 3987, daysActive: 254, riskScore: 82, status: 'High' },
    { username: 'emma.davis@company.com', incidentCount: 3623, daysActive: 243, riskScore: 77, status: 'High' },
    { username: 'alex.brown@company.com', incidentCount: 3298, daysActive: 271, riskScore: 72, status: 'High' },
    { username: 'david.martin@company.com', incidentCount: 2987, daysActive: 259, riskScore: 67, status: 'High' },
    { username: 'lisa.anderson@company.com', incidentCount: 2723, daysActive: 234, riskScore: 63, status: 'Medium' },
    { username: 'robert.taylor@company.com', incidentCount: 2456, daysActive: 218, riskScore: 58, status: 'Medium' },
    { username: 'jennifer.white@company.com', incidentCount: 2187, daysActive: 245, riskScore: 54, status: 'Medium' },
    { username: 'chris.moore@company.com', incidentCount: 1998, daysActive: 227, riskScore: 50, status: 'Low' },
    { username: 'patricia.lee@company.com', incidentCount: 1823, daysActive: 198, riskScore: 46, status: 'Low' },
    { username: 'daniel.garcia@company.com', incidentCount: 1687, daysActive: 209, riskScore: 43, status: 'Low' },
    { username: 'nancy.martinez@company.com', incidentCount: 1534, daysActive: 187, riskScore: 40, status: 'Low' },
    { username: 'kevin.rodriguez@company.com', incidentCount: 1423, daysActive: 176, riskScore: 37, status: 'Low' },
    { username: 'betty.hernandez@company.com', incidentCount: 1298, daysActive: 165, riskScore: 34, status: 'Low' },
  ],
  'alltime': [
    { username: 'john.smith@company.com', incidentCount: 8734, daysActive: 512, riskScore: 99, status: 'Critical' },
    { username: 'sarah.jones@company.com', incidentCount: 7923, daysActive: 498, riskScore: 97, status: 'Critical' },
    { username: 'mike.wilson@company.com', incidentCount: 7156, daysActive: 467, riskScore: 86, status: 'High' },
    { username: 'emma.davis@company.com', incidentCount: 6542, daysActive: 445, riskScore: 81, status: 'High' },
    { username: 'alex.brown@company.com', incidentCount: 5987, daysActive: 489, riskScore: 76, status: 'High' },
    { username: 'david.martin@company.com', incidentCount: 5423, daysActive: 473, riskScore: 71, status: 'High' },
    { username: 'lisa.anderson@company.com', incidentCount: 4934, daysActive: 432, riskScore: 67, status: 'High' },
    { username: 'robert.taylor@company.com', incidentCount: 4456, daysActive: 412, riskScore: 62, status: 'Medium' },
    { username: 'jennifer.white@company.com', incidentCount: 4023, daysActive: 451, riskScore: 58, status: 'Medium' },
    { username: 'chris.moore@company.com', incidentCount: 3678, daysActive: 423, riskScore: 54, status: 'Medium' },
    { username: 'patricia.lee@company.com', incidentCount: 3356, daysActive: 378, riskScore: 50, status: 'Medium' },
    { username: 'daniel.garcia@company.com', incidentCount: 3098, daysActive: 398, riskScore: 47, status: 'Low' },
    { username: 'nancy.martinez@company.com', incidentCount: 2823, daysActive: 356, riskScore: 44, status: 'Low' },
    { username: 'kevin.rodriguez@company.com', incidentCount: 2612, daysActive: 334, riskScore: 41, status: 'Low' },
    { username: 'betty.hernandez@company.com', incidentCount: 2398, daysActive: 312, riskScore: 38, status: 'Low' },
  ],
};

const periodLabels = {
  'week': 'Last Week',
  'month': 'Last Month',
  '3months': 'Last 3 Months',
  '6months': 'Last 6 Months',
  'year': 'Last Year',
  'alltime': 'All Time',
};

const ITEMS_PER_PAGE = 5;

export function TopRiskyUsers() {
  const [currentPage, setCurrentPage] = useState(1);
  const [period, setPeriod] = useState<keyof typeof allUsersData>('3months');
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  
  const allUsers = allUsersData[period];
  const totalPages = Math.ceil(allUsers.length / ITEMS_PER_PAGE);
  const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
  const endIndex = startIndex + ITEMS_PER_PAGE;
  const currentUsers = allUsers.slice(startIndex, endIndex);

  return (
    <div className="bg-white dark:bg-[#1E293B] rounded-2xl p-6 transition-colors" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-[16px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">Top Risky Users</h2>
        <div className="relative">
          <button 
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
            className="flex items-center gap-2 px-3 py-2 text-[14px] text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] border border-[#E2E8F0] dark:border-[#334155] rounded-lg transition-colors"
          >
            {periodLabels[period]}
            <ChevronDown size={16} />
          </button>
          
          {isDropdownOpen && (
            <div className="absolute right-0 mt-2 w-48 bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-lg shadow-lg z-10">
              {(Object.keys(allUsersData) as Array<keyof typeof allUsersData>).map((key) => (
                <button
                  key={key}
                  onClick={() => {
                    setPeriod(key);
                    setCurrentPage(1);
                    setIsDropdownOpen(false);
                  }}
                  className={`w-full text-left px-4 py-2 text-[14px] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] transition-colors first:rounded-t-lg last:rounded-b-lg ${
                    period === key 
                      ? 'text-[#0F172A] dark:text-[#F1F5F9] font-semibold' 
                      : 'text-[#64748B] dark:text-[#94A3B8]'
                  }`}
                >
                  {periodLabels[key]}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="space-y-1">
        {currentUsers.map((user, index) => (
          <div
            key={user.username}
            className="flex items-center justify-between py-3 px-3 rounded-lg hover:bg-[#F8FAFC] dark:hover:bg-[#334155] transition-colors group"
          >
            <div className="flex-1">
              <div className="text-[14px] text-[#0F172A] dark:text-[#F1F5F9] font-medium mb-1">{user.username}</div>
              <div className="flex items-center gap-3 text-[12px] text-[#64748B] dark:text-[#94A3B8]">
                <span>{user.incidentCount} incidents</span>
                <span>•</span>
                <span>{user.daysActive} days active</span>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <span
                className="px-2 py-1 text-[11px] font-semibold rounded"
                style={{
                  backgroundColor: 
                    user.status === 'Critical' ? (document.documentElement.classList.contains('dark') ? '#450a0a' : '#FEF2F2') : 
                    user.status === 'High' ? (document.documentElement.classList.contains('dark') ? '#451a03' : '#FEF3C7') : 
                    user.status === 'Medium' ? (document.documentElement.classList.contains('dark') ? '#1e1b4b' : '#EEF2FF') : 
                    (document.documentElement.classList.contains('dark') ? '#052e16' : '#F0FDF4'),
                  color: 
                    user.status === 'Critical' ? (document.documentElement.classList.contains('dark') ? '#FCA5A5' : '#EF4444') : 
                    user.status === 'High' ? (document.documentElement.classList.contains('dark') ? '#FCD34D' : '#F59E0B') : 
                    user.status === 'Medium' ? (document.documentElement.classList.contains('dark') ? '#A5B4FC' : '#6366F1') : 
                    (document.documentElement.classList.contains('dark') ? '#86EFAC' : '#10B981'),
                }}
              >
                {user.riskScore}
              </span>

              <button className="px-3 py-1.5 text-[12px] text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] hover:bg-white dark:hover:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-lg transition-all flex items-center gap-1">
                Investigate
                <ArrowRight size={12} />
              </button>
            </div>
          </div>
        ))}
      </div>

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        onPageChange={setCurrentPage}
      />
    </div>
  );
}