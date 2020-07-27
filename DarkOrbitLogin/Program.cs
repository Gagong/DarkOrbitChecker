using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Json;
using System.Threading.Tasks;

namespace DarkOrbitLogin
{
    class Program
    {
        private static string outText;
        
        private static async Task Main(string[] args)
        {
            // Variables
            const string fileName = "loginDetails.txt";
            const string fileOut = "AccountsInfo.txt";
            int userLenght = 0;
            
            if (File.Exists(fileOut))
                File.Delete(fileOut);
            
            // Ask Faplord why he need this shit ;D
            using (StreamReader read = new StreamReader(fileName))
                while (!read.EndOfStream)
                {
                    var userName = read.ReadLine()?.Replace(" ", "").Split(":")[0];
                    if (userName.Length > userLenght)
                        userLenght = userName.Length;
                }
            
            using (StreamReader read = new StreamReader(fileName))
                while (!read.EndOfStream)
                {
                    var loginDetailsString = read.ReadLine()?.Replace(" ", "").Split(":");
                    if (loginDetailsString != null)
                    {
                        int needToAppend = userLenght - loginDetailsString[0].Length;
                        string fixName;
                        if (userLenght > 0)
                        {
                            fixName = loginDetailsString[0];
                            fixName += new string(' ', needToAppend);
                        }
                        else
                            fixName = loginDetailsString[0];
                        outText += $"Account info: {fixName} ";
                        await execute(loginDetailsString[0], loginDetailsString[1]);
                    }
                }
            File.WriteAllText(fileOut, outText, Encoding.UTF8);
            
            Console.WriteLine("\n\nDone. Press any key to continue or close this app");
            Console.ReadLine();
        }

        private async static Task execute(string login, string password)
        {
            // Variables
            const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36";
            string authUrl = "https://sas.bpsecure.com/Sas/Authentication/Bigpoint?authUser=22&";
            const string mainPageUrl = "https://www.darkorbit.com";
            var loginDetails = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", login),
                new KeyValuePair<string, string>("password", password),
            });

            Console.Write("Connecting to DarkOrbit main page: ");
            var darkOrbitMainPage = new Uri(mainPageUrl);
            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
            using var client = new HttpClient(handler) { BaseAddress = darkOrbitMainPage };
            var mainPageResult = client.GetAsync("/");
            mainPageResult.Result.EnsureSuccessStatusCode();
            Console.WriteLine(mainPageResult.Result.StatusCode);

            Console.Write("Getting DarkOrbit token: ");
            var token = FindStringInHtml(mainPageResult.Result.Content.ReadAsStringAsync().Result, 
                "https://sas.bpsecure.com/Sas/Authentication/Bigpoint?authUser=22&amp;token=", 
                "\">");
            authUrl += "token=" + token;
            Console.WriteLine("OK");

            Console.Write("Making POST request with login details: ");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl);
            httpRequestMessage.Headers.UserAgent.ParseAdd(userAgent);
            httpRequestMessage.Content = loginDetails;
            var webAwaiter = client.SendAsync(httpRequestMessage).GetAwaiter().GetResult();
            Console.WriteLine(webAwaiter.StatusCode);
            
            string ID = FindStringInHtml(webAwaiter.Content.ReadAsStringAsync().Result, "\"uid\":", ",\"tid\"");
            string SID = FindStringInHtml(webAwaiter.Content.ReadAsStringAsync().Result, "'dosid=", "');");
            string server = webAwaiter.RequestMessage.RequestUri.ToString().Split(".")[0].Split("//")[1];
            Console.WriteLine($"Trying to get account ID: {ID}\n" +
                              $"Trying to get account SID: {SID}\n" +
                              $"Trying to get game server: {server}");
            outText += $"ID: {ID} Server: {server} ";

            dynamic userParams = JsonParser.Deserialize(
                FindStringInHtml(webAwaiter.Content.ReadAsStringAsync().Result, "User.Parameters = ", ";"));
            Console.WriteLine($"Trying to get Uridium: {userParams.balance.uridium}\n" +
                              $"Trying to get Credits: {userParams.balance.credits}");
            outText += $"Uridium: {userParams.balance.uridium} Credits: {userParams.balance.credits} ";
            
            Console.Write("Making GET request with galaxy gates details: ");
            string galaxyGatesUrl = $"https://{server}.darkorbit.com/flashinput/galaxyGates.php?userID={ID}&action=init&sid={SID}";
            var galaxyGatesRequestMessage = new HttpRequestMessage(HttpMethod.Get, galaxyGatesUrl);
            galaxyGatesRequestMessage.Headers.Referrer = new Uri($"https://{server}.darkorbit.com/indexInternal.es?action=internalGalaxyGates");
            galaxyGatesRequestMessage.Headers.UserAgent.ParseAdd(userAgent);
            var galaxyGatesAwaiter = client.SendAsync(galaxyGatesRequestMessage).GetAwaiter().GetResult();
            Console.WriteLine(galaxyGatesAwaiter.StatusCode);
            
            //Lazy to implement XML serializer 
            Console.Write("Trying to get Extra Energy: ");
            string EE = FindStringInHtml(galaxyGatesAwaiter.Content.ReadAsStringAsync().Result, "<samples>", "</samples>");
            Console.WriteLine(EE);
            outText += $"Extra Energy: {EE}\n";
        }
        
        //To find any text between start and end sctring in source
        private static string FindStringInHtml(string source, string start, string end)
        {
            if (source.Contains(start) && source.Contains(end))
            {
                var reqStart = source.IndexOf(start, 0, StringComparison.Ordinal) + start.Length;
                var reqEnd = source.IndexOf(end, reqStart, StringComparison.Ordinal);
                return source.Substring(reqStart, reqEnd - reqStart);
            }
            return "";
        }
        
    }
}