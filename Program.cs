using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Reflection;
using MPXJ.Net;

var converterVersion = StableVersion(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
var mpxjAssembly = typeof(UniversalProjectReader).Assembly;
var mpxjVersion = StableVersion(mpxjAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
    ?? StableVersion(mpxjAssembly.GetName().Version?.ToString());

if (args.Length == 1 && args[0] == "--version")
{
    Console.WriteLine(converterVersion ?? "unknown");
    return 0;
}

if (args.Length == 1 && args[0] == "--info")
{
    if (converterVersion == null || mpxjVersion == null)
    {
        Console.Error.WriteLine("Error: assembly version metadata is unavailable.");
        return 1;
    }
    Console.WriteLine(JsonSerializer.Serialize(new { converterVersion, mpxjVersion }));
    return 0;
}

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Virtuart4DConvert <input> <output.v4d.json>");
    Console.Error.WriteLine("       Virtuart4DConvert --version");
    return 1;
}

var inputPath  = args[0];
var outputPath = args[1];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
    return 1;
}

try
{
    ProjectFile project;
    try
    {
        project = new UniversalProjectReader().Read(inputPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error reading file: {ex.Message}");
        return 1;
    }

    if (project == null)
    {
        Console.Error.WriteLine("Error: File could not be parsed.");
        return 1;
    }

    var props = project.ProjectProperties;

    // --- Calendars ---
    var calendars = new List<CalendarDto>();
    foreach (var cal in project.Calendars)
    {
        if (cal == null) continue;
        if (!TryBuildWorkWeek(cal, out var workWeek, out var workWeekError))
            throw new ConversionException(workWeekError);
        if (!TryBuildExceptions(cal, out var exceptions, out var exceptionError))
            throw new ConversionException(exceptionError);
        if (!TryAppendWorkWeekExceptions(cal, exceptions, out var derivedWeekError))
            throw new ConversionException(derivedWeekError);
        var parent = cal.Parent;
        calendars.Add(new CalendarDto
        {
            Uid             = cal.UniqueID ?? 0,
            Name            = cal.Name ?? "",
            IsBase          = parent == null,
            BaseCalendarUid = parent?.UniqueID ?? -1,
            WorkWeek        = workWeek,
            Exceptions      = exceptions,
        });
    }

    // --- Resources ---
    var resources = new List<ResourceDto>();
    foreach (var res in project.Resources)
    {
        if (res == null) continue;
        var uid = res.UniqueID ?? 0;
        if (uid == 0) continue;
        resources.Add(new ResourceDto
        {
            Uid         = uid,
            Name        = res.Name ?? "",
            Type        = res.Type?.ToString() ?? "Work",
            StdRate     = res.StandardRate?.Amount,
            CalendarUid = res.Calendar?.UniqueID ?? -1,
        });
    }

    // --- Tasks ---
    var tasks = new List<TaskDto>();
    var omittedUids = new List<(int Uid, string Name)>();
    var seenUids = new HashSet<int>();
    var eligibleCount = 0;
    foreach (var task in project.Tasks)
    {
        if (task == null) continue;
        var uid = task.UniqueID;
        var isSyntheticRoot = uid.HasValue && uid.Value == 0;
        if (isSyntheticRoot) continue;
        if (!uid.HasValue || uid.Value < 0)
            throw new ConversionException($"task uid={(uid?.ToString(CultureInfo.InvariantCulture) ?? "missing")} source=mpxj field=uid value={(uid.HasValue ? "negative" : "missing")}");
        if (!seenUids.Add(uid.Value))
            throw new ConversionException($"task uid={uid.Value} source=mpxj field=uid value=duplicate");
        eligibleCount++;

        if (task.ParentTask != null && task.ParentTask.UniqueID == null)
            throw new ConversionException($"task uid={uid.Value} source=mpxj field=parentUid value=unreadable");

        var preds = new List<PredecessorDto>();
        if (task.Predecessors != null)
        {
            foreach (var rel in task.Predecessors)
            {
                if (rel == null) continue;
                if (rel.PredecessorTask == null)
                    throw new ConversionException($"task uid={uid.Value} source=mpxj field=predecessorUid value=unreadable");
                preds.Add(new PredecessorDto
                {
                    Uid      = rel.PredecessorTask.UniqueID ?? 0,
                    Type     = rel.Type?.ToString() ?? "FS",
                    LagHours = DurationToHours(rel.Lag),
                });
            }
        }

        var start = task.Start;
        var finish = task.Finish;
        if (start == null && finish == null)
        {
            omittedUids.Add((uid.Value, task.Name ?? ""));
            continue;
        }
        if (start == null || finish == null)
            throw new ConversionException($"task uid={uid.Value} source=mpxj field=dates value=partial");
        if (start.Value > finish.Value)
            throw new ConversionException($"task uid={uid.Value} source=mpxj field=dates value=inverted");

        var assignments = new List<AssignmentDto>();
        if (task.ResourceAssignments != null)
        {
            foreach (var ra in task.ResourceAssignments)
            {
                if (ra == null) continue;
                var resUid = ra.ResourceUniqueID ?? 0;
                if (resUid == 0) continue;
                assignments.Add(new AssignmentDto
                {
                    ResourceUid = resUid,
                    Units       = ra.Units ?? 1.0,
                    WorkHours   = DurationToHours(ra.Work),
                });
            }
        }

        tasks.Add(new TaskDto
        {
            Uid                 = uid.Value,
            Guid                = task.GUID?.ToString(),
            Name                = task.Name ?? "",
            CalendarUid         = task.Calendar?.UniqueID ?? -1,
            Start               = FmtDateTime(start),
            Finish              = FmtDateTime(finish),
            DurationHours       = DurationToHours(task.Duration),
            IsSummary           = task.Summary,
            IsMilestone         = task.Milestone,
            OutlineLevel        = task.OutlineLevel ?? 0,
            ParentUid           = task.ParentTask?.UniqueID ?? 0,
            Wbs                 = task.WBS,
            PercentComplete     = task.PercentageComplete,
            Cost                = task.Cost.HasValue ? (double?)Convert.ToDouble(task.Cost.Value) : null,
            Accrual             = task.FixedCostAccrual?.ToString(),
            Predecessors        = preds.Count > 0 ? preds : null,
            ResourceAssignments = assignments.Count > 0 ? assignments : null,
        });
    }

    if (eligibleCount > 0 && tasks.Count == 0)
        throw new ConversionException("task source=mpxj field=dates value=all-undated");

    var retainedUids = tasks.Select(t => t.Uid).ToHashSet();
    var omittedSet = omittedUids.Select(o => o.Uid).ToHashSet();
    foreach (var retained in tasks)
    {
        if (retained.ParentUid != 0)
        {
            if (omittedSet.Contains(retained.ParentUid))
                throw new ConversionException($"task uid={retained.Uid} source=mpxj field=parentUid value=omitted-{retained.ParentUid}");
            if (!retainedUids.Contains(retained.ParentUid))
                throw new ConversionException($"task uid={retained.Uid} source=mpxj field=parentUid value=missing-{retained.ParentUid}");
        }
        if (retained.Predecessors == null) continue;
        foreach (var pred in retained.Predecessors)
        {
            if (pred.Uid == 0) continue;
            if (omittedSet.Contains(pred.Uid))
                throw new ConversionException($"task uid={retained.Uid} source=mpxj field=predecessorUid value=omitted-{pred.Uid}");
            if (!retainedUids.Contains(pred.Uid))
                throw new ConversionException($"task uid={retained.Uid} source=mpxj field=predecessorUid value=missing-{pred.Uid}");
        }
    }

    var doc = new RootDto
    {
        SchemaVersion = 1,
        Source = new SourceDto
        {
            Tool         = "Virtuart4DConvert",
            Version      = converterVersion ?? "unknown",
            MpxjVersion  = mpxjAssembly.GetName().Version?.ToString() ?? "unknown",
            OriginalFile = Path.GetFileName(inputPath),
        },
        Currency  = new CurrencyDto { Symbol = props?.CurrencySymbol ?? "", Code = props?.CurrencyCode ?? "" },
        DefaultCalendarUid = project.ProjectProperties.DefaultCalendarUniqueID ?? -1,
        Calendars = calendars,
        Tasks     = tasks,
        Resources = resources,
    };

    var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
    {
        WriteIndented  = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });
    File.WriteAllText(outputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    if (omittedUids.Count > 0)
    {
        var sorted = omittedUids.OrderBy(u => u.Uid).ToList();
        var sample = sorted.Take(10)
            .Select(u => $"{u.Uid}:{u.Name.Replace('\n', ' ').Replace('\r', ' ')}");
        var more = sorted.Count > 10 ? $", and {sorted.Count - 10} more" : "";
        Console.Error.WriteLine($"Warning: omitted {sorted.Count} task(s) without start and finish: {string.Join(", ", sample)}{more}");
    }
    return 0;
}
catch (ConversionException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex}");
    return 1;
}

