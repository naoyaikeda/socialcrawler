using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using OAuthLib;
using Brainchild.Net;
using System.Net;
using System.IO;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using Codeplex.Data;
using System.Threading;
using IronPython.Runtime;
using NLog;

namespace BrainChilld.TweetCapture
{
    class Program
    {
        public static string currentWorkPath = string.Empty;
        public static ScriptEngine scriptEngine = null;
        public static ScriptScope scriptScope = null;
        public static ScriptSource scriptSource = null;
        public static string rotateScript = null;
        public static Logger logger = null;
        static Consumer consumer;

        static void Usage()
        {
            Console.WriteLine("TweetCapture version {0}", Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Copyright (C) 2012 by BrainChild, All rights reserved.");
            Console.WriteLine("Usage:");
            Console.WriteLine("TweetCapture <command> [options...]");
            Console.WriteLine("Command:");
            Console.WriteLine("auth");
            Console.WriteLine("cruise");
            Console.WriteLine("status");
        }

        static void Authentication()
        {
            try
            {
                var reqToken = consumer.ObtainUnauthorizedRequestToken(Properties.Settings.Default.URL_REQUEST_TOKEN, Properties.Settings.Default.URL_REALM);
                var authUrl = Consumer.BuildUserAuthorizationURL(Properties.Settings.Default.URL_OAUTH2_AUTH, reqToken);
                Console.WriteLine("Please, authorize...");
                Console.WriteLine(authUrl);
                Console.Write("Verifier: ");
                var verifier = Console.ReadLine();
                var accessToken = consumer.RequestAccessToken(verifier, reqToken, Properties.Settings.Default.URL_REQUEST_ACCESS_TOKEN, Properties.Settings.Default.URL_REALM);
                Properties.Settings.Default.TokenValue = accessToken.TokenValue;
                Properties.Settings.Default.TokenSecret = accessToken.TokenSecret;
                Properties.Settings.Default.Save();
            }
            catch (WebException ex)
            {
                logger.Error("Can't build token object");
                if (ex.Response != null)
                {
                    using (var sr = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        string buffer;
                        while ((buffer = sr.ReadLine()) != null)
                        {
                            logger.Error("Response corrupted: {0}", buffer);
                        }
                    }
                }
            }
        }

        static void Unset(string name)
        {
            switch (name)
            {
                case "ScriptPath":
                    Properties.Settings.Default.ScriptPath = "";
                    Properties.Settings.Default.Save();
                    break;
                default:
                    Usage();
                    break;
            }
        }

        static void Set(string name, string value)
        {
            switch (name)
            {
                case "ScriptPath":
                    Properties.Settings.Default.ScriptPath = value;
                    Properties.Settings.Default.Save();
                    break;
                default:
                    Usage();
                    break;
            }
        }

        private static TwitterStream CreateClient()
        {
            var twStream = TwitterStream.Create(
                Properties.Settings.Default.ConsumerToken,
                Properties.Settings.Default.ConsumerSecret,
                Properties.Settings.Default.TokenValue,
                Properties.Settings.Default.TokenSecret);
            return twStream;
        }

        static void Cruise()
        {
            currentWorkPath = Path.GetTempFileName();
            logger.Info("Current: {0}", currentWorkPath);
            var sw = new StreamWriter(currentWorkPath, false, Encoding.UTF8);
            TwitterStream twStream = null;
            try
            {
                twStream = CreateClient();
            }
            catch (Exception)
            {
            }
            int retry = 0;

            while (true)
            {
                if (twStream == null)
                {
                    logger.Error("Twitter can't initialize");
                }

                try
                {
                    foreach (var buffer in twStream)
                    {
                        var parsed = DynamicJson.Parse(buffer);
                        var jobject = DynamicJson.Parse(buffer);
                        Debug.WriteLine(buffer, "buffer");
                        if (jobject.IsDefined("user"))
                        {
                            if (jobject.user.IsDefined("lang"))
                            {
                                if (jobject.user.lang == "ja")
                                {
                                    sw.WriteLine(buffer);
                                }
                            }
                            if (jobject.user.IsDefined("time_zone"))
                            {
                                if (jobject.user.lang == "Tokyo")
                                {
                                    sw.WriteLine(buffer);
                                }
                            }
                            var fi = new FileInfo(currentWorkPath);
                            if (fi.Length > Properties.Settings.Default.SPLIT_SIZE)
                            {
                                sw.Close();
                                currentWorkPath = Rotation();
                                logger.Info("Current: {0}", currentWorkPath);
                                sw = new StreamWriter(currentWorkPath, false, Encoding.UTF8);
                            }
                        }
                        else
                        {
                            //writer.WriteLine("**Unknown**");
                        }
                    }
                }
                catch (Exception)
                {
                    retry = 0;
                    twStream = null;
                    while (retry < 10 && twStream == null)
                    {
                        try
                        {
                            twStream = CreateClient();
                        }
                        catch (Exception)
                        {
                        }
                        retry++;
                    }
                    retry = 0;
                    if (twStream == null)
                    {
                        Thread.Sleep(1000 * 60 * 10);
                        while (retry < 10 && twStream == null)
                        {
                            try
                            {
                                twStream = CreateClient();
                            }
                            catch (Exception)
                            {
                            }
                            retry++;
                        }
                    }
                    retry = 0;
                }
            }
        }

        static void Dump()
        {
        }

        static void Import()
        {
        }

        static void Status()
        {
            if (string.IsNullOrEmpty(BrainChilld.TweetCapture.Properties.Settings.Default.TokenValue) == false)
                Console.WriteLine("token value is maybe ok");
            else
                Console.WriteLine("token value is null");
            if (string.IsNullOrEmpty(BrainChilld.TweetCapture.Properties.Settings.Default.TokenSecret) == false)
                Console.WriteLine("token secret is maybe ok");
            else
                Console.WriteLine("token secret is null");
            Console.WriteLine("Script Path: {0}", Properties.Settings.Default.ScriptPath);
        }

        static string Rotation()
        {
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.ScriptPath))
            {
                try
                {
                    scriptScope.SetVariable("CurrentWorkPath", currentWorkPath);
                    scriptSource.Execute(scriptScope);

                    if (File.Exists(currentWorkPath))
                        File.Delete(currentWorkPath);
                }
                catch (UnboundNameException ex)
                {
                    Debug.WriteLine(ex.Message, "Message");
                    Debug.WriteLine(ex.StackTrace, "StackTrace");
                    Console.WriteLine("Message: {0}", ex.Message);
                    Console.WriteLine("Stack: {0}", ex.StackTrace);
                }
            }
            else
            {
                if (File.Exists(currentWorkPath))
                    File.Delete(currentWorkPath);
            }
            return Path.GetTempFileName();
        }

