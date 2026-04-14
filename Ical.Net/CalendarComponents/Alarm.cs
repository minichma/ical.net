//
// Copyright ical.net project maintainers and contributors.
// Licensed under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using Ical.Net.Utility;

namespace Ical.Net.CalendarComponents;

/// <summary>
/// A class that represents an RFC 2445 VALARM component.
/// FIXME: move GetOccurrences() logic into an AlarmEvaluator.
/// </summary>
public class Alarm : CalendarComponent
{
    public virtual string? Action
    {
        get => Properties.Get<string>(AlarmAction.Key);
        set => Properties.Set(AlarmAction.Key, value);
    }

    public virtual Attachment? Attachment
    {
        get => Properties.Get<Attachment>("ATTACH");
        set => Properties.Set("ATTACH", value);
    }

    public virtual IList<Attendee> Attendees
    {
        get => Properties.GetMany<Attendee>("ATTENDEE");
        set => Properties.Set("ATTENDEE", value);
    }

    public virtual string? Description
    {
        get => Properties.Get<string>("DESCRIPTION");
        set => Properties.Set("DESCRIPTION", value);
    }

    public virtual Duration? Duration
    {
        get => Properties.Get<Duration>("DURATION");
        set => Properties.Set("DURATION", value);
    }

    public virtual int Repeat
    {
        get => Properties.Get<int>("REPEAT");
        set => Properties.Set("REPEAT", value);
    }

    public virtual string? Summary
    {
        get => Properties.Get<string>("SUMMARY");
        set => Properties.Set("SUMMARY", value);
    }

    public virtual Trigger? Trigger
    {
        get => Properties.Get<Trigger>(TriggerRelation.Key);
        set => Properties.Set(TriggerRelation.Key, value);
    }

    public Alarm()
    {
        Name = Components.Alarm;
    }

    /// <summary>
    /// Gets a sequence of alarm occurrences for the given recurring component, <paramref name="rc"/>
    /// that occur at or after <paramref name="fromDate"/>.
    /// </summary>
    public virtual IEnumerable<AlarmOccurrence> GetOccurrences(IRecurringComponent rc, CalDateTime? fromDate, EvaluationOptions? options)
    {
        var occurrences =
            GetOccurrencesUnrepeated(rc, fromDate, options)
            .Select(ao => new[] { ao }.Concat(GetRepeatedItems(ao)))

            // Both, the original occurrences as well as the individual repeated sequences are ordered,
            // so we can merge them in a streaming manner using OrderedNestedMergeMany.
            // The outer, as well as the individual inner sequences will only be enumerated
            // as far as necessary while the returned sequence is being enumerated.
            // This way we can deal with both, an indefinite number of occurrences as well as a large numbers.
            .OrderedNestedMergeMany();

        return occurrences;
    }

    private IEnumerable<AlarmOccurrence> GetOccurrencesUnrepeated(IRecurringComponent rc, CalDateTime? fromDate, EvaluationOptions? options)
    {
        if (Trigger == null)
        {
            yield break;
        }

        // If the trigger is relative, it can recur right along with
        // the recurring items, otherwise, it happens once and
        // only once (at a precise time).
        if (Trigger.IsRelative)
        {
            // Ensure that "FromDate" has already been set
            if (fromDate == null)
            {
                fromDate = rc.Start?.Copy();
            }

            Duration? duration = null;
            foreach (var o in rc.GetOccurrences(fromDate, options))
            {
                var dt = o.Period.StartTime;
                if (string.Equals(Trigger.Related, TriggerRelation.End, TriggerRelation.Comparison))
                {
                    if (o.Period.EndTime != null)
                    {
                        dt = o.Period.EndTime;
                        if (duration == null)
                        {
                            duration = o.Period.EffectiveDuration;
                        }
                    }
                    // Use the "last-found" duration as a reference point
                    else if (duration != null)
                    {
                        dt = o.Period.StartTime.Add(duration.Value);
                    }
                    else
                    {
                        throw new ArgumentException(
                        "Alarm trigger is relative to the START of the occurrence; however, the occurence has no discernible end.");
                }
                }

                yield return new AlarmOccurrence(this, dt.Add(Trigger.Duration!.Value), rc);
            }
        }
        else
        {
            var dt = Trigger?.DateTime?.Copy();
            if (dt != null)
            {
                yield return new AlarmOccurrence(this, dt, rc);
            }
        }
    }

    /// <summary>
    /// Polls the <see cref="Alarm"/> component for alarms that have been triggered
    /// since the provided <paramref name="start"/> date/time.  If <paramref name="start"/>
    /// is null, all triggered alarms will be returned.
    /// </summary>
    /// <param name="start">The earliest date/time to poll triggered alarms for.</param>
    /// <param name="options"></param>
    /// <returns>A list of <see cref="AlarmOccurrence"/> objects, each containing a triggered alarm.</returns>
    public virtual IEnumerable<AlarmOccurrence> Poll(CalDateTime? start, EvaluationOptions? options = null)
    {
        // Evaluate the alarms to determine the recurrences
        if (Parent is not RecurringComponent rc)
        {
            return [];
        }

        return GetOccurrences(rc, start, options);
    }

    /// <summary>
    /// Handles the repetitions that occur from the <c>REPEAT</c> and
    /// <c>DURATION</c> properties.  Each recurrence of the alarm will
    /// have its own set of generated repetitions.
    /// </summary>
    private IEnumerable<AlarmOccurrence> GetRepeatedItems(AlarmOccurrence ao)
    {
        if (ao.DateTime == null || ao.Component == null)
            yield break;

        var alarmTime = ao.DateTime.Copy();

        for (var j = 0; j < Repeat; j++)
        {
            if (Duration != null)
                alarmTime = alarmTime?.Add(Duration.Value);

            if (alarmTime != null)
                yield return new AlarmOccurrence(this, alarmTime.Copy(), ao.Component);
        }
    }
}
