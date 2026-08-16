namespace Notes.Api.DTOs;

// ============ 时间线视图 DTO ============

public record TimelineDayDto(
    DateTime Date,
    string WeekdayLabel,
    List<NoteListItemDto> Notes,
    int NoteCount);

public record TimelineMonthDto(
    int Year,
    int Month,
    string MonthLabel,
    List<TimelineDayDto> Days,
    int NoteCount,
    bool IsCurrentMonth);

public record TimelineYearDto(
    int Year,
    List<TimelineMonthDto> Months,
    int NoteCount,
    bool IsCurrentYear);

public record TimelineResponseDto(
    int TotalNotes,
    List<TimelineYearDto> Years);

// 时间线筛选参数：FromQuery 直接按属性绑定，不用记录类型
public class TimelineQueryParams
{
    public List<int> CategoryIds { get; set; } = new();
    public List<int> TagIds { get; set; } = new();
    public string? Keyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

// ============ 笔记版本历史 DTO ============

public record NoteVersionListItemDto(
    int Id,
    string TitlePreview,
    string ContentPreview,
    int? CategoryId,
    string? CategoryName,
    List<string> Tags,
    DateTime CreatedAt,
    bool IsCurrent,
    string SnapshotHash);

public record NoteVersionDetailDto(
    int Id,
    string Title,
    string Content,
    int? CategoryId,
    string? CategoryName,
    List<int> TagIds,
    List<string> TagNames,
    DateTime CreatedAt,
    string SnapshotHash);
