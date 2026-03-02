import { PieChart, Pie, Cell, ResponsiveContainer } from 'recharts';

const data = [
  { name: 'Authorized', value: 45231, color: '#10B981' },
  { name: 'Released', value: 116266, color: '#F59E0B' },
  { name: 'Block', value: 12842, color: '#EF4444' },
  { name: 'Quarantine', value: 8394, color: '#8B5CF6' },
];

const total = data.reduce((sum, item) => sum + item.value, 0);

export function ActionAnalysis() {
  return (
    <div className="bg-white rounded-2xl p-6" style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}>
      <h2 className="text-[16px] font-semibold text-[#0F172A] mb-6">Action Analysis</h2>

      <div className="flex items-center gap-8">
        {/* Donut Chart */}
        <div className="relative" style={{ width: '280px', height: '280px' }}>
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
          <div className="absolute inset-0 flex flex-col items-center justify-center">
            <div className="text-[24px] font-semibold text-[#0F172A]">{total.toLocaleString()}</div>
            <div className="text-[14px] text-[#64748B]">Total Events</div>
          </div>
        </div>

        {/* Legend */}
        <div className="flex-1 space-y-4">
          {data.map((item) => {
            const percentage = ((item.value / total) * 100).toFixed(1);
            return (
              <div key={item.name} className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div
                    className="w-3 h-3 rounded-full"
                    style={{ backgroundColor: item.color }}
                  />
                  <span className="text-[14px] text-[#0F172A]">{item.name}</span>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-[14px] text-[#64748B]">{item.value.toLocaleString()}</span>
                  <span className="text-[14px] font-semibold text-[#0F172A] min-w-[50px] text-right">
                    {percentage}%
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
