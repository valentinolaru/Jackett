﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CsQuery;
using Jackett.Common.Models;
using Jackett.Common.Models.IndexerConfig;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils;
using Newtonsoft.Json.Linq;
using NLog;

namespace Jackett.Common.Indexers
{
    public class ImmortalSeed : BaseWebIndexer
    {
        private string BrowsePage { get { return SiteLink + "browse.php"; } }
        private string LoginUrl { get { return SiteLink + "takelogin.php"; } }
        private string QueryString { get { return "?do=search&keywords={0}&search_type=t_name&category=0&include_dead_torrents=no"; } }

        public override string[] LegacySiteLinks { get; protected set; } = new string[] {
            "http://immortalseed.me/",
        };

        private new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public ImmortalSeed(IIndexerConfigurationService configService, Utils.Clients.WebClient wc, Logger l, IProtectionService ps)
            : base(name: "ImmortalSeed",
                description: "ImmortalSeed (iS) is a Private Torrent Tracker for MOVIES / TV / GENERAL",
                link: "https://immortalseed.me/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                configService: configService,
                client: wc,
                logger: l,
                p: ps,
                configData: new ConfigurationDataBasicLogin())
        {
            Encoding = Encoding.UTF8;
            Language = "en-us";
            Type = "private";

            AddCategoryMapping(3, TorznabCatType.Other, "Nuked");
            AddCategoryMapping(32, TorznabCatType.TVAnime, "Anime");
            AddCategoryMapping(23, TorznabCatType.PC, "Apps");
            AddCategoryMapping(35, TorznabCatType.AudioAudiobook, "Audiobooks");
            AddCategoryMapping(31, TorznabCatType.TV, "Childrens/Cartoons");
            AddCategoryMapping(54, TorznabCatType.TVDocumentary, "Documentary - HD");
            AddCategoryMapping(41, TorznabCatType.BooksComics, "Comics");
            AddCategoryMapping(25, TorznabCatType.PCGames, "Games");
            AddCategoryMapping(29, TorznabCatType.ConsoleXbox, "Games Xbox");
            AddCategoryMapping(27, TorznabCatType.PCGames, "Games-PC Rips");
            AddCategoryMapping(28, TorznabCatType.ConsolePS4, "Games-PSx");
            AddCategoryMapping(49, TorznabCatType.PCPhoneOther, "Mobile");
            AddCategoryMapping(59, TorznabCatType.MoviesUHD, "Movies-4k");
            AddCategoryMapping(60, TorznabCatType.MoviesForeign, "Non-English 4k Movies");
            AddCategoryMapping(16, TorznabCatType.MoviesHD, "Movies HD");
            AddCategoryMapping(18, TorznabCatType.MoviesForeign, "Movies HD Non-English");
            AddCategoryMapping(17, TorznabCatType.MoviesSD, "TS/CAM/PPV");
            AddCategoryMapping(34, TorznabCatType.MoviesForeign, "Movies Low Def Non-English");
            AddCategoryMapping(14, TorznabCatType.MoviesSD, "Movies-SD");
            AddCategoryMapping(33, TorznabCatType.MoviesForeign, "Movies SD Non-English");
            AddCategoryMapping(30, TorznabCatType.AudioOther, "Music");
            AddCategoryMapping(37, TorznabCatType.AudioLossless, "FLAC");
            AddCategoryMapping(36, TorznabCatType.AudioMP3, "MP3");
            AddCategoryMapping(39, TorznabCatType.AudioOther, "Music Other");
            AddCategoryMapping(38, TorznabCatType.AudioVideo, "Music Video");
            AddCategoryMapping(45, TorznabCatType.Other, "Other");
            AddCategoryMapping(7, TorznabCatType.TVSport, "Sports Tv");
            AddCategoryMapping(44, TorznabCatType.TVSport, "Sports Fitness-Instructional");
            AddCategoryMapping(58, TorznabCatType.TVSport, "Olympics");
            AddCategoryMapping(47, TorznabCatType.TVSD, "TV - 480p");
            AddCategoryMapping(8, TorznabCatType.TVHD, "TV - High Definition");
            AddCategoryMapping(48, TorznabCatType.TVSD, "TV - Standard Definition - x264");
            AddCategoryMapping(9, TorznabCatType.TVSD, "TV - Standard Definition - XviD");
            AddCategoryMapping(4, TorznabCatType.TVHD, "TV Season Packs - HD");
            AddCategoryMapping(6, TorznabCatType.TVSD, "TV Season Packs - SD");
            AddCategoryMapping(22, TorznabCatType.BooksEbook, "Ebooks");
            AddCategoryMapping(26, TorznabCatType.PCGames, "Games-PC ISO");
            AddCategoryMapping(46, TorznabCatType.BooksMagazines, "Magazines");
            AddCategoryMapping(50, TorznabCatType.PCPhoneIOS, "IOS");
            AddCategoryMapping(51, TorznabCatType.PCPhoneAndroid, "Android");
            AddCategoryMapping(52, TorznabCatType.PC0day, "Windows");
            AddCategoryMapping(53, TorznabCatType.TVDocumentary, "Documentary - SD");
            AddCategoryMapping(58, TorznabCatType.TVSport, "Olympics");
        }