// ── helpers ──────────────────────────────────────────────────────────────────

static string? StableVersion(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    var stable = value.Split('+', 2)[0];
    var parts = stable.Split('.');
    return parts.Length == 3 && parts.All(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        ? stable
        : null;
}

static string? FmtDateTime(DateTime? dt) =>
    dt.HasValue ? dt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

static string[] FmtRange(TimeOnly start, TimeOnly end)
{
    var endText = start == TimeOnly.MinValue && end == TimeOnly.MinValue
        ? "24:00:00"
        : end.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    return [start.ToString("HH:mm:ss", CultureInfo.InvariantCulture), endText];
}

static double DurationToHours(Duration? d)
{
    if (d == null) return 0;
    var val = d.DurationValue;
    return d.Units switch
    {
        TimeUnit.Minutes        => val / 60.0,
        TimeUnit.Hours          => val,
        TimeUnit.Days           => val * 8.0,
        TimeUnit.Weeks          => val * 40.0,
        TimeUnit.Months         => val * 160.0,
        TimeUnit.ElapsedMinutes => val / 60.0,
        TimeUnit.ElapsedHours   => val,
        TimeUnit.ElapsedDays    => val * 24.0,
        TimeUnit.ElapsedWeeks   => val * 168.0,
        _ => val,
    };
}

static string CalendarError(ProjectCalendar cal, string field, string value) =>
    $"calendar uid={cal.UniqueID?.ToString(CultureInfo.InvariantCulture) ?? "missing"} source=mpxj field={field} value={value} (name={cal.Name ?? ""})";

static bool TryBuildWorkWeek(ProjectCalendar cal, out List<WorkDayDto> result, out string error)
{
    // MPXJ.Net CalendarDayTypes/CalendarHours are Sunday-first: 0=Sun..6=Sat.
    string[] dayNames = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];
    int[] sourceIndexes = [1, 2, 3, 4, 5, 6, 0];
    result = [];
    error = string.Empty;

    var types = cal.CalendarDayTypes;
    var hours = cal.CalendarHours;
    if (types == null || hours == null || types.Length != 7 || hours.Length != 7)
    {
        error = CalendarError(cal, "CalendarDayTypes", "missing");
        return false;
    }

    for (int modelDay = 0; modelDay < 7; ++modelDay)
    {
        var sourceDay = sourceIndexes[modelDay];
        var dayType = types[sourceDay];
        var ranges = new List<string[]>();
        bool defined;

        if (dayType == null || dayType == DayType.Default)
        {
            defined = false;
        }
        else if (dayType == DayType.NonWorking)
        {
            defined = true;
        }
        else if (dayType == DayType.Working)
        {
            defined = true;
            var dayHours = hours[sourceDay];
            if (dayHours == null)
            {
                error = CalendarError(cal, "CalendarHours", $"missing day={dayNames[modelDay]}");
                return false;
            }
            foreach (var range in dayHours)
            {
                if (range == null || range.Start == null || range.End == null)
                {
                    error = CalendarError(cal, "TimeOnlyRange", $"unreadable day={dayNames[modelDay]}");
                    return false;
                }
                if (range.End.Value <= range.Start.Value && range.End.Value != TimeOnly.MinValue)
                {
                    error = CalendarError(cal, "TimeOnlyRange", $"invalid day={dayNames[modelDay]}");
                    return false;
                }
                ranges.Add(FmtRange(range.Start.Value, range.End.Value));
            }
            if (ranges.Count == 0)
            {
                error = CalendarError(cal, "CalendarHours", $"empty day={dayNames[modelDay]}");
                return false;
            }
        }
        else
        {
            error = CalendarError(cal, "CalendarDayTypes", $"unreadable day={dayNames[modelDay]}");
            return false;
        }

        result.Add(new WorkDayDto { Day = dayNames[modelDay], Defined = defined, Ranges = ranges });
    }
    return true;
}

