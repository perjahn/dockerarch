using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

class DockerInspect
{
    public string Architecture { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
}

class Program
{
    static int Main(string[] args)
    {
        GetDockerArch();
        return 0;
    }

    static void GetDockerArch()
    {
        var dockerimages = GetDockerImages();

        List<(string dockerimage, Process process)> inspectors = [];
        foreach (var d in dockerimages)
        {
            if (d.Contains('<') || d.Contains('>'))
            {
                Console.WriteLine($"Ignoring container image: '{d}'");
                continue;
            }

            Process p = new();
            p.StartInfo.FileName = "docker";
            p.StartInfo.Arguments = $"inspect {d}";
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            inspectors.Add((d, p));
        }

        Process localarchprocess = new();
        localarchprocess.StartInfo.FileName = "uname";
        localarchprocess.StartInfo.Arguments = "-m";
        localarchprocess.StartInfo.RedirectStandardOutput = true;
        localarchprocess.Start();
        localarchprocess.WaitForExit();
        var localarch = localarchprocess.StandardOutput.ReadToEnd().TrimEnd();
        Console.WriteLine($"Local arch: '{localarch}'");

        foreach (var (dockerimage, process) in inspectors)
        {
            process.WaitForExit();
            var json = process.StandardOutput.ReadToEnd();

            var inspects = JsonSerializer.Deserialize<DockerInspect[]>(json);
            if (inspects == null)
            {
                continue;
            }
            if (inspects.Length != 1)
            {
                Console.WriteLine($"Ignoring container image: '{dockerimage}'");
                continue;
            }

            if (localarch == inspects[0].Architecture)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }

            Console.WriteLine($"{dockerimage} {inspects[0].Architecture}");
        }

        Console.ResetColor();
    }

    static string[] GetDockerImages()
    {
        Process p = new();
        p.StartInfo.FileName = "docker";
        p.StartInfo.Arguments = "images";
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        p.WaitForExit();
        var output = p.StandardOutput.ReadToEnd();
        List<string> outputlines = [.. output.Split('\n')];
        Console.WriteLine($"Got {outputlines.Count} lines.");
        outputlines.RemoveAll(l => l.StartsWith("REPOSITORY") || l == string.Empty);
        outputlines = [.. outputlines.OrderBy(l => l)];
        List<string> dockerimages = [];
        foreach (var l in outputlines)
        {
            int i1 = 0;
            int i2 = 0;
            int i3 = 0;
            for (var i = 0; i < l.Length; i++)
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
                if (i3 == 0)
                {
                    if (l[i] == ' ')
                    {
                        i3 = i;
                        break;
                    }
                    continue;
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
