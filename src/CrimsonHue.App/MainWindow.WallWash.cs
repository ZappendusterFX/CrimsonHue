using System.Text.Json;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private bool wallPreview;
    private double wallPreviewYaw;
    private double wallPreviewElevation;

    private void SetWallPreview(JsonElement command)
    {
        if (streamTask != null) return;
        var key = command.GetProperty("key").GetString();
        if (key == "enabled")
        {
            wallPreview = command.GetProperty("value").GetBoolean();
            if (wallPreview) DemoCheck.IsChecked = true;
        }
        else
        {
            var value = command.GetProperty("value").GetDouble();
            if (!double.IsFinite(value) || Math.Abs(value) > (key == "yaw" ? 180 : 89))
                throw new CrimsonHueException("Invalid preview angle.");
            if (key == "yaw") wallPreviewYaw = value;
            else if (key == "elevation") wallPreviewElevation = value;
            else throw new CrimsonHueException("Unknown preview setting.");
        }
        previewMapper.Reset();
    }

    private IEnumerable<object> WallWasherSnapshot(EntertainmentArea? area)
    {
        if (area == null) yield break;
        foreach (var id in area.Channels.SelectMany(c => c.Members).Select(m => m.ServiceId).Distinct())
        {
            var washer = wallWash.GetOrCreate(area, id);
            var channels = area.Channels.Where(c => c.Members.Any(m => m.ServiceId == id)).ToArray();
            yield return new
            {
                serviceId = id, label = channels[0].Label, washer.Enabled, washer.Yaw, washer.HorizontalSpan,
                segments = washer.Segments.Select(s => new
                {
                    s.Index, s.YawOffset, s.Elevation, s.VerticalSpan,
                    channels = channels.Where(c => c.Members.Contains(new ChannelMember(id, s.Index))).Select(c => new { c.Id, canIdentify = c.Members.Count == 1 })
                })
            };
        }
    }

    private void SetWallWasher(JsonElement command)
    {
        if (identifying) throw new CrimsonHueException("Finish the segment test before changing its calibration.");
        var area = SelectedArea ?? Room.Area ?? throw new CrimsonHueException("Select an Entertainment area first.");
        var id = command.GetProperty("serviceId").GetString() ?? "";
        var current = wallWash.GetOrCreate(area, id);
        var key = command.GetProperty("key").GetString();
        var next = current;
        if (key == "enabled") next = current with { Enabled = command.GetProperty("value").GetBoolean() };
        else if (key == "reset")
        {
            Volatile.Write(ref wallWash, wallWash.Remove(area.Id, id)); previewMapper.Reset();
            TuningStatus.Text = "Imported Hue positions restored · saved on Save, Start or close"; return;
        }
        else
        {
            var value = command.GetProperty("value").GetDouble();
            if (key == "yaw") next = current with { Yaw = value };
            else if (key == "span") next = current with { HorizontalSpan = value };
            else
            {
                var index = command.GetProperty("index").GetInt32();
                var segment = current.Segments.Single(s => s.Index == index);
                var changed = key switch
                {
                    "offset" => segment with { YawOffset = value },
                    "elevation" => segment with { Elevation = value },
                    "verticalSpan" => segment with { VerticalSpan = value },
                    _ => throw new CrimsonHueException("Unknown wall washer setting.")
                };
                next = current with { Segments = current.Segments.Select(s => s.Index == index ? changed : s).ToArray() };
            }
        }
        Volatile.Write(ref wallWash, wallWash.Set(next));
        previewMapper.Reset();
        TuningStatus.Text = "Room calibration changed · saved on Save, Start or close";
    }

    private async void IdentifySegment(byte channelId)
    {
        // A dedicated explicit action, mutually exclusive with sync. Demo and the
        // synthetic harness cannot reach the physical calibration path.
        if (smoke || busy || streamTask != null || bridge == null || SelectedArea == null || DemoCheck.IsChecked == true) return;
        streamCancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        session = new StreamingSession(bridge);
        identifying = true;
        var activeSession = session; var area = SelectedArea;
        streamTask = Task.Run(() => activeSession.IdentifyAsync(area, channelId, streamCancel.Token));
        UpdateButtons();
        try { await streamTask; StreamStatus.Text = "Segment test ended · previous states restored when still owned"; }
        catch (Exception ex) { StreamStatus.Text = FriendlyError(ex); }
        finally
        {
            identifying = false; streamTask = null; session = null;
            streamCancel.Dispose(); streamCancel = null; previewMapper.Reset(); UpdateButtons();
        }
    }
}
