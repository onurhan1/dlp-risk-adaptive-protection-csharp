export function KPICards() {
  const kpis = [
    { label: 'Authorized', value: '45,231', color: '#10B981' },
    { label: 'Block', value: '12,842', color: '#EF4444' },
    { label: 'Quarantine', value: '8,394', color: '#8B5CF6' },
    { label: 'Released', value: '116,266', color: '#F59E0B' },
  ];

  return (
    <div className="grid grid-cols-4 gap-6">
      {kpis.map((kpi) => (
        <div
          key={kpi.label}
          className="bg-white dark:bg-[#1E293B] rounded-2xl relative overflow-hidden transition-colors"
          style={{ boxShadow: '0 4px 20px rgba(15,23,42,0.06)' }}
        >
          {/* Colored left bar */}
          <div
            className="absolute left-0 top-0 bottom-0 w-1"
            style={{ backgroundColor: kpi.color }}
          />
          
          <div className="p-6 pl-8">
            <div className="text-[14px] text-[#64748B] dark:text-[#94A3B8] mb-2">{kpi.label}</div>
            <div className="text-[32px] font-semibold text-[#0F172A] dark:text-[#F1F5F9]">{kpi.value}</div>
          </div>
        </div>
      ))}
    </div>
  );
}