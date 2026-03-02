import { ArrowRight, ChevronDown, ChevronRight } from 'lucide-react';
import { useState } from 'react';
import { Pagination } from './Pagination';

const allAlerts = [
  {
    id: 1,
    severity: 'CRITICAL',
    username: 'john.smith@company.com',
    matches: 247,
    riskScore: 94,
    date: 'Feb 19, 10:24 AM',
    destination: 'personal-cloud@gmail.com',
    maxMatch: 'SSN-XXX-XX-1234',
    fileType: 'Customer Database.xlsx',
    dataVolume: '2.4 GB',
  },
  {
    id: 2,
    severity: 'CRITICAL',
    username: 'sarah.jones@company.com',
    matches: 189,
    riskScore: 87,
    date: 'Feb 19, 9:15 AM',
    destination: 'external-storage.dropbox.com',
    maxMatch: 'Credit Card Numbers',
    fileType: 'Financial_Report_Q4.pdf',
    dataVolume: '1.8 GB',
  },
  {
    id: 3,
    severity: 'HIGH',
    username: 'mike.wilson@company.com',
    matches: 156,
    riskScore: 72,
    date: 'Feb 19, 8:42 AM',
    destination: 'usb-device-E:/',
    maxMatch: 'Employee Records',
    fileType: 'HR_Data_2026.csv',
    dataVolume: '890 MB',
  },
  {
    id: 4,
    severity: 'CRITICAL',
    username: 'emma.davis@company.com',
    matches: 203,
    riskScore: 91,
    date: 'Feb 18, 11:30 PM',
    destination: 'private@outlook.com',
    maxMatch: 'API Keys & Credentials',
    fileType: 'prod_configs.json',
    dataVolume: '145 MB',
  },
  {
    id: 5,
    severity: 'HIGH',
    username: 'alex.brown@company.com',
    matches: 134,
    riskScore: 68,
    date: 'Feb 18, 10:18 PM',
    destination: 'file-share.onedrive.net',
    maxMatch: 'Patient Health Info',
    fileType: 'Medical_Records.zip',
    dataVolume: '3.2 GB',
  },
  {
    id: 6,
    severity: 'CRITICAL',
    username: 'thomas.anderson@company.com',
    matches: 221,
    riskScore: 89,
    date: 'Feb 18, 9:45 PM',
    destination: 'external-ftp.server.com',
    maxMatch: 'Financial Statements',
    fileType: 'Q4_Financials.xlsx',
    dataVolume: '1.2 GB',
  },
  {
    id: 7,
    severity: 'HIGH',
    username: 'rachel.green@company.com',
    matches: 142,
    riskScore: 70,
    date: 'Feb 18, 8:30 PM',
    destination: 'personal@yahoo.com',
    maxMatch: 'Client Contracts',
    fileType: 'Contracts_Archive.zip',
    dataVolume: '2.1 GB',
  },
  {
    id: 8,
    severity: 'CRITICAL',
    username: 'monica.geller@company.com',
    matches: 198,
    riskScore: 85,
    date: 'Feb 18, 7:15 PM',
    destination: 'cloud-backup.icloud.com',
    maxMatch: 'Payment Info',
    fileType: 'payment_data.csv',
    dataVolume: '567 MB',
  },
  {
    id: 9,
    severity: 'HIGH',
    username: 'chandler.bing@company.com',
    matches: 167,
    riskScore: 73,
    date: 'Feb 18, 6:00 PM',
    destination: 'usb-device-F:/',
    maxMatch: 'Trade Secrets',
    fileType: 'proprietary_tech.pdf',
    dataVolume: '890 MB',
  },
  {
    id: 10,
    severity: 'MEDIUM',
    username: 'joey.tribbiani@company.com',
    matches: 98,
    riskScore: 55,
    date: 'Feb 18, 5:30 PM',
    destination: 'external@hotmail.com',
    maxMatch: 'Internal Memos',
    fileType: 'company_memos.docx',
    dataVolume: '234 MB',
  },
  {
    id: 11,
    severity: 'HIGH',
    username: 'ross.geller@company.com',
    matches: 178,
    riskScore: 76,
    date: 'Feb 18, 4:45 PM',
    destination: 'personal-drive.box.com',
    maxMatch: 'Customer Data',
    fileType: 'customer_list.xlsx',
    dataVolume: '1.5 GB',
  },
  {
    id: 12,
    severity: 'CRITICAL',
    username: 'phoebe.buffay@company.com',
    matches: 234,
    riskScore: 92,
    date: 'Feb 18, 3:20 PM',
    destination: 'unknown-server.net',
    maxMatch: 'Encryption Keys',
    fileType: 'security_keys.json',
    dataVolume: '89 MB',
  },
];

const ITEMS_PER_PAGE = 5;

