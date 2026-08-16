using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notes.Api.Infrastructure;

/// <summary>
/// 把所有 DateTime 统一当作 UTC 时间输出，并在末尾追加 "Z" 后缀。
///
/// 背景：EF Core + Pomelo MySQL 读回的 DateTime 默认 Kind = Unspecified，
/// System.Text.Json 默认序列化时不会追加任何时区标识，输出如 "2026-08-16T07:30:00"。
/// 浏览器 new Date() 看到无后缀字符串会当作本地时间解析，导致 UTC 时间被误显示成本地时间，
/// 在东八区表现为"少 8 小时"。
///
/// 本 Converter 把 Kind 强制设为 Utc 后再输出，从而得到 "2026-08-16T07:30:00Z"。
/// 前端 new Date() 按 UTC 解析，再调用 getHours()/getMinutes() 等本地方法会自动转换为本地时间显示。
/// </summary>
public class JsonDateTimeUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 反序列化：直接用 DateTime.Parse；保持宽松兼容历史数据
        var dt = reader.GetDateTime();
        return dt;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // 序列化：把 Kind 强制为 Utc，System.Text.Json 会自动追加 "Z"
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc);
    }
}
