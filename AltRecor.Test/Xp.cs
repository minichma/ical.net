
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

            //IncTimeDelegate freq = (LocalDateTime t) => NextInterval(dtStart, t, PeriodUnits.Days, 3);
            //IncTimeDelegate bySec = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Minutes, PeriodUnits.Seconds, [14, 55]);
            //IncTimeDelegate byMin = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Hours, PeriodUnits.Minutes, [18, 45]);
            //IncTimeDelegate byHour = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Days, PeriodUnits.Hours, [12, 22]);
            //IncTimeDelegate byYearDay = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Years, PeriodUnits.Days, [-366], supportNegative: true);
            //IncTimeDelegate byMonthDay = (LocalDateTime t) => FindCurrentOrNextBy(t, PeriodUnits.Months, PeriodUnits.Days, [1, -1], supportNegative: true);
            //IncTimeDelegate byDay = (LocalDateTime t) => FindCurrentOrNextByDay(t, PeriodUnits.Months, [(IsoDayOfWeek.Sunday, -1)]);

            //var enumerable = Enumerate(dtStart.PlusWeeks(0), dtStart.PlusYears(1), [
            //    freq]);

            var f1 = (LocalDateTime t) => FindCurrentOrNextInterval(dtStart, t, PeriodUnits.Days, 1, null);
            var f2 = (LocalDateTime t) => FindCurrentOrNextByDay(t, PeriodUnits.Months, [
                (IsoDayOfWeek.Monday, null),
                (IsoDayOfWeek.Tuesday, null),
                (IsoDayOfWeek.Wednesday, null),
                (IsoDayOfWeek.Thursday, null),
                (IsoDayOfWeek.Friday, null)]);

            var enumerableFactory = Enumerate([f1, f2]);
            enumerableFactory = EnumerateWithSetPos(PeriodUnits.Months, enumerableFactory, [-1]);
            enumerableFactory = EnumerateWithCount(dtStart, enumerableFactory, 3);
            
            int i = 0;
            foreach (var x in enumerableFactory((dtStart, null)))
            {
                Console.WriteLine(x);
                i++;
                if (i >= 100)
                    break;
            }
        }
    }
}
