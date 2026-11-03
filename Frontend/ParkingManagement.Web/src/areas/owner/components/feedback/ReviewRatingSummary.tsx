import React from 'react';
import { Star } from 'lucide-react';

interface ReviewRatingSummaryProps {
  averageRating: number;
  reviewCount: number;
  starBreakdown: Record<number, number>;
}

export const ReviewRatingSummary: React.FC<ReviewRatingSummaryProps> = ({
  averageRating,
  reviewCount,
  starBreakdown,
}) => {
  const stars = [5, 4, 3, 2, 1];

  const getPercentage = (count: number) => {
    if (!reviewCount || reviewCount === 0) return 0;
    return Math.round((count / reviewCount) * 100);
  };

  return (
    <div className="bg-white rounded-2xl border border-neutral-200 p-6 shadow-xs mb-6">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 items-center">
        {/* Left: Overall Score */}
        <div className="text-center md:border-r border-neutral-100 md:pr-6">
          <div className="text-5xl font-extrabold text-neutral-900 tracking-tight">
            {averageRating > 0 ? averageRating.toFixed(1) : '0.0'}
          </div>
          <div className="flex items-center justify-center gap-1 my-2">
            {[1, 2, 3, 4, 5].map((s) => (
              <Star
                key={s}
                className={`w-5 h-5 ${
                  s <= Math.round(averageRating)
                    ? 'fill-amber-400 text-amber-400'
                    : 'text-neutral-200 fill-neutral-100'
                }`}
              />
            ))}
          </div>
          <p className="text-xs text-neutral-500 font-medium">
            Đánh giá trung bình ({reviewCount} lượt đánh giá)
          </p>
        </div>

        {/* Right: Breakdown distribution bars */}
        <div className="md:col-span-2 space-y-2">
          {stars.map((star) => {
            const count = starBreakdown[star] || 0;
            const pct = getPercentage(count);
            return (
              <div key={star} className="flex items-center gap-3 text-xs">
                <div className="flex items-center gap-1 w-12 font-medium text-neutral-700">
                  <span>{star}</span>
                  <Star className="w-3.5 h-3.5 fill-amber-400 text-amber-400 shrink-0" />
                </div>
                <div className="flex-1 h-2.5 bg-neutral-100 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-amber-400 rounded-full transition-all duration-500"
                    style={{ width: `${pct}%` }}
                  />
                </div>
                <div className="w-16 text-right text-neutral-500 text-2xs font-semibold">
                  <span>{count} ({pct}%)</span>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
