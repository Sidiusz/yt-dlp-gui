using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Grabsy.Models;

/// <summary>Result of `yt-dlp -J` probe: metadata + distilled quality options.</summary>
public sealed class VideoInfo
{
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Uploader { get; init; }
    public string? ThumbnailUrl { get; init; }
    public double Duration { get; init; }          // seconds; 0 = unknown/live
    public bool IsPlaylist { get; init; }
    public int PlaylistCount { get; init; }
    public List<int> AvailableHeights { get; init; } = new(); // distinct, descending
    public List<MediaTrack> Subtitles { get; init; } = new();  // manual subtitle languages
    public List<MediaTrack> AudioTracks { get; init; } = new(); // distinct audio languages (>1 = selectable)

    public bool HasMultipleAudio => AudioTracks.Count > 1;
    public bool HasSubtitles => Subtitles.Count > 0;

    public string DurationText
    {
        get
        {
            if (Duration <= 0) return "—";
            var t = TimeSpan.FromSeconds(Duration);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }
    }
}

/// <summary>A selectable subtitle or audio language.</summary>
public sealed class MediaTrack
{
    public string Code { get; init; } = "";   // language code, e.g. "en"
    public string Display { get; init; } = ""; // human label, e.g. "English (en)"
    public bool Selected { get; set; }
}

public enum JobState { Queued, Running, Completed, Failed, Canceled }

/// <summary>One download. Observable so the queue list updates live.</summary>
public sealed class DownloadJob : INotifyPropertyChanged
{
    public string Url { get; init; } = "";

    private string _title = "";
    public string Title { get => _title; set => Set(ref _title, value); }

    private string? _thumbnailUrl;
    public string? ThumbnailUrl { get => _thumbnailUrl; set => Set(ref _thumbnailUrl, value); }

    private double _progress;          // 0..1
    public double Progress { get => _progress; set { Set(ref _progress, value); OnChanged(nameof(ProgressPercent)); } }
    public double ProgressPercent => _progress * 100.0;

    private bool _indeterminate;
    public bool Indeterminate { get => _indeterminate; set => Set(ref _indeterminate, value); }

    private string _status = "Queued";
    public string Status { get => _status; set => Set(ref _status, value); }

    // Filled on completion: "45.2 MB · 27 Jun 18:30" (empty while running).
    private string _sizeDate = "";
    public string SizeDate { get => _sizeDate; set { Set(ref _sizeDate, value); OnChanged(nameof(HasSizeDate)); } }
    public bool HasSizeDate => !string.IsNullOrEmpty(_sizeDate);

    private JobState _state = JobState.Queued;
    public JobState State
    {
        get => _state;
        set { Set(ref _state, value); OnChanged(nameof(IsRunning)); OnChanged(nameof(IsDone)); OnChanged(nameof(CanOpen)); }
    }

    public bool IsRunning => _state == JobState.Running || _state == JobState.Queued;
    public bool IsDone => _state == JobState.Completed;
    public bool CanOpen => _state == JobState.Completed && !string.IsNullOrEmpty(OutputPath);

    public string? OutputPath { get; set; }

    // Chosen options snapshot
    public bool AudioOnly { get; init; }
    public string Format { get; init; } = "";        // human label e.g. "1080p MP4"

    public System.Threading.CancellationTokenSource? Cts { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; OnChanged(n);
    }
}
