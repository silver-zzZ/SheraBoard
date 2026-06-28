using SheraBoard.Core.Models;
using System.Windows.Media;

namespace SheraBoard.App.ViewModels;

public sealed class ClipboardItemViewModel
{
    public ClipboardItemViewModel(ClipboardItemRecord record)
    {
        Record = record;
    }

    public ClipboardItemRecord Record { get; }

    public string TimeText => Record.CapturedAt.ToLocalTime().ToString("HH:mm:ss");

    public string DateText => Record.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd");

    public string GroupText => Record.Pinned ? "固定" : FormatDateGroup(Record.CapturedAt.ToLocalTime().Date);

    public string KindText => Record.Kind switch
    {
        ClipboardKind.Text => "文字",
        ClipboardKind.RichText when IsTable => "表格",
        ClipboardKind.RichText => "富文本",
        ClipboardKind.Image => "图片",
        ClipboardKind.FileList => "文件",
        _ => Record.Kind.ToString()
    };

    public System.Windows.Visibility CopyImageVisibility =>
        Record.Kind == ClipboardKind.Image || Record.Formats.Contains(ClipboardFormatNames.Png)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility CopyOriginalVisibility =>
        Record.Kind == ClipboardKind.Image
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public System.Windows.Visibility CopyPlainTextVisibility =>
        Record.Kind == ClipboardKind.Image
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public System.Windows.Media.Brush KindForeground => CreateBrush(Record.Kind switch
    {
        ClipboardKind.Text => "#2563EB",
        ClipboardKind.RichText when IsTable => "#059669",
        ClipboardKind.RichText => "#7C3AED",
        ClipboardKind.Image => "#DB2777",
        ClipboardKind.FileList => "#D97706",
        _ => "#64748B"
    });

    public System.Windows.Media.Brush KindBackground => CreateBrush(Record.Kind switch
    {
        ClipboardKind.Text => "#EAF2FF",
        ClipboardKind.RichText when IsTable => "#DFF8EC",
        ClipboardKind.RichText => "#F3E8FF",
        ClipboardKind.Image => "#FCE7F3",
        ClipboardKind.FileList => "#FEF3C7",
        _ => "#F1F5F9"
    });

    public System.Windows.Media.Brush KindBorder => CreateBrush(Record.Kind switch
    {
        ClipboardKind.Text => "#BFDBFE",
        ClipboardKind.RichText when IsTable => "#A7F3D0",
        ClipboardKind.RichText => "#DDD6FE",
        ClipboardKind.Image => "#FBCFE8",
        ClipboardKind.FileList => "#FDE68A",
        _ => "#CBD5E1"
    });

    public string PreviewText => Record.PreviewText;

    public string SourceApp => string.IsNullOrWhiteSpace(Record.SourceApp) ? "-" : Record.SourceApp;

    public string SizeText => FormatSize(Record.SizeBytes);

    public string PinText => Record.Pinned ? "固定" : string.Empty;

    public string FavoriteText => Record.Favorite ? "收藏" : string.Empty;

    public string StateText => string.Join(" ", new[] { PinText, FavoriteText }.Where(text => !string.IsNullOrWhiteSpace(text)));

    private bool IsTable => Record.Formats.Contains(ClipboardFormatNames.Table);

    private static string FormatDateGroup(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today)
        {
            return "今天";
        }

        if (date == today.AddDays(-1))
        {
            return "昨天";
        }

        return $"{date:M月d日} {FormatWeekday(date.DayOfWeek)}";
    }

    private static string FormatWeekday(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "周一",
            DayOfWeek.Tuesday => "周二",
            DayOfWeek.Wednesday => "周三",
            DayOfWeek.Thursday => "周四",
            DayOfWeek.Friday => "周五",
            DayOfWeek.Saturday => "周六",
            DayOfWeek.Sunday => "周日",
            _ => string.Empty
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)Math.Max(0, bytes);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.0} {units[unit]}";
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
