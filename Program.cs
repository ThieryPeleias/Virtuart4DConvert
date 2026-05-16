using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MPXJ.Net;

const string ToolVersion = "0.1.0";

if (args.Length == 1 && args[0] == "--version")
{
    Console.WriteLine(ToolVersion);
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
        var parent = cal.Parent;
        calendars.Add(new CalendarDto
        {
            Uid             = cal.UniqueID ?? 0,
            Name            = cal.Name ?? "",
            IsBase          = parent == null,
            BaseCalendarUid = parent?.UniqueID ?? -1,
            WorkWeek        = BuildWorkWeek(cal),
            Exceptions      = BuildExceptions(cal),
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
    foreach (var task in project.Tasks)
    {
        if (task == null) continue;
        var uid = task.UniqueID ?? 0;
        if (uid == 0) continue;

        var preds = new List<PredecessorDto>();
        if (task.Predecessors != null)
        {
            foreach (var rel in task.Predecessors)
            {
                if (rel?.PredecessorTask == null) continue;
                preds.Add(new PredecessorDto
                {
                    Uid      = rel.PredecessorTask.UniqueID ?? 0,
                    Type     = rel.Type?.ToString() ?? "FS",
                    LagHours = DurationToHours(rel.Lag),
                });
            }
        }

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
            Uid                 = uid,
            Guid                = task.GUID?.ToString(),
            Name                = task.Name ?? "",
            CalendarUid         = task.Calendar?.UniqueID ?? -1,
            Start               = FmtDateTime(task.Start),
            Finish              = FmtDateTime(task.Finish),
            DurationHours       = DurationToHours(task.Duration),
            IsSummary           = task.Summary,
            IsMilestone         = task.Milestone,
            OutlineLevel        = task.OutlineLevel ?? 0,
            ParentUid           = task.ParentTask?.UniqueID ?? -1,
            Wbs                 = task.WBS,
            PercentComplete     = task.PercentageComplete,
            Cost                = task.Cost.HasValue ? (double?)Convert.ToDouble(task.Cost.Value) : null,
            Accrual             = task.FixedCostAccrual?.ToString(),
            Predecessors        = preds.Count > 0 ? preds : null,
            ResourceAssignments = assignments.Count > 0 ? assignments : null,
        });
    }

    var mpxjVer = typeof(UniversalProjectReader).Assembly.GetName().Version?.ToString() ?? "unknown";
    var doc = new RootDto
    {
        SchemaVersion = 1,
        Source = new SourceDto
        {
            Tool         = "Virtuart4DConvert",
            Version      = ToolVersion,
            MpxjVersion  = mpxjVer,
            OriginalFile = Path.GetFileName(inputPath),
        },
        Currency  = new CurrencyDto { Symbol = props?.CurrencySymbol ?? "", Code = props?.CurrencyCode ?? "" },
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
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex}");
    return 1;
}

// ── helpers ──────────────────────────────────────────────────────────────────

static string? FmtDateTime(DateTime? dt) =>
    dt.HasValue ? dt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

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

static List<WorkDayDto> BuildWorkWeek(ProjectCalendar cal)
{
    // CalendarDayTypes: 0=Sun,1=Mon,2=Tue,3=Wed,4=Thu,5=Fri,6=Sat
    string[] dayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
    var result = new List<WorkDayDto>();
    try
    {
        var types = cal.CalendarDayTypes;
        var hours = cal.CalendarHours;
        if (types == null || hours == null) return result;

        for (int i = 1; i <= 5; i++) // Mon–Fri
        {
            var dayType = types[i];
            if (dayType == DayType.NonWorking) continue;

            var dayHours = hours[i]; // ProjectCalendarHours : IList<TimeOnlyRange>
            var ranges = new List<string[]>();
            if (dayHours != null)
            {
                foreach (TimeOnlyRange range in dayHours)
                {
                    if (range?.Start == null || range.End == null) continue;
                    ranges.Add([range.Start.Value.ToString("HH:mm"),
                                range.End.Value.ToString("HH:mm")]);
                }
            }
            if (ranges.Count == 0)
                ranges = [["08:00", "12:00"], ["13:00", "17:00"]];
            result.Add(new WorkDayDto { Day = dayNames[i], Ranges = ranges });
        }
    }
    catch { /* skip if calendar API differs from expected */ }
    return result;
}

static List<CalendarExceptionDto> BuildExceptions(ProjectCalendar cal)
{
    var result = new List<CalendarExceptionDto>();
    try
    {
        if (cal.CalendarExceptions == null) return result;
        foreach (var ex in cal.CalendarExceptions)
        {
            if (ex?.FromDate == null || ex.ToDate == null) continue;
            result.Add(new CalendarExceptionDto
            {
                From    = ex.FromDate.Value.ToString("yyyy-MM-dd"),
                To      = ex.ToDate.Value.ToString("yyyy-MM-dd"),
                Working = ex.Working,
            });
        }
    }
    catch { /* skip if API differs */ }
    return result;
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

record RootDto
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("source")]        public SourceDto Source { get; init; } = null!;
    [JsonPropertyName("currency")]      public CurrencyDto Currency { get; init; } = null!;
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
    [JsonPropertyName("day")]    public string Day { get; init; } = "";
    [JsonPropertyName("ranges")] public List<string[]> Ranges { get; init; } = [];
}

record CalendarExceptionDto
{
    [JsonPropertyName("from")]    public string From { get; init; } = "";
    [JsonPropertyName("to")]      public string To { get; init; } = "";
    [JsonPropertyName("working")] public bool Working { get; init; }
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
