using HtmlAgilityPack;
using Newtonsoft.Json;
using RestSharp;
using System.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using RestSharp.Extensions.MonoHttp;

namespace SteamAchievementsGenerator
{
    class Program
    {
        private static string maindir;
        private static string filesdir;
        private static string folder;
        private static AchGen achgen;
        private static readonly string invalidChars = new string(Path.GetInvalidFileNameChars());
        private static readonly Dictionary<string, string> lang_names = new Dictionary<string, string>();

    static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return EmbeddedAssembly.Get(args.Name);
        }

        static void Main(string[] args)
        {
            Console.WriteLine(@"Achievements Generator
Version 1.1.1
Programmed by Colmines92
");

            string resource1 = "SteamAchievementsGenerator.Resources.HtmlAgilityPack.dll";
            string resource2 = "SteamAchievementsGenerator.Resources.RestSharp.dll";
            string resource3 = "SteamAchievementsGenerator.Resources.Newtonsoft.Json.dll";
            EmbeddedAssembly.Load(resource1, "HtmlAgilityPack.dll");
            EmbeddedAssembly.Load(resource2, "RestSharp.dll");
            EmbeddedAssembly.Load(resource3, "Newtonsoft.Json.dll");

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            lang_names.Add("simplified chinese", "schinese");
            lang_names.Add("traditional chinese", "tchinese");
            lang_names.Add("korean", "koreana");
            lang_names.Add("portuguese - brazil", "brazilian");

            maindir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            if (maindir != null && maindir != "")
                maindir = maindir.Replace("\\", "/");

            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            var filename = args[0].Replace('\\', '/');

            if (!File.Exists(filename))
            {
                PrintUsage();
                return;
            }

            string filedir = Path.GetDirectoryName(filename);
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(filename);

            filesdir = string.Join("/", new string[]{ filedir, filenameWithoutExt + "_files"});
            if (!Directory.Exists(filesdir)) Directory.CreateDirectory(filesdir);

            achgen = new AchGen(filename);
            if (achgen.AppId == "0")
            {
                PrintUsage();
                return;
            }

            folder = Path.Combine(maindir, achgen.Name == "" ? achgen.AppId : achgen.AppId + " - " + achgen.Name);

            bool existed = Directory.Exists(folder);

            if (!MakeDir(folder))
                return;

            if (!existed)
            {
                try
                {
                    Directory.Delete(folder);
                }
                catch { }
            }

            var achievements = achgen.GetAchievements();
            var stats = achgen.GetStats();
            var dlc = achgen.GetDLC();

            SaveFile("steam_appid.txt", achgen.AppId, "text");

            if (achievements != null && achievements.Length != 0)
                SaveFile("achievements.json", achievements, "achievements");

            if (stats != null && stats.Count != 0)
                SaveFile("stats.txt", stats, "stats");

            if (dlc != null && dlc.Count != 0)
                SaveFile("DLC.txt", dlc, "dlc");
        }

        static void PrintUsage()
        {
            Console.WriteLine(@"USAGE:
    THE FOLLOWING STEPS ARE MEANT TO BE PERFORMED ON A DESKTOP BROWSER.

    1. Search your game at https://steamdb.info/
    2. Choose your game id from the list.
    3. Click on achievements, then download both the html file and the folder ending with '_files'.
    4. Drag the downloaded html into the app executable icon and wait for it to finish.");

            Console.ReadKey();
        }

        static bool MakeDir(string path, bool forced = true)
        {
            if (Directory.Exists(path))
                return true;

            if (File.Exists(path))
            {
                if (!forced)
                    return false;

                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public static bool CopyFile(string src, string dst)
        {
            if (!MakeDir(Path.GetDirectoryName(dst)))
                return false;

            try
            {
                File.Copy(src, dst, true);
            }
            catch { return false; }
            return true;
        }

        static void SaveFile(string name, object content, string mode = "stats")
        {
            if (content == null)
                return;

            var filename = Path.Combine(folder, name);
            string json;

            using (var file = new StreamWriter(filename, false, new System.Text.UTF8Encoding(false)))
            {
                JsonSerializerSettings jsonSettings = new JsonSerializerSettings();

                switch (mode)
                {
                    case "achievements":
                        if (((Achievement[])content).Length == 0)
                            break;

                        json = JsonConvert.SerializeObject(content, Formatting.Indented, jsonSettings);

                        if (!MakeDir(Path.GetDirectoryName(folder)))
                            break;

                        try
                        {
                            file.Write(json);
                        }
                        catch { }

                        break;

                    case "stats":
                        List<Dictionary<string, string>> list = (List<Dictionary<string, string>>)content;
                        if (list.Count == 0)
                            break;

                        if (!MakeDir(Path.GetDirectoryName(folder)))
                            break;

                        foreach (Dictionary<string, string> dict in list)
                        {
                            string _name = dict["name"];
                            string _value = dict["defaultValue"];
                            string _type = "int";

                            if (_value.Contains('.'))
                                _type = "float";

                            string format = "{0}={1}={2}";

                            try
                            {
                                file.WriteLine(string.Format(format, _name, _type, _value));
                            }
                            catch { }
                        }
                        break;
                    case "dlc":
                        List<string> dlcvalue = (List<string>)content;
                        if (dlcvalue.Count == 0)
                            break;
                        file.WriteLine(string.Join("\n", dlcvalue));
                        break;
                    default:
                        string value = content.ToString();
                        if (value.Length == 0 || value == "0")
                            break;
                        file.Write(value);
                        break;
                }
            }
        }

        public class Achievement
        {
            public string name = "";
            public Dictionary<string, string> displayName = new Dictionary<string, string>();
            public Dictionary<string, string> description = new Dictionary<string, string>();
            public string hidden = "";
            public string icon = "";
            public string icon_gray = "";
        }

        class AchGen
        {
            private readonly HtmlDocument soup;
            public readonly string AppId;
            public readonly string Name;

            public AchGen(string filename)
            {
                try
                {
                    var content = File.ReadAllText(filename);
                    soup = new HtmlDocument();
                    soup.LoadHtml(content);
                }
                catch { }

                AppId = soup.DocumentNode.SelectSingleNode("//div[@class='scope-app']")?.GetAttributeValue("data-appid", "0");
                Name = ValidateFileName(soup?.DocumentNode.SelectSingleNode("//h1[contains(@itemprop,'name')]")?.InnerText);
            }


            public Achievement[] GetAchievements()
            {
                string fullName = achgen.AppId.ToString();
                if (achgen.Name != null)
                    fullName += ": " + achgen.Name;

                Console.WriteLine("Generating achievements for {0}", fullName);
                Console.WriteLine("Please, wait...");

                var achievements = new List<Achievement>();
                var infoTable = soup.DocumentNode.SelectSingleNode("//table[contains(@class,'table-languages')]");

                List<string> languages = new List<string>();

                foreach (var row in infoTable.SelectNodes(".//tr"))
                {
                    var tds = row.SelectNodes(".//td");
                    if (tds == null)
                        continue;

                    string lang = tds[0].InnerText.Trim().ToString().ToLowerInvariant();
                    if (lang_names.ContainsKey(lang))
                        lang = lang_names[lang];
                    else
                    {
                        lang = lang.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)[0];
                        if (lang_names.ContainsKey(lang))
                            lang = lang_names[lang];
                    }

                    languages.Add(lang);
                }

                if (languages.Count == 0)
                    languages.Add("english");

                var achievementsTable = soup.DocumentNode.SelectSingleNode("//div[@id='js-achievements']");
                if (achievementsTable == null)
                    return achievements.ToArray();

                achievementsTable = achievementsTable.SelectSingleNode(".//tbody");

                var translation = new Dictionary<string, Achievement>();
                var eng = GetEnglishTranslation();

                foreach (string lang in languages)
                {
                    Achievement ach = GetTranslation(lang, ref eng);
                    if (ach.displayName.Count == 0)
                        continue;
                    if (!translation.ContainsKey(lang))
                        translation.Add(lang, ach);
                }

                var imgdir = $"{folder}/achievement_images";

                if (!MakeDir(folder))
                    return achievements.ToArray();

                foreach (var row in achievementsTable.SelectNodes(".//tr"))
                {
                    var tds = row.SelectNodes(".//td");
                    if (tds.Count < 3)
                        continue;

                    Achievement data = new Achievement()
                    {
                        name = tds[0].InnerText.Trim()
                    };

                    var split = tds[1].InnerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (split.Length >= 4)
                    {
                        var text = split[1];
                        var displayName = text;
                        var text2 = split[3].Trim();
                        var description = text2;

                        data.hidden = tds[1].SelectSingleNode(".//svg[@aria-hidden='true']") != null ? "1" : "0";

                        if (description.ToLowerInvariant() == "hidden." && data.hidden == "1")
                            description = "";

                        if (translation.Count == 0)
                        {
                            if (displayName != "")
                                if (!data.displayName.ContainsKey("english"))
                                    data.displayName.Add("english", HttpUtility.HtmlDecode(displayName));
                            if (description != "")
                                if (!data.description.ContainsKey("english"))
                                    data.description.Add("english", HttpUtility.HtmlDecode(description));
                        }
                        else
                        {
                            foreach (string lang in languages)
                            {
                                if (!translation.ContainsKey(lang))
                                    continue;

                                if (translation[lang].displayName.ContainsKey(text))
                                {
                                    if (translation[lang].displayName[text] != "")
                                        displayName = translation[lang].displayName[text];

                                    if (translation[lang].description[text] != "")
                                        description = translation[lang].description[text];
                                }

                                if (displayName != "")
                                    if (!data.displayName.ContainsKey(lang))
                                        data.displayName.Add(lang, HttpUtility.HtmlDecode(displayName));
                                if (description != "")
                                    if (!data.description.ContainsKey(lang))
                                        data.description.Add(lang, HttpUtility.HtmlDecode(description));
                            }
                        }

                        string token = data.name;
                        if (data.name.StartsWith("id_"))
                            token = "NEW_ACHIEVEMENT_1_" + data.name.Substring(3);

                        data.displayName.Add("token", token + "_NAME");
                        data.description.Add("token", token + "_DESC");
                    }

                    var img = tds[2].SelectNodes(".//img");
                    var icon = img[0].GetAttributeValue("data-name", "");
                    var icongray = img[1].GetAttributeValue("data-name", "");

                    data.icon = icon;
                    data.icon_gray = icongray;

                    string src = "";
                    string dst = "";
                    if (MakeDir(imgdir))
                    {
                        src = $"{filesdir}/{icon}";
                        dst = $"{imgdir}/{icon}";
                        CopyFile(src, dst);

                        src = $"{filesdir}/{icongray}";
                        dst = $"{imgdir}/{icongray}";
                        CopyFile(src, dst);
                    }

                    achievements.Add(data);
                }

                return achievements.ToArray();
            }

            public List<Dictionary<string, string>> GetStats()
            {
                var stats = new List<Dictionary<string, string>>();
                var statsTable = soup.DocumentNode.SelectSingleNode("//div[@id='js-stats']");
                if (statsTable == null)
                    return stats;

                statsTable = statsTable.SelectSingleNode(".//tbody");

                foreach (var row in statsTable.SelectNodes(".//tr"))
                {
                    var tds = row.SelectNodes(".//td");
                    if (tds.Count < 3)
                        continue;

                    var data = new Dictionary<string, string>
                    {
                        ["name"] = tds[0].InnerText.Trim(),
                        ["displayName"] = tds[1].InnerText.Trim(),
                        ["defaultValue"] = tds[2].InnerText.Trim(),
                    };

                    stats.Add(data);
                }

                return stats;
            }

            public List<string> GetDLC()
            {
                var dlc = new List<string>();
                var dlcTable = soup.DocumentNode.SelectSingleNode("//div[@id='dlc']");
                if (dlcTable == null)
                    return dlc;

                dlcTable = dlcTable.SelectSingleNode(".//tbody");

                foreach (var row in dlcTable.SelectNodes(".//tr"))
                {
                    if (row.GetAttributeValue("hidden", null) != null)
                        continue;

                    var tds = row.SelectNodes(".//td");
                    if (tds.Count < 2)
                        continue;

                    var line = $"{tds[0].InnerText.Trim()}={tds[1].InnerText.Trim()}";
                    if (!dlc.Contains(line))
                        dlc.Add(line);
                }

                return dlc;
            }

            public HtmlDocument GetEnglishTranslation()
            {
                HtmlDocument result = new HtmlDocument();

                try
                {
                    string req = String.Format("stats/{0}/achievements/", achgen.AppId);
                    var client = new RestClient("https://steamcommunity.com");
                    var request = new RestRequest(req);
                    client.CookieContainer = new CookieContainer();
                    Cookie cook = new Cookie();

                    try
                    {
                        cook = new Cookie("Steam_Language", "english");
                    }
                    catch { return result; }

                    cook.Domain = "steamcommunity.com";
                    var resp = client.Get(request);
                    var orig = resp.Content;

                    var origSoup = new HtmlDocument();
                    origSoup.LoadHtml(orig);

                    result = origSoup;
                }
                catch { }

                return result;
            }

            public Achievement GetTranslation(string lang, ref HtmlDocument origSoup)
            {
                var translation = new Achievement();
                try
                {
                    string req = String.Format("stats/{0}/achievements/", achgen.AppId);
                    var client = new RestClient("https://steamcommunity.com");
                    var request = new RestRequest(req);
                    client.CookieContainer = new CookieContainer();
                    Cookie cook = new Cookie();

                    try
                    {
                        cook = new Cookie("Steam_Language", lang);
                    }
                    catch { return translation; }

                    cook.Domain = "steamcommunity.com";
                    client.CookieContainer.Add(cook);
                    var resp = client.Get(request);
                    var trns = resp.Content;

                    var trnsSoup = new HtmlDocument();
                    trnsSoup.LoadHtml(trns);

                    var origRows = origSoup.DocumentNode.SelectNodes("//div[@class='achieveRow ']");
                    var trnsRows = trnsSoup.DocumentNode.SelectNodes("//div[@class='achieveRow ']");

                    var cnt = 0;

                    if (origRows == null)
                        return translation;

                    var length = origRows.Count;

                    while (cnt < length)
                    {
                        var achieveTxt = origRows[cnt].SelectSingleNode(".//div[@class='achieveTxt']");
                        var origDisplayName = achieveTxt.SelectSingleNode(".//h3").InnerText.Trim();
                        achieveTxt = trnsRows[cnt].SelectSingleNode(".//div[@class='achieveTxt']");
                        var trnsDisplayName = achieveTxt.SelectSingleNode(".//h3").InnerText.Trim();
                        var trnsDescription = achieveTxt.SelectSingleNode(".//h5").InnerText.Trim();
                        if (!translation.displayName.ContainsKey(origDisplayName))
                        {
                            translation.displayName.Add(origDisplayName, trnsDisplayName);
                            translation.description.Add(origDisplayName, trnsDescription);
                        }
                        cnt++;
                    }
                }
                catch { }

                return translation;
            }

            public string ValidateFileName(string fileName = "")
            {
                foreach (char c in invalidChars)
                    fileName = fileName.Replace(c.ToString(), "_");
                return fileName;
            }
        }
    }
}