        public override async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            LoadValuesFromJson(configJson);

            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value }
            };

            var response = await RequestLoginAndFollowRedirect(LoginUrl, pairs, null, true, null, LoginUrl);
            CQ resultDom = response.Content;

            await ConfigureIfOK(response.Cookies, response.Content.Contains("You have successfully logged in"), () =>
            {
                var errorMessage = response.Content;
                throw new ExceptionWithConfigData(errorMessage, configData);
            });

            return IndexerConfigurationStatus.RequiresTesting;
        }

        protected override async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var searchUrl = BrowsePage;

            if (!string.IsNullOrWhiteSpace(query.GetQueryString()))
            {
                searchUrl += string.Format(QueryString, WebUtility.UrlEncode(query.GetQueryString()));
            }

            var results = await RequestStringWithCookiesAndRetry(searchUrl);

            // Occasionally the cookies become invalid, login again if that happens
            if (results.Content.Contains("You do not have permission to access this page."))
            {
                await ApplyConfiguration(null);
                results = await RequestStringWithCookiesAndRetry(searchUrl);
            }

            try
            {
                CQ dom = results.Content;

                var rows = dom["#sortabletable tr:has(a[href*=\"details.php?id=\"])"];
                foreach (var row in rows)
                {
                    var release = new ReleaseInfo();
                    var qRow = row.Cq();

                    var qDetails = qRow.Find("div > a[href*=\"details.php?id=\"]"); // details link, release name get's shortened if it's to long
                    var qTitle = qRow.Find(".tooltip-content > div:eq(0)"); // use Title from tooltip
                    if (!qTitle.Any()) // fallback to Details link if there's no tooltip
                    {
                        qTitle = qDetails;
                    }
                    release.Title = qTitle.Text();

                    var qDesciption = qRow.Find(".tooltip-content > div");
                    if (qDesciption.Any())
                        release.Description = qDesciption.Get(1).InnerText.Trim();

                    var qLink = row.Cq().Find("a[href*=\"download.php\"]");
                    release.Link = new Uri(qLink.Attr("href"));
                    release.Guid = release.Link;
                    release.Comments = new Uri(qDetails.Attr("href"));

                    // 07-22-2015 11:08 AM
                    var dateString = qRow.Find("td:eq(1) div").Last().Get(0).LastChild.ToString().Trim();
                    release.PublishDate = DateTime.ParseExact(dateString, "MM-dd-yyyy hh:mm tt", CultureInfo.InvariantCulture);

                    var sizeStr = qRow.Find("td:eq(4)").Text().Trim();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(qRow.Find("td:eq(6)").Text().Trim());
                    release.Peers = ParseUtil.CoerceInt(qRow.Find("td:eq(7)").Text().Trim()) + release.Seeders;

                    var catLink = row.Cq().Find("td:eq(0) a").First().Attr("href");
                    var catSplit = catLink.IndexOf("category=");
                    if (catSplit > -1)
                    {
                        catLink = catLink.Substring(catSplit + 9);
                    }

                    release.Category = MapTrackerCatToNewznab(catLink);

                    var grabs = qRow.Find("td:nth-child(6)").Text();
                    release.Grabs = ParseUtil.CoerceInt(grabs);

                    if (qRow.Find("img[title^=\"Free Torrent\"]").Length >= 1)
                        release.DownloadVolumeFactor = 0;
                    else if (qRow.Find("img[title^=\"Silver Torrent\"]").Length >= 1)
                        release.DownloadVolumeFactor = 0.5;
                    else
                        release.DownloadVolumeFactor = 1;


                    if (qRow.Find("img[title^=\"x2 Torrent\"]").Length >= 1)
                        release.UploadVolumeFactor = 2;
                    else
                        release.UploadVolumeFactor = 1;

                    releases.Add(release);
                }
            }
            catch (Exception ex)
            {
                OnParseError(results.Content, ex);
            }

            return releases;
        }
    }
}
