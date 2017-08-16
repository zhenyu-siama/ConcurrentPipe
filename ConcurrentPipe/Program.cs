using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using ConcurrentPipe.Entities;
using System.Collections.Concurrent;

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
                    Task.WaitAll(runner.Commands.Select(command => RunCommand(runnerKey, command)).ToArray(), runner.Timeout * 1000);
                }
            }
            else
            {
                foreach(string arg in args)
                {
                    if (concurrentSettings.Runners.ContainsKey(arg))
                    {
                        RunnerSetting runner = concurrentSettings.Runners[arg];
                        Task.WaitAll(runner.Commands.Select(command => RunCommand(arg, command)).ToArray(), runner.Timeout * 1000);
                    }
                }
            }
            
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
                        while(processes.Count > 0)
                        {
                            var first = processes.First();
                            removed = null;
                            while (processes.ContainsKey(first.Key) && !processes.TryRemove(first.Key, out removed))
                            {
                                if (!removed.HasExited)
                                    removed.Kill();
                            }
                        }
                        Environment.Exit(result.ExitCode);
                    }
                    return result;
                }
            });
            runner.Start();
            return runner;
        }
    }



}
