import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { Calendar, ChevronDown } from 'lucide-react';
import { useEffect, useState } from 'react';

const datasets = {
  '7days': [
    { date: 'Feb 14', authorized: 6842, block: 1923, quarantine: 1234, released: 17482, avgRiskScore: 72 },
    { date: 'Feb 15', authorized: 7123, block: 1654, quarantine: 1089, released: 18347, avgRiskScore: 68 },
    { date: 'Feb 16', authorized: 6234, block: 2145, quarantine: 1456, released: 16234, avgRiskScore: 75 },
    { date: 'Feb 17', authorized: 7456, block: 1834, quarantine: 1123, released: 19456, avgRiskScore: 70 },
    { date: 'Feb 18', authorized: 6987, block: 2034, quarantine: 1345, released: 17987, avgRiskScore: 73 },
    { date: 'Feb 19', authorized: 7234, block: 1756, quarantine: 1187, released: 18567, avgRiskScore: 69 },
    { date: 'Feb 20', authorized: 6543, block: 1987, quarantine: 1298, released: 17234, avgRiskScore: 71 },
  ],
  '14days': [
    { date: 'Feb 7', authorized: 6123, block: 1834, quarantine: 1098, released: 16234, avgRiskScore: 70 },
    { date: 'Feb 8', authorized: 6456, block: 1965, quarantine: 1234, released: 16987, avgRiskScore: 72 },
    { date: 'Feb 9', authorized: 6789, block: 1723, quarantine: 1156, released: 17456, avgRiskScore: 68 },
    { date: 'Feb 10', authorized: 6234, block: 2034, quarantine: 1298, released: 16123, avgRiskScore: 74 },
    { date: 'Feb 11', authorized: 6987, block: 1856, quarantine: 1187, released: 17789, avgRiskScore: 71 },
    { date: 'Feb 12', authorized: 7123, block: 1978, quarantine: 1345, released: 18234, avgRiskScore: 73 },
    { date: 'Feb 13', authorized: 6678, block: 1645, quarantine: 1123, released: 17123, avgRiskScore: 69 },
    { date: 'Feb 14', authorized: 6842, block: 1923, quarantine: 1234, released: 17482, avgRiskScore: 72 },
    { date: 'Feb 15', authorized: 7123, block: 1654, quarantine: 1089, released: 18347, avgRiskScore: 68 },
    { date: 'Feb 16', authorized: 6234, block: 2145, quarantine: 1456, released: 16234, avgRiskScore: 75 },
    { date: 'Feb 17', authorized: 7456, block: 1834, quarantine: 1123, released: 19456, avgRiskScore: 70 },
    { date: 'Feb 18', authorized: 6987, block: 2034, quarantine: 1345, released: 17987, avgRiskScore: 73 },
    { date: 'Feb 19', authorized: 7234, block: 1756, quarantine: 1187, released: 18567, avgRiskScore: 69 },
    { date: 'Feb 20', authorized: 6543, block: 1987, quarantine: 1298, released: 17234, avgRiskScore: 71 },
  ],
  '30days': [
    { date: 'Jan 22', authorized: 5987, block: 1723, quarantine: 1034, released: 15678, avgRiskScore: 69 },
    { date: 'Jan 26', authorized: 6234, block: 1856, quarantine: 1156, released: 16123, avgRiskScore: 71 },
    { date: 'Jan 30', authorized: 6456, block: 1934, quarantine: 1234, released: 16456, avgRiskScore: 72 },
    { date: 'Feb 3', authorized: 6123, block: 1678, quarantine: 1098, released: 15987, avgRiskScore: 68 },
    { date: 'Feb 7', authorized: 6789, block: 1823, quarantine: 1187, released: 17234, avgRiskScore: 70 },
    { date: 'Feb 11', authorized: 6987, block: 1956, quarantine: 1298, released: 17789, avgRiskScore: 73 },
    { date: 'Feb 15', authorized: 7123, block: 1754, quarantine: 1189, released: 18347, avgRiskScore: 68 },
    { date: 'Feb 20', authorized: 6543, block: 1987, quarantine: 1298, released: 17234, avgRiskScore: 71 },
  ],
  '90days': [
    { date: 'Nov 23', authorized: 5234, block: 1523, quarantine: 934, released: 14567, avgRiskScore: 67 },
    { date: 'Dec 7', authorized: 5456, block: 1634, quarantine: 1023, released: 14987, avgRiskScore: 69 },
    { date: 'Dec 21', authorized: 5678, block: 1745, quarantine: 1098, released: 15234, avgRiskScore: 70 },
    { date: 'Jan 4', authorized: 5987, block: 1823, quarantine: 1156, released: 15678, avgRiskScore: 71 },
    { date: 'Jan 18', authorized: 6123, block: 1756, quarantine: 1187, released: 16012, avgRiskScore: 68 },
    { date: 'Feb 1', authorized: 6456, block: 1889, quarantine: 1245, released: 16567, avgRiskScore: 72 },
    { date: 'Feb 15', authorized: 7123, block: 1754, quarantine: 1189, released: 18347, avgRiskScore: 68 },
    { date: 'Feb 20', authorized: 6543, block: 1987, quarantine: 1298, released: 17234, avgRiskScore: 71 },
  ],
};

