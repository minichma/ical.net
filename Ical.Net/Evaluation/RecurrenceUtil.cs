using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Utility;
using System.Collections.Generic;
using System.Linq;

namespace Ical.Net.Evaluation
{
    internal class RecurrenceUtil
    {
        public static void ClearEvaluation(IRecurrable recurrable)
        {
            var evaluator = recurrable.GetService(typeof(IEvaluator)) as IEvaluator;
            evaluator?.Clear();
        }

        public static HashSet<Occurrence> GetOccurrences(IRecurrable recurrable, IDateTime dt, bool includeReferenceDateInResults) => GetOccurrences(recurrable,
            new CalDateTime(dt.AsSystemLocal.Date), new CalDateTime(dt.AsSystemLocal.Date.AddDays(1).AddSeconds(-1)), includeReferenceDateInResults);

        public static HashSet<Occurrence> GetOccurrences(IRecurrable recurrable, IDateTime periodStart, IDateTime periodEnd, bool includeReferenceDateInResults)
        {
            var evaluator = recurrable.GetService(typeof(IEvaluator)) as IEvaluator;
            if (evaluator == null || recurrable.Start == null)
            {
                return new HashSet<Occurrence>();
            }

            // Ensure the start time is associated with the object being queried
            var start = recurrable.Start;
            start.AssociatedObject = recurrable as ICalendarObject;

            // Change the time zone of periodStart/periodEnd as needed
            // so they can be used during the evaluation process.

            periodStart.TzId = start.TzId;
            periodEnd.TzId = start.TzId;

            var periods = evaluator.Evaluate(start, DateUtil.GetSimpleDateTimeData(periodStart), DateUtil.GetSimpleDateTimeData(periodEnd),
                includeReferenceDateInResults);

            var otherOccurrences = from p in periods
                                   let endTime = p.EndTime ?? p.StartTime
                                   where
                                       (endTime.GreaterThan(periodStart) && p.StartTime.LessThan(periodEnd) ||
                                       (periodStart.Equals(periodEnd) && p.StartTime.LessThanOrEqual(periodStart) && endTime.GreaterThan(periodEnd))) || //A period that starts at the same time it ends
                                       (p.StartTime.Equals(endTime) && periodStart.Equals(p.StartTime)) //An event that starts at the same time it ends
                                   select new Occurrence(recurrable, p);

            var occurrences = new HashSet<Occurrence>(otherOccurrences);
            return occurrences;
        }
    }
}