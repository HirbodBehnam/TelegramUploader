using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json;
using TeleSharp.TL;
using TLSharp.Core;
using TLSharp.Core.Exceptions;
using TLSharp.Core.Utils;

namespace TelegramUploader
{
    class Program
    {
        public struct Config
        {
            public int ApiId;
            public string ApiHash;
            public string Number;
        }
        public class CommandLineOptions
        {
            [Option('f', "file", Required = true, HelpText = "The file to upload")]
            public string File { get; set; }
            [Option('c', "caption", Required = false,Default = "", HelpText = "The caption of the file")]
            public string Caption { get; set; }
        }
        private static Config _config;
        static void Main(string[] args)
        {
            // at first parse the config file
            if (!File.Exists("config.json"))
            {
                Console.Write("Error: config.json file does not exists.");
                Environment.Exit(1);
            }

            _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
            // now parse command line
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithParsed(o =>
                {
                    Console.WriteLine("Starting to upload");
                    RunClient(o.File,o.Caption).Wait();
                });
        }

        private static async Task RunClient(string fileName,string caption)
        {
            Console.WriteLine("Logging in");
            var client = new TelegramClient(_config.ApiId, _config.ApiHash);
            await client.ConnectAsync();
            TLUser me;
            if (!client.IsUserAuthorized())
            {
                string hash = await client.SendCodeRequestAsync(_config.Number);
                Console.Write("Please enter the code you received: ");
                string code = Console.ReadLine();
                try
                {
                    me = await client.MakeAuthAsync(_config.Number, hash, code);
                }
                catch (CloudPasswordNeededException)
                {
                    var passwordSetting = await client.GetPasswordSetting();
                    Console.Write("Enter your 2FA password: ");
                    string password = GetPassword();
                    me = await client.MakeAuthWithPasswordAsync(passwordSetting, password);
                }
                catch (InvalidPhoneCodeException ex)
                {
                    throw new Exception(
                        "CodeToAuthenticate is wrong in the app.config file, fill it with the code you just got now by SMS/Telegram",
                        ex);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An exception occured: " + ex);
                    Console.WriteLine("If you want a fresh start just remove session.dat file and restart.");
                    return;
                }
            }
            else
            {
                var result = await client.GetContactsAsync();

                //find recipient in contacts
                me = result.Users
                    .Where(x => x.GetType() == typeof (TLUser))
                    .Cast<TLUser>()
                    .FirstOrDefault(x => x.Self);
            }
            
            Console.WriteLine("Uploading file");
            var attr = new TLVector<TLAbsDocumentAttribute>();
            var docAttr = new TLDocumentAttributeFilename
            {
                FileName = Path.GetFileName(fileName)
            };
            attr.Add(docAttr);
            var fileResult = await client.UploadFile(Path.GetFileName(fileName), new StreamReader(fileName));
            Console.WriteLine("Uploaded; Sending file");
            await client.SendUploadedDocument(
                new TLInputPeerUser { UserId = me.Id },
                fileResult,
                caption,
                "application/octet-stream",
                attr);
            Console.WriteLine("Done");
        }

        private static string GetPassword()
        {
            string pass = "";
            do
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                // Backspace Should Not Work
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                    {
                        pass = pass.Substring(0, (pass.Length - 1));
                        Console.Write("\b \b");
                    }
                    else if(key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        break;
                    }
                }
            } while (true);

            return pass;
        }
    }
    
}