static bool TryBuildExceptions(ProjectCalendar cal, out List<CalendarExceptionDto> result, out string error)
{
    result = [];
    error = string.Empty;
    if (cal.CalendarExceptions == null)
    {
        return true;
    }

    foreach (var exception in cal.CalendarExceptions)
    {
        if (exception == null)
        {
            error = CalendarError(cal, "CalendarException", "unreadable");
            return false;
        }
        if (exception.FromDate == null || exception.ToDate == null)
        {
            error = CalendarError(cal, "CalendarException", "missing-date");
            return false;
        }
        var recurring = exception.Recurring;
        if (recurring != null)
        {
            bool bRecurringValid;
            try
            {
                bRecurringValid = recurring.Valid;
            }
            catch
            {
                error = CalendarError(cal, "Exception.Type", $"invalid exception={exception.Name ?? ""}");
                return false;
            }
            if (!bRecurringValid)
            {
                error = CalendarError(cal, "Exception.Type", $"invalid exception={exception.Name ?? ""}");
                return false;
            }
            if (recurring.RecurrenceType == null || recurring.RecurrenceType != MPXJ.Net.RecurrenceType.Yearly)
            {
                error = CalendarError(cal, "Exception.Type", recurring.RecurrenceType?.ToString() ?? "missing");
                return false;
            }
            if (exception.ExpandedExceptions == null || exception.ExpandedExceptions.Count == 0)
            {
                error = CalendarError(cal, "CalendarException", $"unexpanded exception={exception.Name ?? ""}");
                return false;
            }
            foreach (var expanded in exception.ExpandedExceptions)
            {
                if (!TryAddException(cal, exception.Name ?? "", expanded, recurring.Relative ? 3 : 2, result, out error))
                    return false;
            }
        }
        else if (!TryAddException(cal, exception.Name ?? "", exception, 9, result, out error))
        {
            return false;
        }
    }
    return true;
}

