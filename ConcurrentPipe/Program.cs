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

            if(args == null || args.Length == 0)
            {
                foreach(string runnerKey in concurrentSettings.Runners.Keys)
                {
                    RunnerSetting runner = concurrentSettings.Runners[runnerKey];
                    ExecuteRunner(runner, new List<string>()).Wait();
                }
            }
            else
            {
                foreach(string arg in args)
                {
                    if (concurrentSettings.Runners.ContainsKey(arg))
                    {
                        RunnerSetting runner = concurrentSettings.Runners[arg];
                        ExecuteRunner(runner, new List<string>()).Wait();
                    }
                }
            }
            
        }

        static async Task ExecuteRunner(RunnerSetting runner, List<string> paths)
        {
            Console.WriteLine($"Begin Runner - {string.Join("->", paths.Append(runner.Name))}:");

            //execute all the commands first
            var whenAll = Task.WhenAll(runner.Commands.Select(command => RunCommand(runner.Name, command)));

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
                alives = runner.Alives.Select(command => PrepareProcess(runner.Name, command, checks, runner.ReadyTimeout)).ToList();

            //run all the sub runners in serial
            if(runner.Runners != null && runner.Runners.Count > 0)
                await Task.WhenAll(runner.Runners.Select(subrunner => ExecuteRunner(subrunner, paths.Append(runner.Name))));


            if (alives != null && alives.Count > 0)
                foreach(Process action in alives)
                {
                    StopProcess(action);
                }

            Console.WriteLine($"End Runner - {string.Join("->", paths.Append(runner.Name))}.");
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

        static Task<TaskResult> RunCommand(string batch, string command)
        {
            Console.WriteLine($"Begin to run: {command}");
            ProcessStartInfo commandProcess = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c {command}",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            Task<TaskResult> runner = new Task<TaskResult>(() =>
            {
                using (Process action = Process.Start(commandProcess))
                {
                    processes.AddOrUpdate(action.Id, action, (id, value)=> action);
                    action.WaitForExit();
                    Process removed = null;
                    while(processes.ContainsKey(action.Id) && !processes.TryRemove(action.Id, out removed)) { }
                    string output = "";
                    string error = "";
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
                    if (result.ExitCode != 0)
                    {
                        //exit with non-zero code
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error When: {batch} > {command}");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(result.StandardOutput);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(result.StandardError);
                        Console.ForegroundColor = ConsoleColor.White;
                        KillAll();
                        Environment.Exit(result.ExitCode);
                    }
                    return result;
                }
            });
            runner.Start();
            return runner;
        }

        static Process PrepareProcess(string batch, string command, List<Regex> checks, int timeout)
        {
            Console.WriteLine($"Begin to prepare: {command}");
            ProcessStartInfo commandProcess = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c {command}",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
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
