//
// Copyright ical.net project maintainers and contributors.
// Licensed under the MIT license.
//

using System;
using System.Collections;
using System.Collections.Generic;
using Ical.Net.CalendarComponents;

namespace Ical.Net.DataTypes;

/// <summary>
/// A class that represents a specific occurrence of an <see cref="Alarm"/>.
/// </summary>
/// <remarks>
/// The <see cref="AlarmOccurrence"/> contains the <see cref="Period"/> when
/// the alarm occurs, the <see cref="Alarm"/> that fired, and the
/// component on which the alarm fired.
/// </remarks>
public class AlarmOccurrence : IComparable<AlarmOccurrence>
{
    public Period? Period { get; set; }

    public IRecurringComponent? Component { get; set; }

    public Alarm? Alarm { get; set; }

    public CalDateTime? DateTime
    {
        get => Period?.StartTime;
        set => Period = value != null ? new Period(value) : null;
    }

    public AlarmOccurrence(AlarmOccurrence ao)
    {
        Period = ao.Period;
        Component = ao.Component;
        Alarm = ao.Alarm;
    }

    public AlarmOccurrence(Alarm a, CalDateTime dt, IRecurringComponent rc)
    {
        Alarm = a;
        Period = new Period(dt);
        Component = rc;
    }

    public int CompareTo(AlarmOccurrence? other)
    {
        if (other == this) return 0;
        if (other == null) return 1;
        if (Period == null) return -1;
        if (other.Period == null) return 1;
        return Period.CompareTo(other.Period);
    }
}