static bool TryAppendWorkWeekExceptions(ProjectCalendar cal, List<CalendarExceptionDto> emitted, out string error)
{
    const long MaxWorkWeekExpansionDays = 100000;
    error = string.Empty;
    var weeks = cal.WorkWeeks;
    if (weeks == null || weeks.Count == 0) return true;

    long totalDays = 0;
    bool allDefault = true;
    foreach (var week in weeks)
    {
        if (week == null) { error = CalendarError(cal, "WorkWeeks", "unreadable"); return false; }
        var range = week.DateRange;
        if (range == null || range.Start == null || range.End == null || range.Start.Value > range.End.Value)
        {
            error = CalendarError(cal, "WorkWeeks", "invalid-range");
            return false;
        }
        totalDays += (range.End.Value.DayNumber - range.Start.Value.DayNumber) + 1;
        foreach (var dow in Enum.GetValues<DayOfWeek>())
        {
            var dayType = week.GetCalendarDayType(dow);
            if (dayType == null || dayType == DayType.Default) continue;
            allDefault = false;
            var hours = week.GetCalendarHours(dow);
            if (dayType == DayType.Working && (hours == null || hours.Count == 0))
            { error = CalendarError(cal, "WorkWeeks", $"working-without-hours day={dow}"); return false; }
        }
    }
    if (totalDays > MaxWorkWeekExpansionDays)
    {
        error = CalendarError(cal, "WorkWeeks", $"expansion-limit days={totalDays} max={MaxWorkWeekExpansionDays}");
        return false;
    }
    if (allDefault) return true;

    var covered = emitted
        .Select(e => (From: DateOnly.ParseExact(e.From, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                      To:   DateOnly.ParseExact(e.To,   "yyyy-MM-dd", CultureInfo.InvariantCulture)))
        .ToList();
    var seen = new HashSet<DateOnly>();
    long emittedDays = 0;
    foreach (var derived in cal.ExpandedCalendarExceptionsWithWorkWeeks)
    {
        if (derived == null || derived.FromDate == null || derived.ToDate == null)
        { error = CalendarError(cal, "WorkWeeks", "derived-unreadable-date"); return false; }
        var ranges = new List<string[]>();
        foreach (var r in derived)
        {
            if (r == null || r.Start == null || r.End == null)
            { error = CalendarError(cal, "WorkWeeks", $"derived-unreadable-range name={derived.Name ?? ""}"); return false; }
            ranges.Add(FmtRange(r.Start.Value, r.End.Value));
        }
        var from = derived.FromDate.Value;
        var to   = derived.ToDate.Value;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (++emittedDays > MaxWorkWeekExpansionDays)
            { error = CalendarError(cal, "WorkWeeks", $"expansion-limit days={emittedDays} max={MaxWorkWeekExpansionDays}"); return false; }
            if (!seen.Add(date)) continue;
            if (covered.Any(c => date >= c.From && date <= c.To)) continue;
            emitted.Add(new CalendarExceptionDto
            {
                Name = derived.Name ?? "",
                From = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To   = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Working = ranges.Count > 0,
                Ranges = ranges,
                RecurrenceType = 9,
            });
            if (date == DateOnly.MaxValue) break;
        }
    }
    return true;
}

