using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using ConcurrentPipe.Entities;
using System.Collections.Concurrent;
using System.Threading;

namespace ConcurrentPipe
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length > 0)
            {

                //try deserialize the json from it:
                if(args[0].ToLower() == "-e")
                {
                    ConcurrentSettings concurrentSettings = LoadSettings();

                    if (args.Length == 1)
                    {
                        foreach (string runnerKey in concurrentSettings.Runners.Keys)
                        {
                            Console.WriteLine($"Key: {runnerKey}");
                            Console.WriteLine($"-j \"{EmitSetting(concurrentSettings.Runners[runnerKey])}\"");
                        }
                    }
                    else
                    {
                        foreach (string arg in args.Skip(1))
                        {
                            if (concurrentSettings.Runners.ContainsKey(arg))
                            {
                                Console.WriteLine($"Key: {arg}");
                                Console.WriteLine($"-j \"{EmitSetting(concurrentSettings.Runners[arg])}\"");
                            }
                        }
                    }
                }
                else if (args[0].ToLower() == "-r")
                {
                    ConcurrentSettings concurrentSettings = LoadSettings();

                    if (args == null || args.Length == 0)
                    {
                        foreach (string runnerKey in concurrentSettings.Runners.Keys)
                        {
                            RunnerSetting runner = concurrentSettings.Runners[runnerKey];
                            ExecuteRunner(runner, new List<string>()).Wait();
                        }
                    }
                    else
                    {
                        foreach (string arg in args.Skip(1))
                        {
                            if (concurrentSettings.Runners.ContainsKey(arg))
                            {
                                RunnerSetting runner = concurrentSettings.Runners[arg];
                                ExecuteRunner(runner, new List<string>()).Wait();
                            }
                        }
                    }
                }
                else if(args[0].ToLower() == "-j" && args.Length > 1)
                {
                    //try deserialize json
                    string json = args[1];
                    try
                    {
                        RunnerSetting runner = JsonConvert.DeserializeObject<RunnerSetting>(json);
                        ExecuteRunner(runner, new List<string>()).Wait();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Error in Reading Json {ex.GetType().FullName}");
                        Console.WriteLine(ex.Message);
                        throw ex;
                    }
                }
            }
            else
            {
                Console.WriteLine($"Use -e to emit");
                Console.WriteLine($"Use -r to run");
                Console.WriteLine($"Use -j to use json");
            }
          

            //Console.ReadKey();
        }


        static ConcurrentSettings LoadSettings()
        {
            FileInfo concurrentSettingJson = new FileInfo($"{AppContext.BaseDirectory}\\concurrent.json");

            ConcurrentSettings concurrentSettings = null;
            if (!File.Exists(concurrentSettingJson.FullName))
            {
                concurrentSettings = CreateConcurrentSetting(concurrentSettingJson.FullName);
            }
            else
            {
                try
                {
                    concurrentSettings = JsonConvert.DeserializeObject<ConcurrentSettings>(File.ReadAllText(concurrentSettingJson.FullName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.ToString()}");
                    concurrentSettings = CreateConcurrentSetting(concurrentSettingJson.FullName);
                }
            }

            return concurrentSettings;
        }

        static Regex quoteEscaper = new Regex(@"((?<!\\)""|\\"")");
        static string EmitSetting(RunnerSetting setting)
        {
            string json = JsonConvert.SerializeObject(setting, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            return quoteEscaper.Replace(json, match =>
            {
                if( match.Groups[1].Value == "\"")
                {
                    return "\\\"";
                }
                else
                {
                    return "\\\\\\\"";
                }
            });
        }
        static async Task ExecuteRunner(RunnerSetting runner, List<string> paths)
        {
            Console.WriteLine($"Begin Runner - {string.Join("->", paths.Append(runner.Name))}:");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();


            DirectoryInfo workingDirectory = null;
            if(runner.Directory == null)
            {
                workingDirectory = new DirectoryInfo(Directory.GetCurrentDirectory()); //@"C:\Users\Jack\Documents\GitHub\corscookie");// 

            }
            else
            {
                workingDirectory = new DirectoryInfo($"{Directory.GetCurrentDirectory()}\\{runner.Directory}");
            }
            
            //execute all the commands first
            var whenAll = Task.WhenAll(runner.Commands.Select(command => RunCommand(runner.Name, workingDirectory,  command)));

            if(await Task.WhenAny(whenAll, Task.Delay(1000 * runner.Timeout)) != whenAll) // check if task is timeout
            {
                Console.Error.WriteLine($"Runner \"{runner.Name}\" time out in {runner.Timeout} seconds while executing {string.Join(", ",  runner.Commands)}.");
                Environment.Exit(1);
                return;
            }

            //then prepare the alive ones if there are any
            List<Regex> checks = runner.ReadyChecks?.Select(check => new Regex(check)).ToList();
            List<Process> alives = null;
            if(runner.Alives != null && runner.Alives.Count > 0)
                alives = runner.Alives.Select(command => PrepareProcess(runner.Name, workingDirectory, command, checks, runner.ReadyTimeout)).ToList();

            //run all the sub runners in serial
            if(runner.Runners != null && runner.Runners.Count > 0)
                await Task.WhenAll(runner.Runners.Select(subrunner => ExecuteRunner(subrunner, paths.Append(runner.Name))));


            if (alives != null && alives.Count > 0)
                foreach(Process action in alives)
                {
                    StopProcess(action);
                }

            stopwatch.Stop();
            Console.WriteLine($"End Runner - {string.Join("->", paths.Append(runner.Name))}. {stopwatch.ElapsedMilliseconds}ms costed.");
        }

        static ConcurrentSettings CreateConcurrentSetting(string filename)
        {
            var cacheSetting = new ConcurrentSettings() {
                Runners = new Dictionary<string, RunnerSetting>()
                {
                    {
                        "test",
                        new RunnerSetting() { Commands = new List<string>() { "dir" }, Timeout = 120 } }
                }
            };
            File.WriteAllText(filename, JsonConvert.SerializeObject(cacheSetting));
            Console.WriteLine($"Cache Setting Created!");
            return cacheSetting;
        }

        static ConcurrentDictionary<int, Process> processes = new ConcurrentDictionary<int, Process>();

        static Task<TaskResult> RunCommand(string batch, DirectoryInfo workingDirectory, string command)
        {
            Console.WriteLine($"Begin to run: {command}");
            ProcessStartInfo commandProcess = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c {command}",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                WorkingDirectory = workingDirectory.FullName
            };
            Task<TaskResult> runner = new Task<TaskResult>(() =>
            {

                int shouldRetry = 5;

                while(shouldRetry > 0)
                {
                    using (Process action = Process.Start(commandProcess))
                    {
                        processes.AddOrUpdate(action.Id, action, (id, value) => action);


                        List<string> outputLines = new List<string>();

                        Task _output = new Task(() =>
                        {
                            using (StreamReader reader = action.StandardOutput)
                            {
                                while (!reader.EndOfStream)
                                {
                                    var line = reader.ReadLine();
                                    Debug.WriteLine($"@'{command}': {line}");
                                    outputLines.Add($"@ {line}");
                                }

                            }
                        });

                        _output.Start();

                        Task _error = new Task(() =>
                        {
                            using (StreamReader reader = action.StandardError)
                            {

                                while (!reader.EndOfStream)
                                {
                                    var line = reader.ReadLine();
                                    Debug.WriteLine($"!'{command}: {line}");
                                    outputLines.Add($"! {line}");
                                }
                            }
                        });

                        _error.Start();

                        Task.WaitAll(new Task[] { _output, _error });

                        action.WaitForExit();

                        Process removed = null;
                        while (processes.ContainsKey(action.Id) && !processes.TryRemove(action.Id, out removed)) { }


                        //check if it is blocked by file access.
                        if (outputLines.Any(line => line.Contains("because it is being used by another process.")))
                        {
                            Console.WriteLine($"{command} failed because of file access conflicts. Retry countdown {shouldRetry}");

                            shouldRetry -= 1;
                            continue;
                        }
                        else
                        {
                            shouldRetry = 0;
                        }


                        TaskResult result = new TaskResult()
                        {
                            ExitCode = action.ExitCode,
                            Output = outputLines
                        };

                        LogOperation(command, outputLines, result.ExitCode != 0);

                        if (result.ExitCode != 0)
                        {
                            //exit with non-zero code
                            KillAll();
                            Environment.Exit(result.ExitCode);
                        }
                        return result;
                    }
                }
                return new TaskResult()
                {
                    ExitCode = 0,
                    Output = new List<string>()
                };

            });
            runner.Start();
            return runner;
        }

        static object logger = new object();
        static void LogOperation(string command, List<string> outputLines, bool isError = false)
        {

            // here explains how to create block in team city log
            // https://confluence.jetbrains.com/display/TCD6/Build+Script+Interaction+with+TeamCity#BuildScriptInteractionwithTeamCity-BlocksofServiceMessages


            lock (logger)
            {
                Console.ForegroundColor = isError ? ConsoleColor.Red : ConsoleColor.White;
                Console.WriteLine($"##teamcity[blockOpened name='<{(isError?"Error in ":"")}{command}>']");
                foreach (var line in outputLines)
                {
                    if (line.StartsWith("@"))
                    {
                        Console.ForegroundColor = isError ? ConsoleColor.Red : ConsoleColor.White;
                        Console.WriteLine(line);
                    }
                    else if (line.StartsWith("!"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(line);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(line);
                    }
                }
                Console.ForegroundColor = isError ? ConsoleColor.Red : ConsoleColor.White;
                Console.WriteLine($"##teamcity[blockClosed name='<{command}>']");
            }
        }

        static Process PrepareProcess(string batch, DirectoryInfo workingDirectory, string command, List<Regex> checks, int timeout)
        {
            Console.WriteLine($"Begin to prepare: {command}");

            string root = workingDirectory.Root.FullName.Replace(@"\", "");

            ProcessStartInfo commandProcess = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c {root} & cd {workingDirectory.FullName} & {command}", // 
                UseShellExecute = true,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory.FullName
            };
            Process action = Process.Start(commandProcess);
            processes.AddOrUpdate(action.Id, action, (id, value) => action);
            action.Exited += onExited;

            if(checks != null && checks.Count > 0)
            {
                Stopwatch watch = new Stopwatch();

                watch.Start();

                using (StreamReader reader = action.StandardOutput)
                {
                    while (watch.ElapsedMilliseconds < timeout * 1000)
                    {
                        string output = reader.ReadLine();
                        if (checks.Any(check => check.IsMatch(output)))
                            break;
                        Thread.Sleep(100);
                    }
                }
                watch.Stop();
            }

            return action;
        }

        static void StopProcess(Process action)
        {
            Process removed = null;
            while (processes.ContainsKey(action.Id) && !processes.TryRemove(action.Id, out removed)) { }
            removed.Exited -= onExited;
            removed.Kill();
        }

        static void onExited(object sender, EventArgs e)
        {
            Process action = (Process)sender;
            if (processes.ContainsKey(action.Id) && action.ExitCode != 0)
            {

                if (action.ExitCode != 0)
                {
                    string output;
                    string error;
                    using (StreamReader reader = action.StandardOutput)
                    {
                        output = reader.ReadToEnd();
                    }
                    using (StreamReader reader = action.StandardError)
                    {
                        error = reader.ReadToEnd();
                    }
                    TaskResult result = new TaskResult()
                    {
                        ExitCode = action.ExitCode,
                        StandardOutput = output,
                        StandardError = error
                    };

                    //exit with non-zero code
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error When: {action.ProcessName}");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(result.StandardOutput);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(result.StandardError);
                    Console.ForegroundColor = ConsoleColor.White;
                    //not gracefully killed
                    KillAll();
                    Environment.Exit(action.ExitCode);
                }
                else
                {
                    Process removed = null;
                    while (processes.ContainsKey(action.Id) && !processes.TryRemove(action.Id, out removed)) { }
                }
            }
        }

        static void KillAll()
        {
            Process removed = null;
            while (processes.Count > 0)
            {
                var first = processes.First();
                removed = null;
                while (processes.ContainsKey(first.Key) && !processes.TryRemove(first.Key, out removed))
                {
                    if (!removed.HasExited)
                        removed.Kill();
                }
            }
        }
        
    }


    public static class LinqExtensions
    {
        public static List<string> Append(this List<string> list, string value)
        {
            List<string> newlist = new List<string>(list);
            newlist.Add(value);
            return newlist;
        }
    }

}
