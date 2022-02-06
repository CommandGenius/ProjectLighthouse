#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Serialization;
using LBPUnion.ProjectLighthouse.Types;
using LBPUnion.ProjectLighthouse.Types.Activity;
using LBPUnion.ProjectLighthouse.Types.Activity.Events;
using LBPUnion.ProjectLighthouse.Types.Levels;
using LBPUnion.ProjectLighthouse.Types.News;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IOFile = System.IO.File;

namespace LBPUnion.ProjectLighthouse.Controllers.GameApi;

[ApiController]
[Route("LITTLEBIGPLANETPS3_XML/")]
[Produces("text/plain")]
public class RecentActivityController : ControllerBase
{
    private readonly Database database;

    public RecentActivityController(Database database)
    {
        this.database = database;
    }

    private async Task<List<LevelGroup>> getActivityInvolvingLevels(long timestamp, long endTimestamp)
    {
        List<ActivityEntry> activityEntries = await this.database.ActivityLog.Include
                (e => e.User)
            .Where(l => l.Timestamp < timestamp)
            .Where(l => l.Timestamp >= endTimestamp)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        List<LevelGroup> levelGroups = new();

        // Scores
        foreach (ActivityEntry entry in activityEntries.Where(entry => entry.Type == EventType.NewScore))
        {
            Score? score = await this.database.Scores.FirstOrDefaultAsync(s => s.ScoreId == entry.RelatedId);
            if (score == null) continue;

            LevelGroup levelGroup = levelGroups.GetOrCreateLevelGroup(score.SlotId);
            UserGroup userGroup = levelGroup.GetOrCreateUserGroup(entry.User);

            userGroup.Events.Add
            (
                new NewScoreEvent
                {
                    Timestamp = entry.Timestamp,
                    Score = score,
                    User = entry.User,
                }
            );
        }

        // Levels

        foreach (ActivityEntry entry in activityEntries.Where(entry => entry.Type == EventType.PublishLevel))
        {
            Slot? slot = await this.database.Slots.FirstOrDefaultAsync(s => s.SlotId == entry.RelatedId);
            if (slot == null) continue;

            LevelGroup levelGroup = levelGroups.GetOrCreateLevelGroup(entry.RelatedId);
            UserGroup userGroup = levelGroup.GetOrCreateUserGroup(entry.User);

            userGroup.Events.Add
            (
                new PublishLevelEvent
                {
                    Timestamp = entry.Timestamp,
                    Slot = slot,
                    User = entry.User,
                }
            );
        }

        return levelGroups;
    }

    [HttpGet("stream")]
    public async Task<IActionResult> GetStream([FromQuery] long timestamp = 0, [FromQuery] bool excludeNews = false)
    {
        User? user = await this.database.UserFromGameRequest(this.Request);
        if (user == null) return this.StatusCode(403, "");

//        return this.Ok(IOFile.ReadAllText("/home/jvyden/.config/JetBrains/Rider2021.3/scratches/recent-activity.xml"));
        if (timestamp == 0) timestamp = TimestampHelper.TimestampMillis;
        long endTimestamp = timestamp - 86400000 * 2; // 2 days

        List<LevelGroup> levelGroups = await this.getActivityInvolvingLevels(timestamp, endTimestamp);

        string groups = levelGroups.Aggregate(string.Empty, (current, levelGroup) => current + levelGroup.Serialize());

        string news = string.Empty;
        if (!excludeNews)
        {
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (NewsEntry newsEntry in await this.database.NewsEntries.ToListAsync())
            {
                news += LbpSerializer.StringElement("item", newsEntry.Serialize());

                string newsGroup = LbpSerializer.StringElement("timestamp", newsEntry.Timestamp) + LbpSerializer.StringElement("news_id", newsEntry.NewsId);

                newsGroup += LbpSerializer.StringElement("events", LbpSerializer.TaggedStringElement("event", newsGroup, "type", "news_post"));

                groups += LbpSerializer.TaggedStringElement("group", newsGroup, "type", "news");
            }
        }
        string serialized = LbpSerializer.StringElement
        (
            "stream",
            LbpSerializer.StringElement("start_timestamp", timestamp) + // Start timestamp is current timestamp minus 1 week
            LbpSerializer.StringElement("end_timestamp", endTimestamp) +
            LbpSerializer.StringElement("groups", groups) +
            LbpSerializer.StringElement("news", news)
        );

        #if DEBUG
        await IOFile.WriteAllTextAsync("recent-activity.xml", serialized);
        #endif
        return this.Ok(serialized);
    }
}