        static void Main(string[] args)
        {
            logger = LogManager.GetLogger("TweetCapture");
            logger.Info("TweetCapture {0}", Assembly.GetExecutingAssembly().GetName().Version);

            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.ScriptPath))
            {
                try
                {
                    scriptEngine = Python.CreateEngine();
                    scriptScope = scriptEngine.CreateScope();
                    scriptSource = scriptEngine.CreateScriptSourceFromFile(Properties.Settings.Default.ScriptPath);
                }
                catch (UnboundNameException ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                }
            }

            consumer = new Consumer(
                            Properties.Settings.Default.ConsumerToken,
                            Properties.Settings.Default.ConsumerSecret);

            if (args.Count() < 1)
                Usage();
            else {
                var command = args[0];
                switch (command.ToLower())
                {
                    case "auth":
                        Authentication();
                        break;
                    case "cruise":
                        Cruise();
                        break;
                    case "status":
                        Status();
                        break;
                    case "dump":
                        Dump();
                        break;
                    case "import":
                        Import();
                        break;
                    case "set":
                        if (args.Count() < 3)
                            if (args.Count() == 2)
                                Unset(args[1]);
                            else
                                Usage();
                        else
                            Set(args[1], args[2]);
                        break;
                    case "unset":
                        if (args.Count() < 2)
                            Usage();
                        else
                            Unset(args[1]);
                        break;
                    default:
                        Usage();
                        break;
                }
            }
        }
    }
}