const timeRangeLabels = {
  '7days': 'Last 7 days',
  '14days': 'Last 14 days',
  '30days': 'Last 30 days',
  '90days': 'Last 90 days',
};

export function DailyIncidentTrends() {
  const [isDark, setIsDark] = useState(false);
  const [timeRange, setTimeRange] = useState<keyof typeof datasets>('7days');
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);

  useEffect(() => {
    // Check initial theme
    setIsDark(document.documentElement.classList.contains('dark'));

    // Watch for theme changes
    const observer = new MutationObserver(() => {
      setIsDark(document.documentElement.classList.contains('dark'));
    });

    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });

    return () => observer.disconnect();
  }, []);

  const data = datasets[timeRange];

  return (
    <div className="bg-white dark:bg-[#1E293B] rounded-2xl p-6 transition-colors" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-[16px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">Daily Incident Trends</h2>
        <div className="relative">
          <button 
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
            className="flex items-center gap-2 px-3 py-2 text-[14px] text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] border border-[#E2E8F0] dark:border-[#334155] rounded-lg transition-colors"
          >
            <Calendar size={16} />
            {timeRangeLabels[timeRange]}
            <ChevronDown size={16} />
          </button>
          
          {isDropdownOpen && (
            <div className="absolute right-0 mt-2 w-48 bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-lg shadow-lg z-10">
              {(Object.keys(datasets) as Array<keyof typeof datasets>).map((key) => (
                <button
                  key={key}
                  onClick={() => {
                    setTimeRange(key);
                    setIsDropdownOpen(false);
                  }}
                  className={`w-full text-left px-4 py-2 text-[14px] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] transition-colors first:rounded-t-lg last:rounded-b-lg ${
                    timeRange === key 
                      ? 'text-[#0F172A] dark:text-[#F1F5F9] font-semibold' 
                      : 'text-[#64748B] dark:text-[#94A3B8]'
                  }`}
                >
                  {timeRangeLabels[key]}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <div style={{ width: '100%', height: '280px' }}>
        <ResponsiveContainer>
          <LineChart data={data} margin={{ top: 5, right: 5, left: 0, bottom: 5 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#E2E8F0" className="dark:stroke-[#334155]" vertical={false} />
            <XAxis 
              dataKey="date" 
              axisLine={false}
              tickLine={false}
              tick={{ fill: '#64748B' }}
              className="dark:fill-[#94A3B8]"
            />
            <YAxis 
              yAxisId="left"
              axisLine={false}
              tickLine={false}
              tick={{ fill: '#64748B' }}
              className="dark:fill-[#94A3B8]"
            />
            <YAxis 
              yAxisId="right"
              orientation="right"
              axisLine={false}
              tickLine={false}
              tick={{ fill: '#64748B' }}
              className="dark:fill-[#94A3B8]"
              domain={[0, 100]}
            />
            <Tooltip 
              contentStyle={{ 
                backgroundColor: 'white', 
                border: '1px solid #E2E8F0',
                borderRadius: '8px',
                fontSize: '14px'
              }}
              wrapperClassName="dark:[&>div]:!bg-[#1E293B] dark:[&>div]:!border-[#334155]"
            />
            <Legend 
              wrapperStyle={{ fontSize: '14px' }}
              iconType="line"
            />
            <Line 
              yAxisId="left"
              type="monotone" 
              dataKey="authorized" 
              stroke="#10B981" 
              strokeWidth={2}
              dot={false}
              name="Authorized"
            />
            <Line 
              yAxisId="left"
              type="monotone" 
              dataKey="block" 
              stroke="#EF4444" 
              strokeWidth={2}
              dot={false}
              name="Block"
            />
            <Line 
              yAxisId="left"
              type="monotone" 
              dataKey="quarantine" 
              stroke="#8B5CF6" 
              strokeWidth={2}
              dot={false}
              name="Quarantine"
            />
            <Line 
              yAxisId="left"
              type="monotone" 
              dataKey="released" 
              stroke="#F59E0B" 
              strokeWidth={2}
              dot={false}
              name="Released"
            />
            <Line 
              yAxisId="right"
              type="monotone" 
              dataKey="avgRiskScore" 
              stroke={isDark ? '#F1F5F9' : '#0F172A'}
              strokeWidth={2}
              strokeDasharray="5 5"
              dot={{ fill: isDark ? '#F1F5F9' : '#0F172A', r: 3 }}
              name="Avg Risk Score"
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}