using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

class DockerInspect
{
    public string Architecture { get; set; } = string.Empty;
}

class Program
{
    static int Main()
    {
        return GetDockerArch() ? 0 : 1;
    }

    static bool GetDockerArch()
    {
        var dockerimages = GetDockerImages();
        if (dockerimages == null)
        {
            return false;
        }

        var localarch = RuntimeInformation.OSArchitecture.ToString().ToLower();

        Console.WriteLine($"Got docker images: {dockerimages.Length}");
        Console.WriteLine($"Local arch: '{localarch}'");

        List<(string dockerimage, Process process)> inspectors = [];
        foreach (var d in dockerimages)
        {
            if (d.Contains('<') || d.Contains('>'))
            {
                Console.WriteLine($"Ignoring container image: '{d}'");
                continue;
            }

            ProcessStartInfo startInfo = new("docker", $"inspect {d}") { RedirectStandardOutput = true };
            var p = Process.Start(startInfo);
            if (p == null)
            {
                Thread.Sleep(1000);
                Console.WriteLine($"Failed to start: {startInfo.FileName} {startInfo.Arguments}");
                return false;
            }
            inspectors.Add((d, p));
        }

        foreach (var (dockerimage, process) in inspectors)
        {
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Ignoring container image (1): '{dockerimage}'");
                continue;
            }
            var json = process.StandardOutput.ReadToEnd();

            var inspects = JsonSerializer.Deserialize<DockerInspect[]>(json);
            if (inspects == null)
            {
                Console.WriteLine($"Ignoring container image (2): '{dockerimage}'");
                continue;
            }
            if (inspects.Length != 1)
            {
                Console.WriteLine($"Ignoring container image (3): '{dockerimage}'");
                continue;
            }

            Console.ForegroundColor = localarch == inspects[0].Architecture ? ConsoleColor.Green : ConsoleColor.Yellow;

            Console.WriteLine($"{dockerimage} {inspects[0].Architecture}");
        }

        Console.ResetColor();
        return true;
    }

    static string[]? GetDockerImages()
    {
        ProcessStartInfo startInfo = new("docker", "images") { RedirectStandardOutput = true };
        var p = Process.Start(startInfo);
        if (p == null)
        {
            Thread.Sleep(1000);
            Console.WriteLine($"Failed to start: {startInfo.FileName} {startInfo.Arguments}");
            return null;
        }
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            Console.WriteLine($"Failed to start ({p.ExitCode}): {startInfo.FileName} {startInfo.Arguments}");
            return null;
        }
        var output = p.StandardOutput.ReadToEnd();
        List<string> outputlines = [.. output.Split('\n')];
        _ = outputlines.RemoveAll(l => l.StartsWith("REPOSITORY") || l == string.Empty);
        outputlines = [.. outputlines.OrderBy(l => l)];
        List<string> dockerimages = [];
        foreach (var l in outputlines)
        {
            var i1 = 0;
            var i2 = 0;
            var i3 = 0;
            for (var i = 0; i < l.Length && i3 == 0; i++)
            {
                if (i1 == 0)
                {
                    if (l[i] == ' ')
                    {
                        i1 = i;
                    }
                    continue;
                }
                if (i2 == 0)
                {
                    if (l[i] != ' ')
                    {
                        i2 = i;
                    }
                    continue;
                }
                if (l[i] == ' ')
                {
                    i3 = i;
                }
            }
            if (i1 != 0 && i2 != 0 && i3 != 0)
            {
                var name = l[..i1];
                var tag = l[i2..i3];
                dockerimages.Add(tag == "latest" ? name : $"{name}:{tag}");
            }
        }
        return [.. dockerimages];
    }
}
