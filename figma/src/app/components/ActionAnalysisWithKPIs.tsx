import { PieChart, Pie, Cell, ResponsiveContainer } from 'recharts';
import { ChevronDown } from 'lucide-react';
import { useState } from 'react';

const datasets = {
  '7days': [
    { name: 'Authorized', value: 6231, color: '#10B981' },
    { name: 'Released', value: 16266, color: '#F59E0B' },
    { name: 'Block', value: 1842, color: '#EF4444' },
    { name: 'Quarantine', value: 1394, color: '#8B5CF6' },
  ],
  '30days': [
    { name: 'Authorized', value: 24567, color: '#10B981' },
    { name: 'Released', value: 65432, color: '#F59E0B' },
    { name: 'Block', value: 7234, color: '#EF4444' },
    { name: 'Quarantine', value: 5123, color: '#8B5CF6' },
  ],
  '90days': [
    { name: 'Authorized', value: 78234, color: '#10B981' },
    { name: 'Released', value: 198765, color: '#F59E0B' },
    { name: 'Block', value: 21456, color: '#EF4444' },
    { name: 'Quarantine', value: 15678, color: '#8B5CF6' },
  ],
  'alltime': [
    { name: 'Authorized', value: 445231, color: '#10B981' },
    { name: 'Released', value: 1116266, color: '#F59E0B' },
    { name: 'Block', value: 112842, color: '#EF4444' },
    { name: 'Quarantine', value: 78394, color: '#8B5CF6' },
  ],
};

const periodLabels = {
  '7days': 'Last 7 Days',
  '30days': 'Last 30 Days',
  '90days': 'Last 90 Days',
  'alltime': 'All Time',
};

export function ActionAnalysisWithKPIs() {
  const [period, setPeriod] = useState<keyof typeof datasets>('alltime');
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  
  const data = datasets[period];
  const total = data.reduce((sum, item) => sum + item.value, 0);

  return (
    <div className="bg-white dark:bg-[#1E293B] rounded-2xl p-6 transition-colors" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-[16px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">Action Analysis</h2>
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
              {(Object.keys(datasets) as Array<keyof typeof datasets>).map((key) => (
                <button
                  key={key}
                  onClick={() => {
                    setPeriod(key);
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

      <div className="flex items-center gap-8">
        {/* Donut Chart */}
        <div className="relative" style={{ width: '280px', height: '280px', flexShrink: 0 }}>
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={data}
                cx="50%"
                cy="50%"
                innerRadius={80}
                outerRadius={110}
                paddingAngle={2}
                dataKey="value"
              >
                {data.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Pie>
            </PieChart>
          </ResponsiveContainer>
          
          {/* Center Text */}
          <div className="absolute inset-0 flex flex-col items-center justify-center" style={{ pointerEvents: 'none' }}>
            <div className="text-[24px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">{total.toLocaleString()}</div>
            <div className="text-[14px] text-[#64748B] dark:text-[#94A3B8]">Total Events</div>
          </div>
        </div>

        {/* KPI Cards Grid */}
        <div className="flex-1 grid grid-cols-2 gap-4">
          {data.map((item) => {
            const percentage = ((item.value / total) * 100).toFixed(1);
            return (
              <div
                key={item.name}
                className="relative overflow-hidden rounded-xl border border-[#E2E8F0] dark:border-[#334155] p-4 transition-colors"
              >
                {/* Colored left bar */}
                <div
                  className="absolute left-0 top-0 bottom-0 w-1"
                  style={{ backgroundColor: item.color }}
                />
                
                <div className="pl-2">
                  <div className="text-[14px] text-[#64748B] dark:text-[#94A3B8] mb-1">{item.name}</div>
                  <div className="text-[28px] font-semibold text-[#0F172A] dark:text-[#F1F5F9] mb-1">
                    {item.value.toLocaleString()}
                  </div>
                  <div className="text-[13px] font-semibold" style={{ color: item.color }}>
                    {percentage}%
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}