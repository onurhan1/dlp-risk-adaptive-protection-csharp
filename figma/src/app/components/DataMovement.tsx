import { useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { Pagination } from './Pagination';

const channelData = [
  { title: 'Email', percentage: 68, alerts: 234 },
  { title: 'Cloud Storage', percentage: 52, alerts: 189 },
  { title: 'Web Upload', percentage: 45, alerts: 156 },
  { title: 'File Transfer', percentage: 38, alerts: 127 },
  { title: 'Messaging Apps', percentage: 29, alerts: 98 },
  { title: 'Social Media', percentage: 18, alerts: 67 },
  { title: 'FTP Services', percentage: 35, alerts: 112 },
  { title: 'Remote Desktop', percentage: 22, alerts: 78 },
  { title: 'API Endpoints', percentage: 41, alerts: 145 },
  { title: 'Mobile Apps', percentage: 27, alerts: 89 },
  { title: 'Browser Extensions', percentage: 15, alerts: 52 },
  { title: 'P2P Networks', percentage: 12, alerts: 41 },
];

const destinationData = [
  { title: 'External Email', percentage: 72, alerts: 256 },
  { title: 'Personal Cloud', percentage: 58, alerts: 203 },
  { title: 'USB Devices', percentage: 41, alerts: 142 },
  { title: 'Public Shares', percentage: 35, alerts: 118 },
  { title: 'Mobile Devices', percentage: 27, alerts: 89 },
  { title: 'Print Services', percentage: 15, alerts: 54 },
  { title: 'External APIs', percentage: 48, alerts: 167 },
  { title: 'Public Cloud', percentage: 53, alerts: 186 },
  { title: 'FTP Servers', percentage: 31, alerts: 104 },
  { title: 'Web Services', percentage: 38, alerts: 129 },
  { title: 'Network Shares', percentage: 24, alerts: 82 },
  { title: 'Unknown Hosts', percentage: 19, alerts: 65 },
];

const ITEMS_PER_PAGE = 6;

export function DataMovement() {
  const [activeTab, setActiveTab] = useState<'channel' | 'destination'>('channel');
  const [currentPage, setCurrentPage] = useState(1);
  
  const currentData = activeTab === 'channel' ? channelData : destinationData;
  const totalPages = Math.ceil(currentData.length / ITEMS_PER_PAGE);
  const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
  const endIndex = startIndex + ITEMS_PER_PAGE;
  const paginatedData = currentData.slice(startIndex, endIndex);

  const handleTabChange = (tab: 'channel' | 'destination') => {
    setActiveTab(tab);
    setCurrentPage(1); // Reset to first page when switching tabs
  };

  return (
    <div className="bg-white dark:bg-[#1E293B] rounded-2xl p-6 transition-colors" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <h2 className="text-[16px] font-semibold text-[#0F172A] dark:text-[#F1F5F9] mb-6">Data Movement (30 Days)</h2>

      {/* Tab Switcher */}
      <div className="flex gap-1 mb-6 bg-[#F8FAFC] dark:bg-[#0F172A] rounded-lg p-1 w-fit">
        <button
          onClick={() => handleTabChange('channel')}
          className={`px-4 py-2 text-[14px] rounded-md transition-all ${
            activeTab === 'channel'
              ? 'bg-white dark:bg-[#334155] text-[#0F172A] dark:text-[#F1F5F9] font-semibold shadow-sm'
              : 'text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9]'
          }`}
        >
          Channel
        </button>
        <button
          onClick={() => handleTabChange('destination')}
          className={`px-4 py-2 text-[14px] rounded-md transition-all ${
            activeTab === 'destination'
              ? 'bg-white dark:bg-[#334155] text-[#0F172A] dark:text-[#F1F5F9] font-semibold shadow-sm'
              : 'text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9]'
          }`}
        >
          Destination
        </button>
      </div>

      {/* 2-column grid */}
      <div className="grid grid-cols-2 gap-6 mb-6">
        {paginatedData.map((item) => (
          <div key={item.title} className="p-4 rounded-lg border border-[#E2E8F0] dark:border-[#334155] hover:border-[#CBD5E1] dark:hover:border-[#475569] transition-colors">
            <div className="flex items-start justify-between mb-3">
              <h3 className="text-[14px] font-medium text-[#0F172A] dark:text-[#F1F5F9]">{item.title}</h3>
              <div className="flex items-center gap-1 text-[#EF4444] dark:text-[#FCA5A5]">
                <AlertCircle size={14} />
                <span className="text-[12px] font-semibold">{item.alerts}</span>
              </div>
            </div>

            <div className="text-[28px] font-semibold text-[#0F172A] dark:text-[#F1F5F9] mb-3">{item.percentage}%</div>

            {/* Horizontal Progress Bar */}
            <div className="w-full h-2 bg-[#F1F5F9] dark:bg-[#0F172A] rounded-full overflow-hidden">
              <div
                className="h-full rounded-full transition-all"
                style={{
                  width: `${item.percentage}%`,
                  backgroundColor: item.percentage >= 60 ? '#EF4444' : item.percentage >= 40 ? '#F59E0B' : '#10B981',
                }}
              />
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