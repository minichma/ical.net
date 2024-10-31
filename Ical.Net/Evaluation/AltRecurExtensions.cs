using AltRecur;
using System;

namespace Ical.Net.Evaluation
{
    public static class AltRecurExtensions
    {
        public static RuleFrequency ToAltRecur(this FrequencyType freq)
            => freq switch
            {
                FrequencyType.Secondly => RuleFrequency.Secondly,
                FrequencyType.Minutely => RuleFrequency.Minutely,
                FrequencyType.Hourly => RuleFrequency.Hourly,
                FrequencyType.Daily => RuleFrequency.Daily,
                FrequencyType.Weekly => RuleFrequency.Weekly,
                FrequencyType.Monthly => RuleFrequency.Monthly,
                FrequencyType.Yearly => RuleFrequency.Yearly,
                _ => throw new NotSupportedException()
            };
    }
}