export function DataExfiltrationAlerts() {
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  const totalPages = Math.ceil(allAlerts.length / ITEMS_PER_PAGE);
  const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
  const endIndex = startIndex + ITEMS_PER_PAGE;
  const currentAlerts = allAlerts.slice(startIndex, endIndex);

  const toggleExpand = (id: number) => {
    setExpandedId(expandedId === id ? null : id);
  };

  return (
    <div className="bg-white dark:bg-[#1E293B] rounded-2xl p-6 h-full flex flex-col transition-colors" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-[16px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">Potential Data Exfiltration</h2>
        <div className="px-2 py-1 bg-[#FEF2F2] dark:bg-[#450a0a] text-[#EF4444] dark:text-[#FCA5A5] text-[12px] font-semibold rounded">
          {allAlerts.length} alerts
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 flex-1">
        {currentAlerts.map((alert) => {
          const isExpanded = expandedId === alert.id;
          
          return (
            <div
              key={alert.id}
              className="relative border-l-2 pl-3 py-3 cursor-pointer transition-all h-fit"
              style={{ 
                borderColor: alert.severity === 'CRITICAL' ? '#EF4444' : alert.severity === 'HIGH' ? '#F59E0B' : '#6366F1',
              }}
              onClick={() => toggleExpand(alert.id)}
            >
              <div className="flex items-start justify-between mb-2">
                <div className="flex items-center gap-1">
                  {isExpanded ? (
                    <ChevronDown size={14} className="text-[#64748B] dark:text-[#94A3B8]" />
                  ) : (
                    <ChevronRight size={14} className="text-[#64748B] dark:text-[#94A3B8]" />
                  )}
                  <span
                    className="px-2 py-1 text-[10px] font-semibold rounded"
                    style={{
                      backgroundColor: 
                        alert.severity === 'CRITICAL' ? (document.documentElement.classList.contains('dark') ? '#450a0a' : '#FEF2F2') : 
                        alert.severity === 'HIGH' ? (document.documentElement.classList.contains('dark') ? '#451a03' : '#FEF3C7') : 
                        (document.documentElement.classList.contains('dark') ? '#1e1b4b' : '#EEF2FF'),
                      color: 
                        alert.severity === 'CRITICAL' ? (document.documentElement.classList.contains('dark') ? '#FCA5A5' : '#EF4444') : 
                        alert.severity === 'HIGH' ? (document.documentElement.classList.contains('dark') ? '#FCD34D' : '#F59E0B') : 
                        (document.documentElement.classList.contains('dark') ? '#A5B4FC' : '#6366F1'),
                    }}
                  >
                    {alert.severity}
                  </span>
                </div>
              </div>

              <div className="text-[13px] text-[#0F172A] dark:text-[#F1F5F9] font-medium mb-2 truncate">
                {alert.username}
              </div>

              <div className="flex flex-col gap-1 text-[11px] text-[#64748B] dark:text-[#94A3B8] mb-2">
                <span>{alert.matches} matches</span>
                <span className="font-semibold text-[#0F172A] dark:text-[#F1F5F9]">Risk: {alert.riskScore}</span>
                <span>{alert.date}</span>
              </div>

              {isExpanded && (
                <div className="mt-3 pt-3 border-t border-[#E2E8F0] dark:border-[#334155] space-y-2">
                  <div>
                    <div className="text-[10px] text-[#64748B] dark:text-[#94A3B8] mb-1">Destination</div>
                    <div className="text-[11px] text-[#0F172A] dark:text-[#F1F5F9] font-medium break-all">{alert.destination}</div>
                  </div>
                  <div>
                    <div className="text-[10px] text-[#64748B] dark:text-[#94A3B8] mb-1">Max Match</div>
                    <div className="text-[11px] text-[#0F172A] dark:text-[#F1F5F9] font-medium">{alert.maxMatch}</div>
                  </div>
                  <div>
                    <div className="text-[10px] text-[#64748B] dark:text-[#94A3B8] mb-1">File Type</div>
                    <div className="text-[11px] text-[#0F172A] dark:text-[#F1F5F9] font-medium">{alert.fileType}</div>
                  </div>
                  <div>
                    <div className="text-[10px] text-[#64748B] dark:text-[#94A3B8] mb-1">Data Volume</div>
                    <div className="text-[11px] text-[#0F172A] dark:text-[#F1F5F9] font-medium">{alert.dataVolume}</div>
                  </div>
                </div>
              )}

              <button 
                className="text-[11px] text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] flex items-center gap-1 transition-colors mt-2"
                onClick={(e) => {
                  e.stopPropagation();
                }}
              >
                Investigate
                <ArrowRight size={12} />
              </button>
            </div>
          );
        })}
      </div>

      <Pagination
        currentPage={currentPage}
        totalPages={totalPages}
        onPageChange={setCurrentPage}
      />
    </div>
  );
}