using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using VsTeXCommentsExtension.Integration.Data;

namespace PerformanceTests
{
    class Program
    {
        static void Main()
        {
            string text = RandomCodeGenerator.GenerateCode(segmentsCount: 1000);

            //Load referenced assembly
            typeof(IContentType).ToString();


            const string VsAssembliesPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\IDE\CommonExtensions\Microsoft\Editor";
            //const string VsAssembliesPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\CommonExtensions\Microsoft\Editor";
            AppDomain.CurrentDomain.AssemblyResolve +=
                (object sender, ResolveEventArgs args) =>
                {
                    var shortName = GetShortName(args.Name);

                    //need to use assemblies referenced by this project and not by VS assemblies
                    var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var loadedAssembly = loadedAssemblies.Where(a => GetShortName(a.FullName) == shortName).SingleOrDefault();
                    if (loadedAssembly != null) return loadedAssembly;

                    var path = Path.Combine(VsAssembliesPath, shortName + ".dll");
                    if (File.Exists(path)) return Assembly.LoadFile(path);
                    throw new InvalidOperationException($"Unable to find assembly '{args.Name}'.");

                    string GetShortName(string assemblyFullName) => assemblyFullName.Split(',')[0];
                };

            var vsEditorAssembly = Assembly.LoadFile(Path.Combine(VsAssembliesPath, "Microsoft.VisualStudio.Platform.VSEditor.dll"));
            var contentType = CreateContentType(vsEditorAssembly, "CSharp");
            var stringRebuilder = CreateStringRebuilder(vsEditorAssembly, text);
            var textBuffer = CreateTextBuffer(vsEditorAssembly, contentType, stringRebuilder, null, null);

            var spans = new NormalizedSnapshotSpanCollection(textBuffer.CurrentSnapshot, new Span(0, text.Length));
            var teXCommentTagger = new TeXCommentTagger(textBuffer);


            var watch = new Stopwatch();
            Console.WriteLine($"{nameof(TeXCommentTagger)} performance:");
            for (int i = 0; i < 10; i++)
            {
                watch.Restart();
                const int Iterations = 1000000;
                for (int iteration = 0; iteration < Iterations; iteration++)
                {
                    var tags = teXCommentTagger.GetTags(spans);
                    var count = 0;
                    foreach (var tag in tags) ++count;
                }
                Console.WriteLine($"{watch.ElapsedMilliseconds}ms");
            }
        }

        private static IContentType CreateContentType(Assembly vsEditorAssembly, string contentType)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Utilities.Implementation.ContentTypeImpl");
            var ctor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(string), typeof(IEnumerable<string>) },
                null);
            return (IContentType)ctor.Invoke(new[] { contentType, null, null }); //string name, string mimeType = null, IEnumerable<string> baseTypes = null
        }

        private static object CreateStringRebuilder(Assembly vsEditorAssembly, string text)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Text.Implementation.StringRebuilder");
            var createMethod = type.GetMethod(
                "Create",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(string) },
                null);
            return createMethod.Invoke(null, new[] { text }); //string text
        }

        private static ITextBuffer CreateTextBuffer(Assembly vsEditorAssembly, IContentType contentType, object stringRebuilder, object textDifferencingService, object guardedOperations)
        {
            var type = vsEditorAssembly.GetType("Microsoft.VisualStudio.Text.Implementation.TextBuffer");
            var ctor = type.GetConstructors().Single(c => c.GetParameters().Length == 4);
            return (ITextBuffer)ctor.Invoke(new[] { contentType, stringRebuilder, textDifferencingService, guardedOperations });
        }
    }
}