using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using VsTeXCommentsExtension.Integration;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace SnippetThumbnailsGenerator
{
    class Program
    {
        const string OutputPath = "Output";
        static HtmlRenderer renderer;

        [STAThread]
        static void Main(string[] args)
        {
            new System.Windows.Application(); //needed for correct resour loading

            var rendererCache = new HtmlRendererCache();
            if (Directory.Exists(rendererCache.CacheDirectory))
            {
                Directory.Delete(rendererCache.CacheDirectory, true);
            }

            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);

            var form = new Form();
            form.Load +=
                (s, e) =>
                {
                    renderer = new HtmlRenderer();
                    Task.Run(new Action(GenerateSnippets));
                };
            Application.Run(form); //we need message pump for web browser
        }

        private static void GenerateSnippets()
        {
            var font = new System.Drawing.Font("Consolas", 12);
            var cfg = XElement.Load(@"..\..\Snippets.xml");


            var exportElement = new XElement("Snippets");
            int index = 0;
            foreach (var snippet in cfg.Elements("Snippet"))
            {
                var code = snippet.Element("Code").Value;
                Console.WriteLine($"Rendering {code}");

                var result = renderer.Render(
                      new HtmlRenderer.Input(
                          new TeXCommentTag($"$${code}$$", default(TeXCommentBlockSpan)),
                          1.3,
                          Colors.Black,
                          Colors.White,
                          font,
                          null,
                          null));

                SaveSnippet(result.CachePath, snippet.Element("Group").Value, code, $"{++index}.png", exportElement);
            }

            exportElement.Save(Path.Combine(OutputPath, "Snippets.xml"));

            Console.WriteLine("Done");
        }

        private static void SaveSnippet(string renderedImagePath, string group, string code, string outputPath, XElement exportElement)
        {
            var absoluteOutputPath = Path.Combine(Environment.CurrentDirectory, OutputPath, outputPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            File.Copy(renderedImagePath, absoluteOutputPath);


            bool isMultiline = code.Contains('\n');
            if (isMultiline)
            {
                var lines = code.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = "//" + lines[i];
                }
                code = lines.Aggregate((a, b) => a + "\r\n" + b);
            }

            var snippetElement = new XElement("Snippet");
            snippetElement.Add(new XElement("Group") { Value = group });
            snippetElement.Add(new XElement("Code") { Value = code });
            snippetElement.Add(new XElement("Icon") { Value = "Snippets/" + outputPath });
            exportElement.Add(snippetElement);
        }
    }
}
