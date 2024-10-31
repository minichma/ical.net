using AltRecur;
using Ical.Net.DataTypes;
using Ical.Net.Utility;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using static AltRecur.RuleEnumerationUtils;
using Period = Ical.Net.DataTypes.Period;

namespace Ical.Net.Evaluation
{
    /// <summary>
    /// Much of this code comes from iCal4j, as Ben Fortuna has done an
    /// excellent job with the recurrence pattern evaluation there.
    ///
    /// Here's the iCal4j license:
    /// ==================
    ///  iCal4j - License
    ///  ==================
    ///
    /// Copyright (c) 2009, Ben Fortuna
    /// All rights reserved.
    ///
    /// Redistribution and use in source and binary forms, with or without
    /// modification, are permitted provided that the following conditions
    /// are met:
    ///
    /// o Redistributions of source code must retain the above copyright
    /// notice, this list of conditions and the following disclaimer.
    ///
    /// o Redistributions in binary form must reproduce the above copyright
    /// notice, this list of conditions and the following disclaimer in the
    /// documentation and/or other materials provided with the distribution.
    ///
    /// o Neither the name of Ben Fortuna nor the names of any other contributors
    /// may be used to endorse or promote products derived from this software
    /// without specific prior written permission.
    ///
    /// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
    /// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
    /// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
    /// A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT OWNER OR
    /// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
    /// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
    /// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
    /// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
    /// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
    /// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
    /// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
    /// </summary>
    public class RecurrencePatternEvaluator : Evaluator
    {
        // FIXME: in ical4j this is configurable.
        private const int _maxIncrementCount = 1000;

        protected RecurrencePattern Pattern { get; set; }

        public RecurrencePatternEvaluator(RecurrencePattern pattern)
        {
            Pattern = pattern;
        }

