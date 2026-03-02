import { ArrowRight } from 'lucide-react';
import { useState } from 'react';
import { Pagination } from './Pagination';

const allUsers = [
  { username: 'david.martin@company.com', incidentCount: 423, blockedToday: 34, lastActive: '2 min ago' },
  { username: 'lisa.anderson@company.com', incidentCount: 387, blockedToday: 28, lastActive: '5 min ago' },
  { username: 'robert.taylor@company.com', incidentCount: 356, blockedToday: 45, lastActive: '12 min ago' },
  { username: 'jennifer.white@company.com', incidentCount: 312, blockedToday: 19, lastActive: '18 min ago' },
  { username: 'chris.moore@company.com', incidentCount: 289, blockedToday: 23, lastActive: '24 min ago' },
  { username: 'patricia.lee@company.com', incidentCount: 256, blockedToday: 15, lastActive: '31 min ago' },
  { username: 'daniel.garcia@company.com', incidentCount: 234, blockedToday: 31, lastActive: '38 min ago' },
  { username: 'nancy.martinez@company.com', incidentCount: 198, blockedToday: 12, lastActive: '45 min ago' },
  { username: 'kevin.rodriguez@company.com', incidentCount: 176, blockedToday: 18, lastActive: '52 min ago' },
  { username: 'betty.hernandez@company.com', incidentCount: 154, blockedToday: 9, lastActive: '58 min ago' },
  { username: 'mark.lopez@company.com', incidentCount: 142, blockedToday: 21, lastActive: '1 hr ago' },
  { username: 'sandra.gonzalez@company.com', incidentCount: 128, blockedToday: 14, lastActive: '1 hr ago' },
  { username: 'anthony.wilson@company.com', incidentCount: 115, blockedToday: 7, lastActive: '2 hrs ago' },
  { username: 'carol.moore@company.com', incidentCount: 98, blockedToday: 11, lastActive: '2 hrs ago' },
  { username: 'paul.thomas@company.com', incidentCount: 87, blockedToday: 5, lastActive: '3 hrs ago' },
];

const ITEMS_PER_PAGE = 5;

export function TodayActiveUsers() {
  const [currentPage, setCurrentPage] = useState(1);
  
  const totalPages = Math.ceil(allUsers.length / ITEMS_PER_PAGE);
  const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
  const endIndex = startIndex + ITEMS_PER_PAGE;
  const currentUsers = allUsers.slice(startIndex, endIndex);

  return (
    <div className="bg-white dark:bg-[#1E293B] rounded-2xl p-6 transition-colors" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <h2 className="text-[16px] font-semibold text-[#0F172A] dark:text-[#F1F5F9] mb-6">Today's Active Users</h2>

      <div className="space-y-1">
        {currentUsers.map((user) => (
          <div
            key={user.username}
            className="flex items-center justify-between py-3 px-3 rounded-lg hover:bg-[#F8FAFC] dark:hover:bg-[#334155] transition-colors group"
          >
            <div className="flex-1">
              <div className="text-[14px] text-[#0F172A] dark:text-[#F1F5F9] font-medium mb-1">{user.username}</div>
              <div className="flex items-center gap-3 text-[12px] text-[#64748B] dark:text-[#94A3B8]">
                <span>{user.incidentCount} incidents</span>
                <span>•</span>
                <span className="text-[#EF4444] dark:text-[#FCA5A5] font-semibold">{user.blockedToday} blocked</span>
                <span>•</span>
                <span>{user.lastActive}</span>
              </div>
            </div>

            <div className="flex items-center gap-3">
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