static bool TryAddException(ProjectCalendar cal, string name, ProjectCalendarException exception,
    int recurrenceType, List<CalendarExceptionDto> result, out string error)
{
    error = string.Empty;
    if (exception == null || exception.FromDate == null || exception.ToDate == null)
    {
        error = CalendarError(cal, "CalendarException", "unreadable-date");
        return false;
    }
    if (exception.FromDate.Value > exception.ToDate.Value)
    {
        error = CalendarError(cal, "CalendarException", $"inverted exception={name}");
        return false;
    }
    var ranges = new List<string[]>();
    foreach (var range in exception)
    {
        if (range == null || range.Start == null || range.End == null)
        {
            error = CalendarError(cal, "TimeOnlyRange", $"unreadable exception={name}");
            return false;
        }
        ranges.Add([
            range.Start.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            range.End.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)]);
    }
    result.Add(new CalendarExceptionDto
    {
        Name = name,
        From = exception.FromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        To = exception.ToDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Working = ranges.Count > 0,
        Ranges = ranges,
        RecurrenceType = recurrenceType,
    });
    return true;
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

record RootDto
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("source")]        public SourceDto Source { get; init; } = null!;
    [JsonPropertyName("currency")]      public CurrencyDto Currency { get; init; } = null!;
    [JsonPropertyName("defaultCalendarUid")] public int DefaultCalendarUid { get; init; }
    [JsonPropertyName("calendars")]     public List<CalendarDto> Calendars { get; init; } = [];
    [JsonPropertyName("tasks")]         public List<TaskDto> Tasks { get; init; } = [];
    [JsonPropertyName("resources")]     public List<ResourceDto> Resources { get; init; } = [];
}

record SourceDto
{
    [JsonPropertyName("tool")]         public string Tool { get; init; } = "";
    [JsonPropertyName("version")]      public string Version { get; init; } = "";
    [JsonPropertyName("mpxjVersion")]  public string MpxjVersion { get; init; } = "";
    [JsonPropertyName("originalFile")] public string OriginalFile { get; init; } = "";
}

