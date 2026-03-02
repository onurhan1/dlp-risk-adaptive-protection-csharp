import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import { useState, useEffect } from 'react';

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ currentPage, totalPages, onPageChange }: PaginationProps) {
  const [inputValue, setInputValue] = useState(currentPage.toString());

  useEffect(() => {
    setInputValue(currentPage.toString());
  }, [currentPage]);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setInputValue(e.target.value);
  };

  const handleInputSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const pageNum = parseInt(inputValue);
    if (pageNum >= 1 && pageNum <= totalPages) {
      onPageChange(pageNum);
    } else {
      setInputValue(currentPage.toString());
    }
  };

  return (
    <div className="flex items-center justify-between pt-4 border-t border-[#E2E8F0] dark:border-[#334155]">
      <div className="text-[13px] text-[#64748B] dark:text-[#94A3B8]">
        Page {currentPage} of {totalPages}
      </div>

      <div className="flex items-center gap-2">
        {/* First Page */}
        <button
          onClick={() => onPageChange(1)}
          disabled={currentPage === 1}
          className="p-2 rounded-lg text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          title="First page"
        >
          <ChevronsLeft size={16} />
        </button>

        {/* Previous Page */}
        <button
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage === 1}
          className="p-2 rounded-lg text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          title="Previous page"
        >
          <ChevronLeft size={16} />
        </button>

        {/* Page Input */}
        <form onSubmit={handleInputSubmit} className="flex items-center gap-2">
          <span className="text-[13px] text-[#64748B] dark:text-[#94A3B8]">Go to</span>
          <input
            type="number"
            min="1"
            max={totalPages}
            value={inputValue}
            onChange={handleInputChange}
            onBlur={handleInputSubmit}
            className="w-16 px-2 py-1 text-[13px] text-center border border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#0F172A] text-[#0F172A] dark:text-[#F1F5F9] rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0F172A] dark:focus:ring-[#F1F5F9] focus:ring-opacity-20 transition-colors"
          />
        </form>

        {/* Next Page */}
        <button
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage === totalPages}
          className="p-2 rounded-lg text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          title="Next page"
        >
          <ChevronRight size={16} />
        </button>

        {/* Last Page */}
        <button
          onClick={() => onPageChange(totalPages)}
          disabled={currentPage === totalPages}
          className="p-2 rounded-lg text-[#64748B] dark:text-[#94A3B8] hover:text-[#0F172A] dark:hover:text-[#F1F5F9] hover:bg-[#F8FAFC] dark:hover:bg-[#334155] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
          title="Last page"
        >
          <ChevronsRight size={16} />
        </button>
      </div>
    </div>
  );
}