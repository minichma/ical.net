
using NodaTime;
using NUnit.Framework;
using static AltRecur.RuleEnumerationUtils;

namespace AltRecur.Test
{
    [TestFixture]
    public class Xp
    {
        [Test]
        public void TestSomething()
        {
            var dtStart = new LocalDateTime(2024, 10, 26, 22, 18, 55);

            IncTimeDelegate freq = (LocalDateTime t) => NextInterval(dtStart, t, PeriodUnits.Days, 1);
            IncTimeDelegate bySec = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Minutes, PeriodUnits.Seconds, [14, 55]);
            IncTimeDelegate byMin = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Hours, PeriodUnits.Minutes, [18, 45]);
            IncTimeDelegate byHour = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Days, PeriodUnits.Hours, [12, 22]);
            IncTimeDelegate byYearDay = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Years, PeriodUnits.Days, [-366], supportNegative: true);
            IncTimeDelegate byMonthDay = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Months, PeriodUnits.Days, [1, -1], supportNegative: true);

            var enumeratble = Enumerate(dtStart, [
                freq,
                bySec,
                byMin,
                byHour]);

            int i = 0;
            foreach (var x in enumeratble)
            {
                Console.WriteLine(x);
                i++;
                if (i >= 100)
                    break;
            }
        }
    }
}