        private void EnforceEvaluationRestrictions(RecurrencePattern pattern)
        {
            RecurrenceEvaluationModeType? evaluationMode = pattern.EvaluationMode;
            RecurrenceRestrictionType? evaluationRestriction = pattern.RestrictionType;

            if (evaluationRestriction != RecurrenceRestrictionType.NoRestriction)
            {
                switch (evaluationMode)
                {
                    case RecurrenceEvaluationModeType.AdjustAutomatically:
                        switch (pattern.Frequency)
                        {
                            case FrequencyType.Secondly:
                                {
                                    switch (evaluationRestriction)
                                    {
                                        case RecurrenceRestrictionType.Default:
                                        case RecurrenceRestrictionType.RestrictSecondly:
                                            pattern.Frequency = FrequencyType.Minutely;
                                            break;
                                        case RecurrenceRestrictionType.RestrictMinutely:
                                            pattern.Frequency = FrequencyType.Hourly;
                                            break;
                                        case RecurrenceRestrictionType.RestrictHourly:
                                            pattern.Frequency = FrequencyType.Daily;
                                            break;
                                    }
                                }
                                break;
                            case FrequencyType.Minutely:
                                {
                                    switch (evaluationRestriction)
                                    {
                                        case RecurrenceRestrictionType.RestrictMinutely:
                                            pattern.Frequency = FrequencyType.Hourly;
                                            break;
                                        case RecurrenceRestrictionType.RestrictHourly:
                                            pattern.Frequency = FrequencyType.Daily;
                                            break;
                                    }
                                }
                                break;
                            case FrequencyType.Hourly:
                                {
                                    switch (evaluationRestriction)
                                    {
                                        case RecurrenceRestrictionType.RestrictHourly:
                                            pattern.Frequency = FrequencyType.Daily;
                                            break;
                                    }
                                }
                                break;
                        }
                        break;
                    case RecurrenceEvaluationModeType.ThrowException:
                    case RecurrenceEvaluationModeType.Default:
                        switch (pattern.Frequency)
                        {
                            case FrequencyType.Secondly:
                                {
                                    switch (evaluationRestriction)
                                    {
                                        case RecurrenceRestrictionType.Default:
                                        case RecurrenceRestrictionType.RestrictSecondly:
                                        case RecurrenceRestrictionType.RestrictMinutely:
                                        case RecurrenceRestrictionType.RestrictHourly:
                                            throw new ArgumentException();
                                    }
                                }
                                break;
                            case FrequencyType.Minutely:
                                {
                                    switch (evaluationRestriction)
                                    {
                                        case RecurrenceRestrictionType.RestrictMinutely:
                                        case RecurrenceRestrictionType.RestrictHourly:
                                            throw new ArgumentException();
                                    }
                                }
                                break;
                            case FrequencyType.Hourly:
                                {
                                    switch (evaluationRestriction)
                                    {
                                        case RecurrenceRestrictionType.RestrictHourly:
                                            throw new ArgumentException();
                                    }
                                }
                                break;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Evaluate the occurrences of this recurrence pattern.
        /// </summary>
        /// <param name="referenceDate">The reference date, i.e. DTSTART.</param>
        /// <param name="periodStart">Start (incl.) of the period occurrences are generated for.</param>
        /// <param name="periodEnd">End (excl.) of the period occurrences are generated for.</param>
        /// <param name="includeReferenceDateInResults">Whether the referenceDate itself should be returned. Ignored as the reference data MUST equal the first occurrence of an RRULE.</param>
        /// <returns></returns>
        public override HashSet<DataTypes.Period> Evaluate(IDateTime referenceDate, DateTime periodStart, DateTime periodEnd, bool includeReferenceDateInResults)
        {
            if ((this.Pattern.Frequency != FrequencyType.None) && (this.Pattern.Frequency < FrequencyType.Daily) && !referenceDate.HasTime)
            {
                // This case is not defined by RFC 5545. We handle it by evaluating the rule
                // as if referenceDate had a time (i.e. set to midnight).

                referenceDate = referenceDate.Copy<IDateTime>();
                referenceDate.HasTime = true;
            }

            EnforceEvaluationRestrictions(Pattern);

            // We treat UNITL as UTC, even if it is set to local time due to https://github.com/ical-org/ical.net/issues/406
            DateTime? until = (Pattern.Until == DateTime.MinValue) ? null : Pattern.Until;
            if (until != null)
            {
                if (until.Value.Kind == DateTimeKind.Utc)
                {
                    if (referenceDate.TzId != null)
                        until = new CalDateTime(until.Value, "UTC").ToTimeZone(referenceDate.TzId).Value;
                }
                else {
                    until = DateTime.SpecifyKind(until.Value, DateTimeKind.Unspecified);
                }
            }

            IReadOnlySet<int> ConvertBy(List<int> by) => (by.Count == 0) ? null : by.ToHashSet();

            var rrule = new RecurrenceRule(
                referenceDate.ToNodaLocalDateTime(),
                referenceDate.HasTime,
                Pattern.Frequency.ToAltRecur(),
                Pattern.Interval,
                ConvertBy(Pattern.ByMonth),
                ConvertBy(Pattern.ByWeekNo),
                ConvertBy(Pattern.ByYearDay),
                ConvertBy(Pattern.ByMonthDay),
                (Pattern.ByDay.Count == 0) ? null : Pattern.ByDay.Select(x => (x.DayOfWeek.ToNodaIsoDayOfWeek(), (x.Offset == int.MinValue) ? (int?)null : x.Offset)).ToHashSet(),
                ConvertBy(Pattern.ByHour),
                ConvertBy(Pattern.ByMinute),
                ConvertBy(Pattern.BySecond),
                ConvertBy(Pattern.BySetPosition),
                (Pattern.Count == int.MinValue) ? null : Pattern.Count,
                (until == null) ? null : LocalDateTime.FromDateTime(until.Value),
                WeekStart: Pattern.FirstDayOfWeek.ToNodaIsoDayOfWeek());

            var res = Enumerate(rrule, LocalDateTime.FromDateTime(periodStart), (periodEnd == DateTime.MaxValue) ? null : LocalDateTime.FromDateTime(periodEnd).PlusTicks(1))
                .Select(x => new DataTypes.Period(x.ToCalDateTime(referenceDate.TzId, referenceDate.HasTime)))
                .ToHashSet();

            return res;
        }
    }
}