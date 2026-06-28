using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Grabsy.Services;

/// <summary>Loopback HTTP listener for the browser userscript bridge.
/// Binds 127.0.0.1 only (no admin urlacl needed) so remote hosts can't reach it.</summary>
public sealed class BridgeServer
{
    public const int Port = 47821;

    public readonly record struct JobStatus(int Progress, string State, string Status);

    private readonly Func<string, string, string, string> _start;   // url, mode, quality -> id
    private readonly Func<string, JobStatus?> _status;              // id -> status
    private readonly Action<string> _cancel;                        // id -> cancel
    private HttpListener? _listener;

    public BridgeServer(Func<string, string, string, string> start, Func<string, JobStatus?> status, Action<string> cancel)
    {
        _start = start;
        _status = status;
        _cancel = cancel;
    }

    public void Start()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }
        catch { _listener = null; }
    }

    public void Stop()
    {
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    private async Task LoopAsync()
    {
        var l = _listener;
        while (l != null && l.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await l.GetContextAsync(); }
            catch { break; }
            try { Handle(ctx); } catch { }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var resp = ctx.Response;
        resp.AddHeader("Access-Control-Allow-Origin", "*");
        var q = ctx.Request.QueryString;
        var path = ctx.Request.Url?.AbsolutePath ?? "/";

        switch (path)
        {
            case "/ping":
                Write(resp, "{\"app\":\"grabsy\"}");
                return;

            case "/download":
            {
                var url = q["url"];
                if (string.IsNullOrWhiteSpace(url) ||
                    !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    resp.StatusCode = 400;
                    Write(resp, "{\"ok\":false}");
                    return;
                }
                var mode = q["mode"] ?? "videoaudio";
                var quality = q["quality"] ?? "best";
                var id = _start(url, mode, quality);
                Write(resp, $"{{\"id\":\"{Esc(id)}\"}}");
                return;
            }

            case "/status":
            {
                var id = q["id"] ?? "";
                var s = _status(id);
                if (s == null) { resp.StatusCode = 404; Write(resp, "{\"state\":\"unknown\"}"); return; }
                var v = s.Value;
                Write(resp, $"{{\"progress\":{v.Progress},\"state\":\"{Esc(v.State)}\",\"status\":\"{Esc(v.Status)}\"}}");
                return;
            }

            case "/cancel":
            {
                var id = q["id"] ?? "";
                _cancel(id);
                Write(resp, "{\"ok\":true}");
                return;
            }

            default:
                resp.StatusCode = 404;
                try { resp.Close(); } catch { }
                return;
        }
    }

    private static string Esc(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");

    private static void Write(HttpListenerResponse resp, string json)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }
        catch { }
    }
}
