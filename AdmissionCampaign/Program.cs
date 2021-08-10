using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using static System.Console;
using static AdmissionCampaign.BaseData;
using static AdmissionCampaign.DataHandler;

namespace AdmissionCampaign
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var message = await Task.Run(() => EntryPoint().Result);
            await SaveMessageToFile(message);
            WriteLine(message);
            WriteLine($"Fetch is done.");
        }

        private static async Task<string> EntryPoint()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            var data = SpecialityValues.Select(speciality => Task.Run(async() => ParseData(await Request(speciality)))).ToList();

            var message = string.Empty;
            var lastReport = await ReadLastRequest();
            foreach (var parsedDataResult in data.Select(parsedData => parsedData.Result))
            {
                message +=
                    $"{SpecialityTitleShort[parsedDataResult.Specialty]}: {parsedDataResult.CurrentCount} согласий на {parsedDataResult.MaxCount} бюджетных мест";

                var lastSpecialityMetrics =
                    lastReport?.Data.SingleOrDefault(d => d.Specialty == parsedDataResult.Specialty);

                if (lastSpecialityMetrics != null)
                {
                    if (lastSpecialityMetrics.CurrentCount < parsedDataResult.CurrentCount)
                    {
                        message += $" (+{parsedDataResult.CurrentCount - lastSpecialityMetrics.CurrentCount})";
                    }
                    else if (lastSpecialityMetrics.CurrentCount > parsedDataResult.CurrentCount)
                    {
                        message += $" (-{lastSpecialityMetrics.CurrentCount - parsedDataResult.CurrentCount})";
                    }
                }

                message += $", проходной балл {parsedDataResult.AcceptedScore}\n";
            }

            var dataForSave = new ParsedDataList(data);
            
            SaveLastRequest(dataForSave);

            return message;
        }

        private static ParsedData ParseData((Specialty specialty, string html) input)
        {
            var (specialty, html) = input;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var ul = doc.DocumentNode.SelectSingleNode("//ul");
            var lines = ul.SelectNodes("//li").Select(node => node.InnerText).ToList();

            var current = int.Parse(Regex.Replace(lines[1], DigitOnlyPattern, ""));
            var max = int.Parse(Regex.Replace(lines[7], DigitOnlyPattern, ""));

            var ratingTable = doc.GetElementbyId("div4").ChildNodes[1];

            var hasAgreementCount = 0;
            var finalScore = 0;
            foreach (var row in ratingTable.SelectNodes("tr"))
            {
                var cells = row.SelectNodes("td");
                
                var agreementCheck = cells[16].InnerText.Contains("да ");
                if (agreementCheck)
                {
                    hasAgreementCount++;
                }

                if (hasAgreementCount == max)
                {
                    finalScore = int.Parse(Regex.Replace(cells[14].InnerText, DigitOnlyPattern, ""));
                    break;
                }
            }
            
            return new ParsedData(specialty, current, max, finalScore);
        }

        private static async Task<(Specialty, string)> Request(Specialty specialty)
        {
            string responseStr;

            using var httpClient = new HttpClient();

            try
            {
                var getResponse = httpClient.PostAsync(ServerUrl, CreateContent(specialty));
                var response = await getResponse;
                responseStr = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                WriteLine("Error while getting server response: " + e.Message);
                responseStr = e.Message;
            }

            return (specialty, responseStr);
        }

        private static FormUrlEncodedContent CreateContent(Specialty specialty)
        {
            var values = new List<KeyValuePair<string, string>>();
            CreateRequestForm(specialty, ref values);

            var content = new FormUrlEncodedContent(values);
            content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);

            return content;
        }
    }

    #region Models

    public enum Specialty
    {
        IiIT_01,
        IiIT_02,
        IiIT_03,
        IiIT_04,
        FIT_Web,
        FIT_SAPR,
        FIT_POIT,
        FIT_CyberPhys,
        FIT_CORPIS,
        FIT_BigData
    }

    public static class BaseData
    {
        public const string DigitOnlyPattern = @"[^\d]";
        public const string ServerUrl = "https://raitinglistpk.mospolytech.ru/rating_list_ajax.php";
        public const string ContentType = "application/x-www-form-urlencoded";

        public static IEnumerable<Specialty> SpecialityValues => Enum.GetValues(typeof(Specialty)).Cast<Specialty>();
        
        private static readonly Dictionary<Specialty, string> SpecialityTitle = new Dictionary<Specialty, string>()
        {
            {
                Specialty.IiIT_01,
                "09.03.02_Информационные системы и технологии (Информационные системы и технологии обработки цифрового контента; Информационные и автоматизированные системы обр"
            },
            { Specialty.IiIT_02, "09.03.02_Информационные системы и технологии обработки цифрового контента" },
            { Specialty.IiIT_03, "09.03.02_Информационные системы автоматизированных комплексов медиаиндустрии" },
            { Specialty.IiIT_04, "09.03.02_Цифровая трансформация" },
            { Specialty.FIT_Web, "09.03.01_Веб-технологии"},
            { Specialty.FIT_SAPR, "09.03.01_Интеграция и программирование в САПР"},
            { Specialty.FIT_POIT, "09.03.01_Программное обеспечение информационных систем"},
            { Specialty.FIT_CyberPhys, "09.03.01_Киберфизические системы"},
            { Specialty.FIT_CORPIS, "09.03.03_Корпоративные информационные системы"},
            { Specialty.FIT_BigData, "09.03.03_Большие и открытые данные"}
            
        };

        public static readonly Dictionary<Specialty, string> SpecialityTitleShort = new Dictionary<Specialty, string>()
        {
            { Specialty.IiIT_01, "09.03.02.01 (ИСиТ)" },
            { Specialty.IiIT_02, "09.03.02.02 (ИСиТ ОЦК)" },
            { Specialty.IiIT_03, "09.03.02.03 (ИСАКМ)" },
            { Specialty.IiIT_04, "09.03.02.04 (ЦТ)" },
            { Specialty.FIT_Web, "09.03.01.01 (Веб)"},
            { Specialty.FIT_SAPR, "09.03.01.02 (САПР)"},
            { Specialty.FIT_POIT, "09.03.01.03 (ПОИС)"},
            { Specialty.FIT_CyberPhys, "09.03.01.04 (КФС)"},
            { Specialty.FIT_CORPIS, "09.03.03.01 (КИС)"},
            { Specialty.FIT_BigData, "09.03.03.02 (Биг дата)"}
        };

        public static void CreateRequestForm(Specialty specialty,
            ref List<KeyValuePair<string, string>> data)
        {
            data.AddRange(CommonForm);
            data.Add(new KeyValuePair<string, string>("specCode", SpecialityTitle[specialty]));


            if (specialty == Specialty.IiIT_02 || specialty == Specialty.FIT_POIT)
            {
                data.Add(new KeyValuePair<string, string>("eduForm", "Заочная"));
            }
            else
            {
                data.Add(new KeyValuePair<string, string>("eduForm", "Очная"));
            }
        }

        private static readonly List<KeyValuePair<string, string>> CommonForm = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("select1", "000000017_01"),
            new KeyValuePair<string, string>("eduFin", "Бюджетная основа")
        };
    }

    #endregion

    #region I/O Tools

    public static class DataHandler
    {
        private const string ParsedDataFilename = "ParsedData.json";
        private const string MessageContentFilename = "MessageContent.txt";

        public class ParsedDataList
        {
            public List<ParsedData> Data { get; set; }

            public ParsedDataList() { }
            
            public ParsedDataList(IEnumerable<Task<ParsedData>> data)
            {
                Data = new List<ParsedData>();
                foreach (var task in data)
                {
                    Data.Add(task.Result);
                }
            }
        }

        public class ParsedData
        {
            public Specialty Specialty { get; set; }
            public int MaxCount { get; set; }
            public int CurrentCount { get; set; }
            public int AcceptedScore { get; set; }

            public ParsedData(Specialty spec, int current, int max, int score)
            {
                Specialty = spec;
                MaxCount = max;
                CurrentCount = current;
                AcceptedScore = score;
            }
        }

        public static async Task SaveLastRequest(ParsedDataList data)
        {
            var path = Path.Combine(Environment.CurrentDirectory, ParsedDataFilename);
            var json = JsonConvert.SerializeObject(data);
            await File.WriteAllTextAsync(path, json);
        }

        public static async Task<ParsedDataList> ReadLastRequest()
        {
            var path = Path.Combine(Environment.CurrentDirectory, ParsedDataFilename);
            
            if (!File.Exists(path))
            {
                WriteLine("[WARNING] Can't find saved data, abort.");
                return null;
            }

            var json = await File.ReadAllTextAsync(path);

            var data = JsonConvert.DeserializeObject(json, typeof(ParsedDataList));
            if (data == null)
            {
                WriteLine("[WARNING] Can't parse saved data, abort.");
                return null;
            }

            return (ParsedDataList)data;
        }

        public static async Task SaveMessageToFile(string data)
        {
            var path = Path.Combine(Environment.CurrentDirectory, MessageContentFilename);
            await File.WriteAllTextAsync(path, data);
        }
    }

    #endregion
}