record CurrencyDto
{
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";
    [JsonPropertyName("code")]   public string Code { get; init; } = "";
}

record CalendarDto
{
    [JsonPropertyName("uid")]             public int Uid { get; init; }
    [JsonPropertyName("name")]            public string Name { get; init; } = "";
    [JsonPropertyName("isBase")]          public bool IsBase { get; init; }
    [JsonPropertyName("baseCalendarUid")] public int BaseCalendarUid { get; init; }
    [JsonPropertyName("workWeek")]        public List<WorkDayDto> WorkWeek { get; init; } = [];
    [JsonPropertyName("exceptions")]      public List<CalendarExceptionDto> Exceptions { get; init; } = [];
}

record WorkDayDto
{
    [JsonPropertyName("day")]     public string Day { get; init; } = "";
    [JsonPropertyName("defined")] public bool Defined { get; init; }
    [JsonPropertyName("ranges")]  public List<string[]> Ranges { get; init; } = [];
}

record CalendarExceptionDto
{
    [JsonPropertyName("name")]           public string Name { get; init; } = "";
    [JsonPropertyName("from")]           public string From { get; init; } = "";
    [JsonPropertyName("to")]             public string To { get; init; } = "";
    [JsonPropertyName("working")]        public bool Working { get; init; }
    [JsonPropertyName("ranges")]         public List<string[]>? Ranges { get; init; }
    [JsonPropertyName("recurrenceType")] public int? RecurrenceType { get; init; }
}

record TaskDto
{
    [JsonPropertyName("uid")]                 public int Uid { get; init; }
    [JsonPropertyName("guid")]                public string? Guid { get; init; }
    [JsonPropertyName("name")]                public string Name { get; init; } = "";
    [JsonPropertyName("calendarUid")]         public int CalendarUid { get; init; }
    [JsonPropertyName("start")]               public string? Start { get; init; }
    [JsonPropertyName("finish")]              public string? Finish { get; init; }
    [JsonPropertyName("durationHours")]       public double DurationHours { get; init; }
    [JsonPropertyName("isSummary")]           public bool IsSummary { get; init; }
    [JsonPropertyName("isMilestone")]         public bool IsMilestone { get; init; }
    [JsonPropertyName("outlineLevel")]        public int OutlineLevel { get; init; }
    [JsonPropertyName("parentUid")]           public int ParentUid { get; init; }
    [JsonPropertyName("wbs")]                 public string? Wbs { get; init; }
    [JsonPropertyName("percentComplete")]     public double? PercentComplete { get; init; }
    [JsonPropertyName("cost")]                public double? Cost { get; init; }
    [JsonPropertyName("accrual")]             public string? Accrual { get; init; }
    [JsonPropertyName("predecessors")]        public List<PredecessorDto>? Predecessors { get; init; }
    [JsonPropertyName("resourceAssignments")] public List<AssignmentDto>? ResourceAssignments { get; init; }
}

record PredecessorDto
{
    [JsonPropertyName("uid")]      public int Uid { get; init; }
    [JsonPropertyName("type")]     public string Type { get; init; } = "FS";
    [JsonPropertyName("lagHours")] public double LagHours { get; init; }
}

record AssignmentDto
{
    [JsonPropertyName("resourceUid")] public int ResourceUid { get; init; }
    [JsonPropertyName("units")]       public double Units { get; init; }
    [JsonPropertyName("workHours")]   public double WorkHours { get; init; }
}

record ResourceDto
{
    [JsonPropertyName("uid")]         public int Uid { get; init; }
    [JsonPropertyName("name")]        public string Name { get; init; } = "";
    [JsonPropertyName("type")]        public string Type { get; init; } = "Work";
    [JsonPropertyName("stdRate")]     public double? StdRate { get; init; }
    [JsonPropertyName("calendarUid")] public int CalendarUid { get; init; }
}

sealed class ConversionException : Exception
{
    public ConversionException(string message) : base(message) { }
}
